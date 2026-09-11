using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using AirGestureAI.AgentRuntime;
using AirGestureAI.AIProviders;
using AirGestureAI.AIWorker;
using AirGestureAI.Analytics;
using AirGestureAI.ApiDocs;
using AirGestureAI.Camera;
using AirGestureAI.Cognitive;
using AirGestureAI.Compatibility;
using AirGestureAI.Configuration;
using AirGestureAI.Cursor;
using AirGestureAI.DesktopAutomation;
using AirGestureAI.DiagnosticsSubsystem;
using AirGestureAI.Experiments;
using AirGestureAI.Extensions;
using AirGestureAI.Federated;
using AirGestureAI.FederatedLearning;
using AirGestureAI.GestureAI;
using AirGestureAI.GestureRecognition;
using AirGestureAI.HandTracking;
using AirGestureAI.HoverSelection;
using AirGestureAI.Input;
using AirGestureAI.Learning;
using AirGestureAI.Memory;
using AirGestureAI.Migration;
using AirGestureAI.ModelZoo;
using AirGestureAI.OnnxEngine;
using AirGestureAI.Optimization;
using AirGestureAI.Planning;
using AirGestureAI.PluginRuntime;
using AirGestureAI.Plugins;
using AirGestureAI.Plugins.YouTube;
using AirGestureAI.Production;
using AirGestureAI.Release;
using AirGestureAI.Research;
using AirGestureAI.SDK;
using AirGestureAI.Security;
using AirGestureAI.SecurityAudit;
using AirGestureAI.Semantic;
using AirGestureAI.Services;
using AirGestureAI.ServicesHost;
using AirGestureAI.SpatialComputing;
using AirGestureAI.Tools;
using AirGestureAI.TrackerHost;
using AirGestureAI.Utilities;
using AirGestureAI.ViewModels;
using AirGestureAI.Views;
using AirGestureAI.WorkflowEngine;
using AirGestureAI.XR;
using Microsoft.Extensions.DependencyInjection;

namespace AirGestureAI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private IServiceProvider? _serviceProvider;
        private string _appDataPath = string.Empty;
        private CancellationTokenSource? _appLifetimeCts;

        /// <summary>
        /// Exposes read-only recovery status for UI and diagnostic tooling.
        /// </summary>
        public RecoveryStatus RecoveryStatus { get; private set; } = new();

        /// <summary>
        /// Gets the current App instance.
        /// </summary>
        public static new App Current => (App)Application.Current;

        /// <summary>
        /// Custom startup handler setting up DI container and initializing systems.
        /// </summary>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Configure AppData directory for local logs
            _appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AirGestureAI"
            );
            var appDataPath = _appDataPath;

            // Initialize structured LoggingService first
            var loggingService = new AirGestureAI.Services.LoggingService(appDataPath);
            Logger.Initialize(loggingService);

            Logger.Info("AirGesture AI 4.1 RTM starting...");

            // ── v4.0.0: Subprocess mode detection ─────────────────────────────
            // When launched with --tracker-host, run as the hand-tracker subprocess.
            // When launched with --ai-worker, run as the AI inference subprocess.
            // In both cases we skip the WPF UI entirely.
            var args = Environment.GetCommandLineArgs();
            if (args.Contains("--tracker-host"))
            {
                Logger.Info("[TrackerHost] Subprocess mode detected. Starting TrackerHost IPC loop.");
                var host = new AirGestureAI.TrackerHost.TrackerHost();
                host.Start();
                // Keep the process alive — TrackerHost IPC server runs on background threads.
                // The host will block on the named-pipe server internally.
                return;
            }

            if (args.Contains("--ai-worker"))
            {
                Logger.Info("[AIWorkerHost] Subprocess mode detected. Starting AIWorker IPC loop.");
                var aiHost = new AIWorkerHost();
                aiHost.Start();
                return;
            }

            Logger.Info("Application startup sequence initiated.");
            _appLifetimeCts = new CancellationTokenSource();

            // Initialize StartupProfiler
            var startupProfiler = new AirGestureAI.Services.StartupProfiler(appDataPath);

            // Configure Services DI
            var swDi = Stopwatch.StartNew();
            var services = new ServiceCollection();
            ConfigureServices(services, loggingService, startupProfiler);
            // Build service container
            _serviceProvider = services.BuildServiceProvider();
            swDi.Stop();
            startupProfiler.RecordDiBuild(swDi.ElapsedMilliseconds);

            // Apply startup performance optimizations
            var optimizer = _serviceProvider.GetRequiredService<PerformanceOptimizer>();
            optimizer.Apply();

            // ── Milestone 3: Startup Recovery Flow ───────────────────────────
            // Run recovery agent synchronously before showing any UI so the
            // session state can be applied before the MainWindow is constructed.
            try
            {
                Logger.Info("[Recovery] Starting recovery sequence...");
                var recoveryAgent = _serviceProvider.GetRequiredService<RecoveryAgent>();
                var stateProvider = _serviceProvider.GetRequiredService<ISessionStateProvider>();
                var autoSave = _serviceProvider.GetRequiredService<AutoSaveService>();

                var recoveredState = recoveryAgent.ExecuteRecoveryAsync(
                    silentMode: false,
                    cancellationToken: _appLifetimeCts.Token)
                    .GetAwaiter().GetResult();

                // Apply recovered state to live services (camera, preferences, workflows, layout)
                stateProvider.ApplyStateAsync(recoveredState).GetAwaiter().GetResult();

                // Populate read-only RecoveryStatus for the UI layer
                var crashSvc = _serviceProvider.GetRequiredService<CrashRecoveryService>();
                RecoveryStatus = new RecoveryStatus
                {
                    IsSafeMode = crashSvc.IsSafeMode,
                    AbnormalShutdownDetected = crashSvc.AbnormalShutdownDetected,
                    RecoverySource = crashSvc.LastRecoveryReport?.RecoverySource ?? "None",
                    ConsecutiveFailureCount = crashSvc.LastRecoveryReport?.ConsecutiveFailureCount ?? 0,
                    LastValidationSeverity = recoveryAgent.LastValidationReport?.OverallSeverity ?? "Healthy",
                    LastSuccessfulSaveTime = autoSave.LastSuccessfulSaveTime
                };

                Logger.Info($"[Recovery] Complete. Source={RecoveryStatus.RecoverySource}, " +
                            $"SafeMode={RecoveryStatus.IsSafeMode}, " +
                            $"Severity={RecoveryStatus.LastValidationSeverity}.");

                // ── Milestone 4: Start AutoSave on app lifetime token ──────────
                _ = autoSave.StartAsync(_appLifetimeCts.Token);
                Logger.Info("[Recovery] AutoSaveService started.");
            }
            catch (Exception ex)
            {
                Logger.Error("[Recovery] Recovery sequence failed — proceeding with default state.", ex);
            }

            // Run MainWindow
            try
            {
                // Explicit shutdown control prevents premature exit during wizard/startup transition
                ShutdownMode = ShutdownMode.OnExplicitShutdown;

                var wizardService = _serviceProvider.GetRequiredService<SetupWizardService>();
                if (wizardService.IsFirstRun)
                {
                    Logger.Info("First-run detected. Launching Setup Wizard…");
                    var wizardWin = _serviceProvider.GetRequiredService<SetupWizardWindow>();
                    if (wizardWin.ShowDialog() != true)
                    {
                        Logger.Info("Setup Wizard cancelled by user. Shutting down.");
                        Shutdown(0);
                        return;
                    }
                }

                // Resolve MainWindow
                var swWindow = Stopwatch.StartNew();
                var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
                swWindow.Stop();
                startupProfiler.RecordMainWindowCreation(swWindow.ElapsedMilliseconds);

                // Assign MainWindow
                MainWindow = mainWindow;

                // Show MainWindow
                var swShow = Stopwatch.StartNew();
                mainWindow.Show();
                swShow.Stop();
                startupProfiler.RecordUiShown(swShow.ElapsedMilliseconds);
                Logger.Info("MainWindow shown successfully.");

                // After MainWindow is successfully shown, configure normal shutdown behavior
                ShutdownMode = ShutdownMode.OnMainWindowClose;

                // Defer heavy initializations to background execution
                optimizer.ScheduleBackgroundTask(async (ct) =>
                {
                    // Initialize ONNX Runtime Engine
                    var swAi = Stopwatch.StartNew();
                    _serviceProvider.GetRequiredService<OnnxRuntimeEngine>().Initialize();
                    swAi.Stop();
                    startupProfiler.RecordAiLoad(swAi.ElapsedMilliseconds);

                    // Initialize Service Host (manages subprocess lifecycle)
                    _serviceProvider.GetRequiredService<ServiceHost>().Start();

                    // Initialize Enterprise Security Center
                    _serviceProvider.GetRequiredService<EnterpriseSecurityCenter>().Initialize();

                    // Initialize Agent Orchestrator
                    _serviceProvider.GetRequiredService<AgentOrchestrator>().Start();

                    // Initialize Spatial Computing Center
                    _serviceProvider.GetRequiredService<SpatialComputingCenter>().Initialize();

                    // Initialize Desktop Automation Manager
                    _serviceProvider.GetRequiredService<DesktopAutomationManager>().Initialize();

                    // Initialize Diagnostics Manager
                    _serviceProvider.GetRequiredService<AirGestureAI.DiagnosticsSubsystem.DiagnosticsManager>();

                    // Measure mock plugin load
                    var swPlugin = Stopwatch.StartNew();
                    await Task.Delay(50, ct).ConfigureAwait(false); // Simulating plugin registry scanning
                    swPlugin.Stop();
                    startupProfiler.RecordPluginLoad(swPlugin.ElapsedMilliseconds);

                    // Measure mock camera init
                    var swCam = Stopwatch.StartNew();
                    await Task.Delay(50, ct).ConfigureAwait(false); // Simulating camera provider connection
                    swCam.Stop();
                    startupProfiler.RecordCameraInitialization(swCam.ElapsedMilliseconds);

                    // Finalize profile and generate startup_report.json
                    startupProfiler.FinalizeProfile();

                    Logger.Info("Startup complete.");
                }, "BackgroundInitialization");
            }
            catch (Exception ex)
            {
                Logger.Error("Fatal startup error resolving MainWindow", ex);
                MessageBox.Show($"Failed to launch application: {ex.Message}", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(-1);
            }
        }

        private void ConfigureServices(IServiceCollection services, AirGestureAI.Services.LoggingService loggingService, AirGestureAI.Services.StartupProfiler startupProfiler)
        {
            string appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AirGestureAI");

            // Register LoggingService and StartupProfiler as singletons
            services.AddSingleton<AirGestureAI.Services.LoggingService>(loggingService);
            services.AddSingleton<AirGestureAI.Services.StartupProfiler>(startupProfiler);

            // Register configuration (load persisted appsettings.json or use defaults)
            string configPath = Path.Combine(appDataPath, "appsettings.json");
            var config = AppConfig.Load(configPath);
            services.AddSingleton<AppConfig>(config);

            // Register utilities and manager
            services.AddSingleton<DependencyManager>();

            // Register core modules
            services.AddSingleton<ICameraProvider, OpenCvCameraProvider>();
            services.AddSingleton<IHandTracker, PythonHandTracker>();
            services.AddSingleton<IHoverEngine, HoverEngine>();
            services.AddSingleton<ICursorEngine, CursorEngine>();
            services.AddSingleton<IGestureDetector, GestureEngine>();
            services.AddSingleton<IInputSimulator, WindowsInputSimulator>();

            // Register coordinator orchestrator and pipeline manager
            services.AddSingleton<PipelineManager>();
            services.AddSingleton<AppCoordinator>();

            // Phase 9: Application Context Engine
            services.AddSingleton<IApplicationContextEngine, ApplicationContextEngine>();

            // Phase 10: Plugin Framework
            services.AddSingleton<PluginRegistry>();
            services.AddSingleton<PluginManager>();

            // Phase 11: YouTube Smart Adapter (registered directly for DI resolution)
            services.AddSingleton<YouTubeSmartAdapter>();

            // Phase 24: SDK, Extensions, Compatibility & Migration
            services.AddSingleton<ISdkHost, SdkHost>();
            services.AddSingleton<SdkGenerator>();
            services.AddSingleton<ExtensionInstaller>(sp =>
            {
                var installRoot = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AirGestureAI");
                return new ExtensionInstaller(installRoot, sp.GetRequiredService<ISdkHost>());
            });
            services.AddSingleton<DocumentationBuilder>();
            services.AddSingleton<ApiDocumentationGenerator>();
            services.AddSingleton<SampleProjectGenerator>(sp =>
            {
                var outputDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AirGestureAI", "SampleProjects");
                return new SampleProjectGenerator(outputDir);
            });
            services.AddSingleton<CompatibilityChecker>();
            services.AddSingleton<DeprecationManager>();
            services.AddSingleton<MigrationAdvisor>();
            services.AddSingleton<ApiDiffAnalyzer>();
            services.AddSingleton<MigrationEngine>();

            // Phase 25: Release & Security Audit stubs
            services.AddSingleton<ReleaseManager>();
            services.AddSingleton<SecurityAuditManager>();

            // Phase 25: Performance Optimizer
            services.AddSingleton<PerformanceOptimizer>();

            // ── Phase 27: Research Platform ───────────────────────────────────

            // Research
            services.AddSingleton<ResearchManager>();

            // Learning
            services.AddSingleton<LearningProfile>();
            services.AddSingleton<GestureTrainer>();
            services.AddSingleton<ConfidenceOptimizer>();
            services.AddSingleton<ModelEvaluator>();
            services.AddSingleton<PersonalizationManager>();
            services.AddSingleton<AdaptiveLearningEngine>();

            // Federated
            services.AddSingleton<PrivacyManager>();
            services.AddSingleton<ModelSynchronizer>();
            services.AddSingleton<AggregationEngine>();
            services.AddSingleton<FederatedCoordinator>();

            // Analytics
            services.AddSingleton<AnalyticsEngine>();

            // Experiments
            services.AddSingleton<ExperimentManager>();

            // XR
            services.AddSingleton<XRSession>();
            services.AddSingleton<XRInputProvider>();

            // AI Providers
            services.AddSingleton<ProviderManager>(sp =>
                new ProviderManager(sp.GetRequiredService<AppConfig>().DefaultAiProvider));

            // Model Zoo
            services.AddSingleton<ModelCatalog>();
            services.AddSingleton<ModelDownloader>();
            services.AddSingleton<ModelValidator>();
            services.AddSingleton<ModelBenchmark>();

            // ── v4.0.0 Milestone 1: ServicesHost & IPC ────────────────────────
            services.AddSingleton<ServiceHost>();
            services.AddSingleton<ServiceRegistry>();
            services.AddSingleton<ServiceHeartbeat>();
            services.AddSingleton<ServiceProcessManager>();
            services.AddSingleton<ServiceDiscovery>();

            // ── v4.0.0 Milestone 1: TrackerHost & AIWorker bridges ────────────
            services.AddSingleton<TrackerBridge>();
            services.AddSingleton<AIWorkerBridge>();

            // ── v4.0.0 Milestone 1: PluginRuntime ────────────────────────────
            services.AddSingleton<RuntimeManager>();
            services.AddSingleton<AirGestureAI.PluginRuntime.PluginManifestValidator>();

            // ── v4.0.0 Milestone 1: Diagnostics ──────────────────────────────
            services.AddSingleton<PerformanceProfilerService>(sp => new PerformanceProfilerService(appDataPath));
            services.AddSingleton<MemoryDiagnosticsService>(sp => new MemoryDiagnosticsService(appDataPath));
            services.AddSingleton<AccessibilityVerifierService>(sp => new AccessibilityVerifierService(appDataPath));
            services.AddSingleton<SystemDiagnosticsService>(sp => new SystemDiagnosticsService(appDataPath));
            services.AddSingleton<PluginDiagnosticsService>(sp => new PluginDiagnosticsService(appDataPath, sp.GetRequiredService<PluginManager>()));
            services.AddSingleton<AirGestureAI.DiagnosticsSubsystem.DiagnosticsManager>();

            // ── v4.0.0 Milestone 2: AI Subsystems ────────────────────────────
            services.AddSingleton<SemanticIntentEngine>();
            services.AddSingleton<OnnxRuntimeEngine>();
            services.AddSingleton<GestureSequenceEngine>();
            services.AddSingleton<AIModelManager>();
            services.AddSingleton<FederatedLearningCoordinator>();

            // ── v4.0.0 Milestone 3: Spatial Computing ────────────────────────
            services.AddSingleton<SpatialComputingCenter>();

            // ── v4.0.0 Milestone 4: Enterprise Security ───────────────────────
            services.AddSingleton<EnterpriseSecurityCenter>();

            // ── v4.0.0 Milestone 5: Agent Runtime ─────────────────────────────────────
            services.AddSingleton<AgentRegistry>();
            services.AddSingleton<AgentScheduler>();
            services.AddSingleton<AgentMessageBus>();
            // NOTE: GestureSequenceEngine already registered above in Milestone 2 – no duplicate here.
            // AgentOrchestrator receives DI-registered singletons via constructor injection.
            services.AddSingleton<AgentOrchestrator>(sp => new AgentOrchestrator(
                sp.GetRequiredService<AgentRegistry>(),
                sp.GetRequiredService<AgentScheduler>(),
                sp.GetRequiredService<AgentMessageBus>()));

            // ── v4.0.0 Milestone 6: Autonomous Intelligence ──────────────────
            services.AddSingleton<MemoryManager>();
            services.AddSingleton<PlanExecutor>();
            services.AddSingleton<CognitiveEngine>();
            services.AddSingleton<WorkflowEngineService>();
            services.AddSingleton<ToolRegistry>();

            // ── v4.0.0 Milestone 7: Desktop Automation ────────────────────────
            services.AddSingleton<DesktopAutomationManager>();

            // ── v4.0.0 Milestone 7: Production Operations ─────────────────────
            services.AddSingleton<ProductionOperationsCenter>();

            // ── v4.1 Sprint 1 UI / Services ──────────────────────────────────
            services.AddSingleton<SetupWizardService>();
            services.AddTransient<SetupWizardViewModel>();
            services.AddTransient<SetupWizardWindow>();

            services.AddSingleton<GestureProfileService>();
            services.AddTransient<GestureManagerViewModel>();
            services.AddTransient<GestureManagerView>();

            services.AddTransient<WorkflowEditorViewModel>();
            services.AddTransient<WorkflowEditorView>();

            services.AddTransient<PerformanceDashboardViewModel>();
            services.AddTransient<PerformanceDashboardView>();

            services.AddTransient<PluginBrowserViewModel>();
            services.AddTransient<PluginBrowserView>();

            services.AddSingleton<NotificationService>();

            // ── Phase 5 / Milestone 2: Crash Recovery Services ───────────────
            services.AddSingleton<SessionStateManager>(sp => new SessionStateManager(
                appDataPath,
                sp.GetRequiredService<LoggingService>()));

            services.AddSingleton<CrashRecoveryService>(sp => new CrashRecoveryService(
                sp.GetRequiredService<SessionStateManager>(),
                sp.GetRequiredService<LoggingService>(),
                appDataPath));

            // ISessionStateProvider must be registered before AutoSaveService
            services.AddSingleton<ISessionStateProvider, SessionStateProvider>();

            services.AddSingleton<AutoSaveService>(sp => new AutoSaveService(
                sp.GetRequiredService<SessionStateManager>(),
                sp.GetRequiredService<ISessionStateProvider>(),
                sp.GetRequiredService<LoggingService>(),
                appDataPath,
                intervalSeconds: 30));

            services.AddSingleton<IRecoveryNotificationService, WpfRecoveryNotificationService>();

            services.AddSingleton<RecoveryAgent>(sp => new RecoveryAgent(
                sp.GetRequiredService<SessionStateManager>(),
                sp.GetRequiredService<CrashRecoveryService>(),
                sp.GetRequiredService<LoggingService>(),
                appDataPath,
                sp.GetRequiredService<IRecoveryNotificationService>()));

            // Register View (UI)
            services.AddSingleton<MainWindow>();
        }

        /// <summary>
        /// Triggered when the application is closing down.
        /// </summary>
        protected override void OnExit(ExitEventArgs e)
        {
            Logger.Info("[Shutdown] Application exit sequence initiated.");

            // Milestone 5: Graceful Shutdown
            // Cancel the app lifetime token to stop AutoSave background loop.
            try { _appLifetimeCts?.Cancel(); } catch { /* best-effort */ }

            // Stop AutoSaveService cleanly, then flush a final state save.
            try
            {
                var autoSave = _serviceProvider?.GetService<AutoSaveService>();
                if (autoSave != null)
                {
                    Logger.Info("[Shutdown] Stopping AutoSaveService...");
                    Task.Run(async () =>
                    {
                        await autoSave.StopAsync().ConfigureAwait(false);
                        autoSave.MarkDirty();
                        await autoSave.SaveNowAsync().ConfigureAwait(false);
                    }).GetAwaiter().GetResult();
                    Logger.Info("[Shutdown] Final session state saved.");
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"[Shutdown] AutoSaveService stop/save failed: {ex.Message}");
            }

            // Remove crash lock file to signal a clean exit.
            try
            {
                var crashSvc = _serviceProvider?.GetService<CrashRecoveryService>();
                if (crashSvc != null)
                {
                    Task.Run(async () => await crashSvc.DisposeAsync().ConfigureAwait(false)).GetAwaiter().GetResult();
                    Logger.Info("[Shutdown] CrashRecoveryService lock file removed (clean exit).");
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"[Shutdown] CrashRecoveryService dispose failed: {ex.Message}");
            }

            // Stop ServiceHost (manages subprocess lifecycle)
            try
            {
                var serviceHost = _serviceProvider?.GetService<ServiceHost>();
                serviceHost?.Stop();
            }
            catch (Exception ex)
            {
                Logger.Warn($"[Shutdown] Failed to stop ServiceHost on exit: {ex.Message}");
            }

            _appLifetimeCts?.Dispose();

            // Safely dispose DI service provider with async disposal support to prevent InvalidOperationException
            DisposeServiceProvider(_serviceProvider);

            base.OnExit(e);
            Logger.Info("[Shutdown] Application shut down clean.");
        }

        /// <summary>
        /// Safely disposes the DI service provider, executing asynchronous disposal
        /// on a threadpool task to avoid deadlocks with the WPF DispatcherSynchronizationContext,
        /// and falling back to synchronous IDisposable cleanup.
        /// </summary>
        /// <param name="serviceProvider">The service provider to dispose.</param>
        public static void DisposeServiceProvider(IServiceProvider? serviceProvider)
        {
            if (serviceProvider == null) return;

            try
            {
                if (serviceProvider is IAsyncDisposable asyncDisposable)
                {
                    Task.Run(async () =>
                    {
                        await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                    }).GetAwaiter().GetResult();
                }
                else if (serviceProvider is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"[Shutdown] Service provider disposal error: {ex.Message}");
            }
        }

        /// <summary>
        /// Asynchronously disposes the DI service provider.
        /// </summary>
        /// <param name="serviceProvider">The service provider to dispose.</param>
        public static async Task DisposeServiceProviderAsync(IServiceProvider? serviceProvider)
        {
            if (serviceProvider == null) return;

            try
            {
                if (serviceProvider is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                }
                else if (serviceProvider is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"[Shutdown] Service provider async disposal error: {ex.Message}");
            }
        }
    }
}
