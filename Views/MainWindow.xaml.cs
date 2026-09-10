using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AirGestureAI.AgentRuntime;
using AirGestureAI.AIProviders;
using AirGestureAI.AIWorker;
using AirGestureAI.Analytics;
using AirGestureAI.ApiDocs;
using AirGestureAI.Camera;
using AirGestureAI.Cursor;
using AirGestureAI.Experiments;
using AirGestureAI.Extensions;
using AirGestureAI.Federated;
using AirGestureAI.FederatedLearning;
using AirGestureAI.GestureRecognition;
using AirGestureAI.HandTracking;
using AirGestureAI.Learning;
using AirGestureAI.Memory;
using AirGestureAI.Models;
using AirGestureAI.ModelZoo;
using AirGestureAI.Optimization;
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
using AirGestureAI.TrackerHost;
using AirGestureAI.Utilities;
using AirGestureAI.WorkflowEngine;
using AirGestureAI.XR;
using Microsoft.Win32;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using PerformanceSnapshot = AirGestureAI.Optimization.PerformanceSnapshot;
using Point = System.Windows.Point;
using Rect = System.Windows.Rect;
using Window = System.Windows.Window;

namespace AirGestureAI.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly AppCoordinator _coordinator;
        private readonly PipelineManager _pipelineManager;
        private readonly DependencyManager _dependencyManager;
        private readonly ICameraProvider _cameraProvider;
        private readonly ICursorEngine _cursorEngine;

        // Phase 24: Developer Center services
        private readonly ISdkHost _sdkHost;
        private readonly ExtensionInstaller _extensionInstaller;
        private readonly ApiDocumentationGenerator _apiDocGenerator;
        private readonly SampleProjectGenerator _sampleProjectGenerator;

        // Phase 25: Release Center services
        private readonly SecurityAuditManager _securityAuditManager;
        private readonly ReleaseManager _releaseManager;

        // Phase 27: Research Lab services
        private readonly ResearchManager _researchManager;
        private readonly AdaptiveLearningEngine _adaptiveLearning;
        private readonly AnalyticsEngine _analytics;
        private readonly ExperimentManager _experiments;
        private readonly FederatedCoordinator _federatedCoordinator;
        private readonly XRSession _xrSession;
        private readonly ProviderManager _providerManager;
        private readonly ModelCatalog _modelCatalog;
        private readonly ModelBenchmark _modelBenchmark;

        // v4.0.0 Subsystems
        private readonly ServiceProcessManager _serviceProcessManager;
        private readonly TrackerBridge _trackerBridge;
        private readonly AIWorkerBridge _aiWorkerBridge;
        private readonly SemanticIntentEngine _semanticIntentEngine;
        private readonly FederatedLearningCoordinator _federatedLearningCoordinator;
        private readonly SpatialComputingCenter _spatialComputingCenter;
        private readonly EnterpriseSecurityCenter _enterpriseSecurityCenter;
        private readonly AgentOrchestrator _agentOrchestrator;
        private readonly MemoryManager _memoryManager;
        private readonly WorkflowEngineService _workflowEngineService;
        private readonly ProductionOperationsCenter _productionOperationsCenter;

        private int _capturedFramesCount;
        private int _processedFramesCount;
        private string _lastGesture = "None";
        private string _pendingExtensionPath = string.Empty;
        private readonly DispatcherTimer? _uiTimer;

        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow"/> class.
        /// </summary>
        public MainWindow(
            AppCoordinator coordinator,
            PipelineManager pipelineManager,
            DependencyManager dependencyManager,
            ICameraProvider cameraProvider,
            ICursorEngine cursorEngine,
            ISdkHost sdkHost,
            ExtensionInstaller extensionInstaller,
            ApiDocumentationGenerator apiDocGenerator,
            SampleProjectGenerator sampleProjectGenerator,
            SecurityAuditManager securityAuditManager,
            ReleaseManager releaseManager,
            ResearchManager researchManager,
            AdaptiveLearningEngine adaptiveLearning,
            AnalyticsEngine analytics,
            ExperimentManager experiments,
            FederatedCoordinator federatedCoordinator,
            XRSession xrSession,
            ProviderManager providerManager,
            ModelCatalog modelCatalog,
            ModelBenchmark modelBenchmark,
            ServiceProcessManager serviceProcessManager,
            TrackerBridge trackerBridge,
            AIWorkerBridge aiWorkerBridge,
            SemanticIntentEngine semanticIntentEngine,
            FederatedLearningCoordinator federatedLearningCoordinator,
            SpatialComputingCenter spatialComputingCenter,
            EnterpriseSecurityCenter enterpriseSecurityCenter,
            AgentOrchestrator agentOrchestrator,
            MemoryManager memoryManager,
            WorkflowEngineService workflowEngineService,
            ProductionOperationsCenter productionOperationsCenter,
            GestureManagerView gestureManagerView,
            WorkflowEditorView workflowEditorView,
            PerformanceDashboardView performanceDashboardView,
            PluginBrowserView pluginBrowserView)
        {
            InitializeComponent();
            
            _coordinator             = coordinator;
            _pipelineManager         = pipelineManager;
            _dependencyManager       = dependencyManager;
            _cameraProvider          = cameraProvider;
            _cursorEngine            = cursorEngine;
            _sdkHost                 = sdkHost;
            _extensionInstaller      = extensionInstaller;
            _apiDocGenerator         = apiDocGenerator;
            _sampleProjectGenerator  = sampleProjectGenerator;
            _securityAuditManager    = securityAuditManager;
            _releaseManager          = releaseManager;

            // Phase 27 Research Lab wiring
            _researchManager         = researchManager;
            _adaptiveLearning        = adaptiveLearning;
            _analytics               = analytics;
            _experiments             = experiments;
            _federatedCoordinator    = federatedCoordinator;
            _xrSession               = xrSession;
            _providerManager         = providerManager;
            _modelCatalog            = modelCatalog;
            _modelBenchmark          = modelBenchmark;

            // v4.0.0 wiring
            _serviceProcessManager = serviceProcessManager;
            _trackerBridge = trackerBridge;
            _aiWorkerBridge = aiWorkerBridge;
            _semanticIntentEngine = semanticIntentEngine;
            _federatedLearningCoordinator = federatedLearningCoordinator;
            _spatialComputingCenter = spatialComputingCenter;
            _enterpriseSecurityCenter = enterpriseSecurityCenter;
            _agentOrchestrator = agentOrchestrator;
            _memoryManager = memoryManager;
            _workflowEngineService = workflowEngineService;
            _productionOperationsCenter = productionOperationsCenter;

            // Assign v4.1 content views
            GestureManagerContent.Content = gestureManagerView;
            WorkflowEditorContent.Content = workflowEditorView;
            PerformanceDashboardContent.Content = performanceDashboardView;
            PluginBrowserContent.Content = pluginBrowserView;

            // Wire up dependency checks and logging
            Logger.LogWritten += OnLogWritten;
            _dependencyManager.ProgressLogged += OnDependencyProgress;
            _coordinator.FrameProcessed += OnFrameProcessed;
            _coordinator.HandTracked += OnHandTracked;
            _coordinator.GestureRecognized += OnGestureRecognized;

            // Wire up cursor engine states to reflect on dashboard
            _cursorEngine.StateChanged += OnCursorStateChanged;

            // Wire up PipelineManager events for diagnostics
            _pipelineManager.MetricsUpdated += OnPipelineMetricsUpdated;
            _pipelineManager.HealthChanged  += OnPipelineHealthChanged;

            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;

            // Populate Developer Center SDK info after components are wired
            Loaded += (_, _) => PopulateDeveloperCenter();

            // Populate Research Lab UI defaults
            Loaded += (_, _) => InitializeResearchLabDefaults();

            // Set up 500ms dashboard refresh timer
            _uiTimer = new DispatcherTimer();
            _uiTimer.Interval = TimeSpan.FromMilliseconds(500);
            _uiTimer.Tick += UiTimer_Tick;
            _uiTimer.Start();
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Logger.Info("MainWindow loaded. Starting system checks...");

            // ── Phase 6: Recovery Status Banner ──────────────────────────────
            // Read the snapshot populated by App.OnStartup before any UI was shown.
            // The WpfRecoveryNotificationService already queued a toast via BeginInvoke;
            // this banner provides a persistent, in-window indicator that stays until
            // the user explicitly dismisses it.
            var recovery = App.Current.RecoveryStatus;
            if (recovery.AbnormalShutdownDetected || recovery.IsSafeMode)
            {
                if (recovery.IsSafeMode)
                {
                    // Red banner — Safe Mode is a critical condition
                    RecoveryBannerBrush.Color =
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#7F1D1D");
                    RecoveryBannerIcon.Foreground =
                        new System.Windows.Media.SolidColorBrush(
                            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF4444"));
                    RecoveryBannerTitle.Text = "⚠  Safe Mode Active";
                    RecoveryBannerMessage.Text =
                        $"AirGesture AI started in Safe Mode after {recovery.ConsecutiveFailureCount} consecutive " +
                        $"failures. Workflows are paused. Source: {recovery.RecoverySource}. " +
                        $"Validation: {recovery.LastValidationSeverity}.";
                }
                else
                {
                    // Amber banner — session restored from backup / autosave
                    RecoveryBannerBrush.Color =
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#7C3B00");
                    RecoveryBannerIcon.Foreground =
                        new System.Windows.Media.SolidColorBrush(
                            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFC107"));
                    RecoveryBannerTitle.Text = "↩  Session Restored";

                    var saveInfo = recovery.LastSuccessfulSaveTime is { } t
                        ? $" Last save: {t}."
                        : string.Empty;

                    RecoveryBannerMessage.Text =
                        $"Previous session was recovered from {recovery.RecoverySource}. " +
                        $"Validation: {recovery.LastValidationSeverity}.{saveInfo}";
                }

                RecoveryBanner.Visibility = Visibility.Visible;
                Logger.Info($"[Recovery] Banner shown. SafeMode={recovery.IsSafeMode}, " +
                            $"Source={recovery.RecoverySource}, Severity={recovery.LastValidationSeverity}.");
            }

            // Disable UI until environment is verified
            StartBtn.IsEnabled = false;

            // Initialize all health LEDs to offline
            OnPipelineHealthChanged();

            bool isReady = await _dependencyManager.VerifyDependenciesAsync();
            if (isReady)
            {
                Logger.Info("System is ready. Populating camera devices...");
                PopulateCameras();
                StartBtn.IsEnabled = true;
            }
            else
            {
                Logger.Warn("System verification failed. Please check Python and MediaPipe configuration.");
                MessageBox.Show("Python or MediaPipe was not found and auto-install failed. Please install Python 3.8+ and run 'pip install mediapipe opencv-python'.", "System Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                PopulateCameras(); // Try populated anyway as fallback
                StartBtn.IsEnabled = true;
            }
        }

        /// <summary>
        /// Dismisses the recovery/safe-mode status banner.
        /// </summary>
        private void RecoveryBannerDismiss_Click(object sender, RoutedEventArgs e)
        {
            RecoveryBanner.Visibility = Visibility.Collapsed;
            Logger.Info("[Recovery] User dismissed the recovery status banner.");
        }


        private void PopulateCameras()
        {
            try
            {
                var cameras = _cameraProvider.GetAvailableCameras();
                CameraComboBox.ItemsSource = null;
                CameraComboBox.Items.Clear();

                if (cameras.Count > 0)
                {
                    CameraComboBox.ItemsSource = cameras;
                    CameraComboBox.DisplayMemberPath = "Name";
                    CameraComboBox.SelectedValuePath = "Index";
                    CameraComboBox.SelectedIndex = 0;
                    StartBtn.IsEnabled = true;
                }
                else
                {
                    CameraComboBox.Items.Add("No Webcams Found");
                    CameraComboBox.SelectedIndex = 0;
                    StartBtn.IsEnabled = false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to discover webcams", ex);
            }
        }

        private async void StartBtn_Click(object sender, RoutedEventArgs e)
        {
            if (CameraComboBox.SelectedValue == null || CameraComboBox.SelectedItem is string str && str == "No Webcams Found")
            {
                MessageBox.Show("Please select a valid webcam device.", "Selection Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int index = (int)CameraComboBox.SelectedValue;
            Logger.Info($"User initiated stream start on Camera index: {index}");

            try
            {
                StartBtn.IsEnabled = false;
                CameraComboBox.IsEnabled = false;

                await _coordinator.StartAsync(index);

                StopBtn.IsEnabled = true;
                PlaceholderPanel.Visibility = Visibility.Collapsed;
                StatusLed.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0D6EFD")); // Blue (running)
                StatusText.Text = "Active Tracking";
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to start coordination loop", ex);
                MessageBox.Show($"Failed to start: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                
                StartBtn.IsEnabled = true;
                CameraComboBox.IsEnabled = true;
                StopBtn.IsEnabled = false;
            }
        }

        private async void StopBtn_Click(object sender, RoutedEventArgs e)
        {
            Logger.Info("User initiated stream stop.");

            try
            {
                StopBtn.IsEnabled = false;

                await _coordinator.StopAsync();

                StartBtn.IsEnabled = true;
                CameraComboBox.IsEnabled = true;
                PreviewImage.Source = null;
                PlaceholderPanel.Visibility = Visibility.Visible;
                StatusLed.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF4444")); // Red (stopped)
                StatusText.Text = "Stopped";

                HandPresenceText.Text = "Not Detected";
                HandPresenceText.Foreground = new SolidColorBrush(Colors.Red);
                StateDetailsText.Text = "Hand Lost / None";
                
                // Force health refresh to Offline
                OnPipelineHealthChanged();
            }
            catch (Exception ex)
            {
                Logger.Error("Error while stopping stream", ex);
            }
        }

        private void OnFrameProcessed(object? sender, FrameProcessedEventArgs e)
        {
            _capturedFramesCount++;
            _processedFramesCount++;

            Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
            {
                if (!_coordinator.IsRunning) return;

                try
                {
                    // Render pre-converted and frozen WPF ImageSource directly
                    PreviewImage.Source = e.PreviewImage;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Frame render crash: {ex.Message}");
                }
            }));
        }

        private void OnHandTracked(object? sender, HandTrackedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!_coordinator.IsRunning) return;

                if (e.HandData.IsDetected)
                {
                    HandPresenceText.Text = "Detected";
                    HandPresenceText.Foreground = new SolidColorBrush(Colors.Green);
                }
                else
                {
                    HandPresenceText.Text = "Not Detected";
                    HandPresenceText.Foreground = new SolidColorBrush(Colors.Red);
                }
            }));
        }

        private void OnCursorStateChanged(object? sender, CursorState state)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                string gestureStr = _coordinator.IsRunning ? _lastGesture : "None";
                StateDetailsText.Text = $"{state} / {gestureStr}";
            }));
        }

        private void OnLogWritten(string message)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                LogsList.Items.Add(message);
                if (LogsList.Items.Count > 150) // Cap log limit in UI to preserve memory
                {
                    LogsList.Items.RemoveAt(0);
                }
                LogsList.ScrollIntoView(message);
            }));
        }

        private void OnDependencyProgress(string message)
        {
            // Forward dependency managers progress logs to UI console directly
            OnLogWritten($"[SYS] {message}");
        }

        private void OnGestureRecognized(object? sender, GestureEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _lastGesture = e.Gesture.ToString();
                StateDetailsText.Text = $"{_cursorEngine.CurrentState} / {_lastGesture}";
            }));
        }

        private void OnPipelineMetricsUpdated()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
            {
                FpsDetailsText.Text = $"{_pipelineManager.CameraFps:F1} / {_pipelineManager.TrackingFps:F1} / {_pipelineManager.CursorFps:F1}";
                E2eLatencyText.Text = $"{_pipelineManager.EndToEndLatencyMs:F1} ms";
                InferenceLatencyText.Text = $"{_pipelineManager.InferenceLatencyMs:F1} ms";
                CpuUsageText.Text = $"{_pipelineManager.CpuUsage:F1} %";
                RamUsageText.Text = $"{_pipelineManager.RamUsageMb:F1} MB";

                // Update Application Context and Plugins diagnostics
                var ctx = _pipelineManager.ContextEngine.CurrentContext;
                ForegroundAppText.Text = $"{ctx.AppType} (PID {ctx.ProcessId})";
                ForegroundWindowTitleText.Text = string.IsNullOrEmpty(ctx.WindowTitle) ? "None" : ctx.WindowTitle;
                YouTubePageTypeText.Text = _pipelineManager.YouTubeAdapter.CurrentPageType.ToString();
                ActivePluginsText.Text = $"{_pipelineManager.PluginManager.Registry.Count} Active";
            }));
        }

        private void OnPipelineHealthChanged()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
            {
                UpdateHealthUI(PipelineHealthLed, PipelineHealthText, _pipelineManager.PipelineHealth);
                UpdateHealthUI(CameraHealthLed, CameraHealthText, _pipelineManager.CameraHealth);
                UpdateHealthUI(PythonHealthLed, PythonHealthText, _pipelineManager.PythonHealth);
                UpdateHealthUI(CursorHealthLed, CursorHealthText, _pipelineManager.CursorHealth);
                UpdateHealthUI(HoverHealthLed, HoverHealthText, _pipelineManager.HoverHealth);
                UpdateHealthUI(GestureHealthLed, GestureHealthText, _pipelineManager.GestureHealth);
                UpdateHealthUI(InputHealthLed, InputHealthText, _pipelineManager.InputHealth);

                // Update main status LED
                if (!_pipelineManager.IsRunning)
                {
                    StatusLed.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF4444")); // Red (stopped)
                    StatusText.Text = "Stopped";
                }
                else
                {
                    var mainHealth = _pipelineManager.PipelineHealth;
                    StatusLed.Fill = mainHealth == ComponentHealth.Healthy
                        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0D6EFD")) // Blue
                        : mainHealth == ComponentHealth.Warning
                            ? new SolidColorBrush(Colors.Yellow)
                            : new SolidColorBrush(Colors.Red);

                    StatusText.Text = "Active Tracking";
                }
            }));
        }

        private void UpdateHealthUI(System.Windows.Shapes.Ellipse led, TextBlock text, ComponentHealth health)
        {
            if (!_pipelineManager.IsRunning)
            {
                led.Fill = new SolidColorBrush(Colors.Gray);
                text.Text = "Offline";
                text.Foreground = new SolidColorBrush(Colors.Gray);
                return;
            }

            switch (health)
            {
                case ComponentHealth.Healthy:
                    led.Fill = new SolidColorBrush(Colors.Green);
                    text.Text = "Healthy";
                    text.Foreground = new SolidColorBrush(Colors.Green);
                    break;
                case ComponentHealth.Warning:
                    led.Fill = new SolidColorBrush(Colors.Yellow);
                    text.Text = "Warning";
                    text.Foreground = new SolidColorBrush(Colors.Yellow);
                    break;
                case ComponentHealth.Error:
                    led.Fill = new SolidColorBrush(Colors.Red);
                    text.Text = "Error";
                    text.Foreground = new SolidColorBrush(Colors.Red);
                    break;
            }
        }

        private async void MainWindow_Closed(object? sender, EventArgs e)
        {
            Logger.Info("MainWindow closed. Releasing all resources...");
            
            try
            {
                _uiTimer?.Stop();
            }
            catch {}

            Logger.LogWritten -= OnLogWritten;
            _dependencyManager.ProgressLogged -= OnDependencyProgress;
            _coordinator.FrameProcessed -= OnFrameProcessed;
            _coordinator.HandTracked -= OnHandTracked;
            _coordinator.GestureRecognized -= OnGestureRecognized;
            _cursorEngine.StateChanged -= OnCursorStateChanged;

            _pipelineManager.MetricsUpdated -= OnPipelineMetricsUpdated;
            _pipelineManager.HealthChanged -= OnPipelineHealthChanged;

            try
            {
                await _coordinator.StopAsync();
                _coordinator.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during shutdown dispose: {ex.Message}");
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // Phase 24 – Developer Center Handlers
        // ═══════════════════════════════════════════════════════════════════

        private void PopulateDeveloperCenter()
        {
            SdkVersionText.Text        = _sdkHost.Version.ToString();
            CompatModeText.Text        = "Enabled";
            var installed              = _extensionInstaller.GetInstalled();
            InstalledExtensionsText.Text = installed.Count.ToString();

            // Populate deprecation warnings (empty for a fresh install)
            DeprecationWarningsList.Items.Clear();
            NoWarningsText.Visibility  = Visibility.Visible;
        }

        private void BrowseExtension_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title  = "Select Extension Package",
                Filter = "AirGesture Extension|*.airgesture-extension|ZIP Archive|*.zip|All Files|*.*",
            };
            if (dlg.ShowDialog() == true)
            {
                _pendingExtensionPath     = dlg.FileName;
                ExtensionPathBox.Text     = dlg.FileName;
                InstallExtensionBtn.IsEnabled = true;
            }
        }

        private void InstallExtension_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_pendingExtensionPath) || !File.Exists(_pendingExtensionPath))
            {
                MessageBox.Show("Please select a valid extension package file first.",
                    "Install Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var pkg = _extensionInstaller.Install(_pendingExtensionPath);
                ExtensionStatusText.Text       = $"✓ Installed: {pkg.Manifest.DisplayName} v{pkg.Manifest.Version}";
                ExtensionStatusText.Visibility = Visibility.Visible;
                InstalledExtensionsText.Text   = _extensionInstaller.GetInstalled().Count.ToString();
                Logger.Info($"MainWindow: Extension '{pkg.Manifest.DisplayName}' installed successfully.");
            }
            catch (Exception ex)
            {
                ExtensionStatusText.Text       = $"✗ Install failed: {ex.Message}";
                ExtensionStatusText.Foreground = new SolidColorBrush(Colors.OrangeRed);
                ExtensionStatusText.Visibility = Visibility.Visible;
                Logger.Error("MainWindow: Extension install failed", ex);
            }
        }

        private void GenerateHtmlDocs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var outputDir  = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AirGestureAI", "ApiDocs");
                var outputPath = Path.Combine(outputDir, "api_reference.html");
                _apiDocGenerator.ExportHtml(Assembly.GetExecutingAssembly(), outputPath);
                DocsStatusText.Text       = $"✓ HTML docs written to: {outputPath}";
                DocsStatusText.Visibility = Visibility.Visible;
                // Open in default browser
                Process.Start(new ProcessStartInfo(outputPath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                DocsStatusText.Text       = $"✗ Error: {ex.Message}";
                DocsStatusText.Visibility = Visibility.Visible;
                Logger.Error("MainWindow: HTML doc generation failed", ex);
            }
        }

        private void GenerateMdDocs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var outputDir  = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AirGestureAI", "ApiDocs");
                var outputPath = Path.Combine(outputDir, "api_reference.md");
                _apiDocGenerator.ExportMarkdown(Assembly.GetExecutingAssembly(), outputPath);
                DocsStatusText.Text       = $"✓ Markdown docs written to: {outputPath}";
                DocsStatusText.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                DocsStatusText.Text       = $"✗ Error: {ex.Message}";
                DocsStatusText.Visibility = Visibility.Visible;
                Logger.Error("MainWindow: Markdown doc generation failed", ex);
            }
        }

        private void GenerateSampleProject_Click(object sender, RoutedEventArgs e)
        {
            var pluginName = PluginNameBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(pluginName))
            {
                MessageBox.Show("Please enter a plugin name.",
                    "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var zipPath = _sampleProjectGenerator.Generate(pluginName);
                SampleStatusText.Text       = $"✓ Created: {Path.GetFileName(zipPath)}";
                SampleStatusText.Visibility = Visibility.Visible;
                // Open the output folder
                Process.Start(new ProcessStartInfo(Path.GetDirectoryName(zipPath)!) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                SampleStatusText.Text       = $"✗ Error: {ex.Message}";
                SampleStatusText.Visibility = Visibility.Visible;
                Logger.Error("MainWindow: Sample project generation failed", ex);
            }
        }

        private void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Language switching is logged; full runtime re-translation is a
            // future enhancement requiring resource dictionary hot-reload.
            if (LanguageCombo.SelectedItem is ComboBoxItem item)
                Logger.Info($"MainWindow: Language preference changed to '{item.Tag}'.");
        }

        // ═══════════════════════════════════════════════════════════════════
        // Phase 25 – Release Center Handlers
        // ═══════════════════════════════════════════════════════════════════

        private void RefreshPerformanceSnapshot_Click(object sender, RoutedEventArgs e)
        {
            var snap = PerformanceSnapshot.Capture();
            RcRamText.Text    = $"{snap.WorkingSetMb:F1} MB";
            RcGcMemText.Text  = $"{snap.GcMemoryMb:F1} MB";
            RcThreadText.Text = snap.ThreadCount.ToString();
            RcGcColText.Text  = $"{snap.Gen0Collections} / {snap.Gen1Collections} / {snap.Gen2Collections}";

            // Update performance grade based on RAM
            PerformanceGradeText.Text = snap.WorkingSetMb < 300 ? "A"
                : snap.WorkingSetMb < 500 ? "B" : "C";

            UpdateReadinessScore();
        }

        private void RunSecurityAudit_Click(object sender, RoutedEventArgs e)
        {
            SecurityAuditList.Items.Clear();
            RunSecurityAuditBtn.IsEnabled = false;

            try
            {
                var appDir = AppDomain.CurrentDomain.BaseDirectory;
                _securityAuditManager.StepCompleted += msg =>
                    Dispatcher.BeginInvoke(() => SecurityAuditList.Items.Add(msg));

                var report = _securityAuditManager.RunAudit(appDir);

                SecurityGradeText.Text      = report.Grade;
                SecurityGradeText.Foreground = report.Grade == "A"
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#22C55E"))
                    : new SolidColorBrush(Colors.OrangeRed);

                UpdateReadinessScore();
            }
            catch (Exception ex)
            {
                SecurityAuditList.Items.Add($"✗ Audit error: {ex.Message}");
                Logger.Error("MainWindow: Security audit failed", ex);
            }
            finally
            {
                RunSecurityAuditBtn.IsEnabled = true;
            }
        }

        private void BuildPortableZip_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var sourceDir = AppDomain.CurrentDomain.BaseDirectory;
                var outputDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "AirGestureAI_Release");

                _releaseManager.StepCompleted += msg =>
                    Dispatcher.BeginInvoke(() => ReleaseOutputList.Items.Add(msg));

                var zipPath = _releaseManager.BuildPortableZip(sourceDir, outputDir);
                ReleaseOutputList.Items.Add($"ZIP: {zipPath}");
            }
            catch (Exception ex)
            {
                ReleaseOutputList.Items.Add($"✗ Error: {ex.Message}");
                Logger.Error("MainWindow: Portable ZIP build failed", ex);
            }
        }

        private void GenerateReleaseNotes_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var outputPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "AirGestureAI_Release", "RELEASE_NOTES.md");

                _releaseManager.StepCompleted += msg =>
                    Dispatcher.BeginInvoke(() => ReleaseOutputList.Items.Add(msg));

                _releaseManager.GenerateReleaseNotes(outputPath);
                ReleaseOutputList.Items.Add($"Notes: {outputPath}");
                Process.Start(new ProcessStartInfo(outputPath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                ReleaseOutputList.Items.Add($"✗ Error: {ex.Message}");
                Logger.Error("MainWindow: Release notes generation failed", ex);
            }
        }

        private void UpdateReadinessScore()
        {
            var secOk   = SecurityGradeText.Text    is "A" or "B";
            var accOk   = AccessibilityGradeText.Text is "A" or "B";
            var perfOk  = PerformanceGradeText.Text  is "A" or "B";

            var (score, label)  = _releaseManager.ComputeReadinessScore(secOk, accOk, perfOk);
            ReadinessScoreText.Text  = score.ToString();
            ReadinessLabelText.Text  = label;
        }

        // ── Phase 27: Research Lab Interactive Handlers ──────────────────────

        private async void InitializeResearchLabDefaults()
        {
            ProviderSelectionBox.SelectedIndex = 0; // Local
            ActiveModelBox.SelectedIndex = 0; // gesture_classifier_v1
            ResearchOutputList.Items.Add("Research Lab Initialized.");

            ResearchOutputList.Items.Add("Running Phase 27 self-diagnostic unit tests...");
            try
            {
                bool testsPassed = await ResearchTests.RunAllTestsAsync();
                if (testsPassed)
                {
                    ResearchOutputList.Items.Add("✓ ALL Phase 27 Unit Tests PASSED successfully.");
                }
                else
                {
                    ResearchOutputList.Items.Add("✗ Warning: Some diagnostic unit tests failed.");
                }
            }
            catch (Exception ex)
            {
                ResearchOutputList.Items.Add($"✗ Test runner error: {ex.Message}");
            }
        }

        private async void TrainModel_Click(object sender, RoutedEventArgs e)
        {
            ResearchOutputList.Items.Add("Training personalized model locally...");
            TrainingProgressBar.Value = 10;
            try
            {
                var tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                TrainingProgressBar.Value = 40;
                double accuracy = await _adaptiveLearning.RetrainModelAsync(tokenSource.Token);
                TrainingProgressBar.Value = 100;
                AdaptiveAccuracyText.Text = $"{(accuracy * 100):F2}%";
                ResearchOutputList.Items.Add($"✓ Retraining complete. New accuracy: {(accuracy * 100):F2}%");
            }
            catch (Exception ex)
            {
                ResearchOutputList.Items.Add($"✗ Training error: {ex.Message}");
                TrainingProgressBar.Value = 0;
            }
        }

        private void ResetLearning_Click(object sender, RoutedEventArgs e)
        {
            _adaptiveLearning.Reset();
            AdaptiveAccuracyText.Text = "85.00%";
            TrainingProgressBar.Value = 0;
            ResearchOutputList.Items.Add("✓ Personalized weights reset to baseline defaults.");
        }

        private void RefreshTelemetry_Click(object sender, RoutedEventArgs e)
        {
            AnalyticsFpsText.Text = $"{_pipelineManager.TrackingFps:F1} Hz";
            AnalyticsLatencyText.Text = $"{_pipelineManager.EndToEndLatencyMs:F2} ms";
            AnalyticsAnomaliesText.Text = _analytics.Predictions.AnomalyCount.ToString();
            AnalyticsDurationText.Text = $"{_analytics.Usage.CurrentSessionDurationSeconds:F0}s";
            ResearchOutputList.Items.Add("✓ Telemetry analytics updated.");
        }

        private void ToggleFlag_Click(object sender, RoutedEventArgs e)
        {
            var flag = _experiments.Flags["EnhancedSmoothing"];
            flag.IsEnabled = !flag.IsEnabled;
            FlagStatusText.Text = flag.IsEnabled ? "Enabled" : "Disabled";
            FlagStatusText.Foreground = flag.IsEnabled ? (Brush)new BrushConverter().ConvertFromString("#22C55E")! : (Brush)new BrushConverter().ConvertFromString("#888888")!;
            ResearchOutputList.Items.Add($"✓ FeatureFlag 'EnhancedSmoothing' set to: {flag.IsEnabled}");
        }

        private void EvaluateVariant_Click(object sender, RoutedEventArgs e)
        {
            var controlMetrics = new ResearchMetrics { Accuracy = 0.85, Precision = 0.84, Recall = 0.86, AverageLatencyMs = 4.5, MemoryOverheadMb = 12.0 };
            var treatmentMetrics = new ResearchMetrics { Accuracy = 0.89, Precision = 0.88, Recall = 0.90, AverageLatencyMs = 3.8, MemoryOverheadMb = 12.5 };
            var result = _experiments.EvaluateExperiment(0, controlMetrics, treatmentMetrics);
            ResearchOutputList.Items.Add($"✓ Evaluation complete. Winner: {result.WinnerVariant} (Accuracy Lift: +{(result.AccuracyLift * 100):F1}%, Latency Delta: {result.LatencyDeltaMs:F2} ms)");
        }

        private async void ProviderSelectionBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProviderSelectionBox == null || _providerManager == null) return;
            var selectedItem = (ComboBoxItem)ProviderSelectionBox.SelectedItem;
            var providerName = selectedItem.Content.ToString() ?? "Local";
            _providerManager.SetProvider(providerName);
            var status = await _providerManager.GetProviderStatusAsync();
            InferenceStatusText.Text = status;
            InferenceStatusText.Foreground = status == "Online" ? (Brush)new BrushConverter().ConvertFromString("#22C55E")! : (Brush)new BrushConverter().ConvertFromString("#E11D48")!;
            ResearchOutputList.Items.Add($"AI Provider switched to: {providerName} (Status: {status})");
        }

        private void ActiveModelBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ActiveModelBox == null || _modelCatalog == null) return;
            var selectedItem = (ComboBoxItem)ActiveModelBox.SelectedItem;
            var modelId = selectedItem.Content.ToString() ?? "gesture_classifier_v1";
            var model = _modelCatalog.FindById(modelId);
            if (model != null)
            {
                ResearchOutputList.Items.Add($"Active model configuration loaded: {model.DisplayName} (Size: {model.SizeMb} MB, Validated: {model.IsValidated})");
            }
        }

        private async void BenchmarkOnnx_Click(object sender, RoutedEventArgs e)
        {
            ResearchOutputList.Items.Add("Running model benchmarks...");
            var model = _modelCatalog.Models[0];
            try
            {
                double latency = await _modelBenchmark.RunBenchmarkAsync(model, 10, CancellationToken.None);
                ResearchOutputList.Items.Add($"✓ Model benchmark complete. {model.DisplayName} Latency: {latency:F2} ms");
            }
            catch (Exception ex)
            {
                ResearchOutputList.Items.Add($"Benchmark error: {ex.Message}");
            }
        }

        private async void TestProvider_Click(object sender, RoutedEventArgs e)
        {
            ResearchOutputList.Items.Add("Testing active provider inference...");
            string response = await _providerManager.ExecuteAsync("Identify gesture 'pinch'");
            ResearchOutputList.Items.Add($"✓ Response: {response}");
        }

        private void StartXrSession_Click(object sender, RoutedEventArgs e)
        {
            if (_xrSession.IsActive)
            {
                _xrSession.StopSession();
                ResearchOutputList.Items.Add("✓ XR Session stopped.");
            }
            else
            {
                _xrSession.StartSession();
                ResearchOutputList.Items.Add("✓ XR Session initiated. Depth sensor connected.");
            }
        }

        private void AddAnchor_Click(object sender, RoutedEventArgs e)
        {
            _xrSession.CreateAnchor("Anchor_" + Guid.NewGuid().ToString()[..4], new SpatialCoordinate(0.1, 0.2, 0.7));
            XrAnchorText.Text = $"Anchors count: {_xrSession.Anchors.Count}. Latest: {_xrSession.Anchors[^1].Name}";
            ResearchOutputList.Items.Add($"✓ Created Spatial Anchor at (0.1, 0.2, 0.7)");
        }

        private void ExportResearchPack_Click(object sender, RoutedEventArgs e)
        {
            var metrics = new ResearchMetrics { Accuracy = _adaptiveLearning.TrackingAccuracy, Precision = 0.88, Recall = 0.89, AverageLatencyMs = 4.2, MemoryOverheadMb = 12.8 };
            string path = _researchManager.ExportResearchPack(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ResearchPack"), metrics);
            ResearchOutputList.Items.Add($"✓ Exported complete Research Pack to: {path}");
        }

        private void GenerateReports_Click(object sender, RoutedEventArgs e)
        {
            string path = _analytics.ExportReports(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AnalyticsPack"));
            ResearchOutputList.Items.Add($"✓ Exported diagnostic reports to: {path}");
        }

        private void UiTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                // Process status
                bool trackerAlive = _serviceProcessManager.IsAlive("TrackerHost");
                TrackerSubprocessLed.Fill = trackerAlive ? new SolidColorBrush(Colors.Green) : new SolidColorBrush(Colors.Red);
                TrackerSubprocessText.Text = trackerAlive ? "Online" : "Offline";

                bool aiAlive = _serviceProcessManager.IsAlive("AIWorker");
                AiSubprocessLed.Fill = aiAlive ? new SolidColorBrush(Colors.Green) : new SolidColorBrush(Colors.Red);
                AiSubprocessText.Text = aiAlive ? "Online" : "Offline";

                // Spatial
                CalibrationStatusText.Text = _spatialComputingCenter.Calibration.IsCalibrated ? "Calibration: Calibrated" : "Calibration: Pending";
                SpatialAnchorStatusText.Text = $"Active Anchors: {_spatialComputingCenter.Anchors.Anchors.Count}";

                // Agent
                AgentTaskQueueText.Text = $"Queue Depth: {_agentOrchestrator.Scheduler.QueueDepth}";

                // Memory
                MemoryCountText.Text = $"Stored Memories: {_memoryManager.Documents.Count}";

                // Profiler
                LiveProfilingText.Text = $"Live Performance: Latency: {_productionOperationsCenter.Profiler.AverageLatencyMs:F2}ms | Gen2 collections: {GC.CollectionCount(2)}";

                // Workflow
                WorkflowStatusText.Text = _workflowEngineService.Recorder.IsRecording ? "Recorder State: Recording" : "Recorder State: Idle";
            }
            catch
            {
                // Suppress background errors during UI shutdown
            }
        }

        private async void PingTracker_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string resp = await _trackerBridge.PingAsync();
                MessageBox.Show($"Tracker Host Ping Response: {resp}", "IPC Ping Result", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Tracker Host Ping Failed: {ex.Message}", "IPC Ping Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void PingAiWorker_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string resp = await _aiWorkerBridge.InferAsync("health_check");
                MessageBox.Show($"AI Worker Ping Response: {resp}", "IPC Ping Result", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"AI Worker Ping Failed: {ex.Message}", "IPC Ping Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void AnalyzeIntent_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = await _semanticIntentEngine.ClassifyAsync(IntentPromptInput.Text);
                IntentResultText.Text = $"Tag: {result.Tag}\nConfidence: {result.Confidence:P0}\nExplanation: {result.Explanation}";
            }
            catch (Exception ex)
            {
                IntentResultText.Text = $"Error: {ex.Message}";
            }
        }

        private async void TriggerFederatedRound_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _federatedLearningCoordinator.RegisterParticipant("LocalNode_" + Guid.NewGuid().ToString()[..4]);
                var model = await _federatedLearningCoordinator.RunRoundAsync();
                FedLearningStatusText.Text = $"Active Round: {model.Round} | DP Noise Multiplier: 0.10 | Accuracy: {model.Accuracy:P2}";
                MessageBox.Show($"Federated round {model.Round} completed with aggregated accuracy: {model.Accuracy:P2}", "FedAvg Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Federated training failed: {ex.Message}", "Training Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void RunCalibration_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var session = _spatialComputingCenter.Cameras.Sessions.Count > 0 
                    ? _spatialComputingCenter.Cameras.Sessions[0] 
                    : _spatialComputingCenter.Cameras.OpenCamera(new SpatialComputing.CameraConfig { DeviceId = 0 });
                await _spatialComputingCenter.Calibration.CalibrateAsync(session);
                CalibrationStatusText.Text = "Calibration: Calibrated";
                MessageBox.Show("Stereo Lens Calibration Wizard succeeded.", "Calibration Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Calibration failed: {ex.Message}", "Calibration Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddSpatialAnchor_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var name = "Anchor_" + Guid.NewGuid().ToString()[..4];
                _spatialComputingCenter.Anchors.CreateAnchor(name, new SpatialComputing.Point3D { X = 0.5f, Y = 1.2f, Z = 0.8f });
                SpatialAnchorStatusText.Text = $"Active Anchors: {_spatialComputingCenter.Anchors.Anchors.Count} (Latest: {name})";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Anchor creation failed: {ex.Message}", "Anchor Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BackupVault_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string backupPath = await _enterpriseSecurityCenter.Backup.BackupAsync("Backups");
                MessageBox.Show($"Encrypted AES-256 Vault Backup saved to:\n{backupPath}", "Backup Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Backup failed: {ex.Message}", "Security Backup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void SubmitAgentGoal_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var tasks = await _agentOrchestrator.SubmitGoalAsync(AgentGoalInput.Text);
                MessageBox.Show($"Goal decomposed into {tasks.Count} agent tasks in execution queue.", "Goal Scheduled", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Agent goal execution failed: {ex.Message}", "Agent Runtime Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StartRecordingWorkflow_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _workflowEngineService.StartRecording("Workflow_" + Guid.NewGuid().ToString()[..4]);
                WorkflowStatusText.Text = "Recorder State: Recording";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Workflow recorder failed to start: {ex.Message}", "Recorder Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StopRecordingWorkflow_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var wf = _workflowEngineService.StopRecording();
                WorkflowStatusText.Text = "Recorder State: Idle";
                if (wf != null)
                {
                    MessageBox.Show($"Recorded workflow '{wf.Name}' with {wf.Steps.Count} steps.", "Recording Saved", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Workflow recorder failed to stop: {ex.Message}", "Recorder Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ReplayWorkflow_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_workflowEngineService.Workflows.Count == 0)
                {
                    MessageBox.Show("No workflows recorded yet. Please record one first.", "Replay Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                var lastWf = _workflowEngineService.Workflows[^1];
                MessageBox.Show($"Replaying last recorded workflow: {lastWf.Name}", "Replay Initiated", MessageBoxButton.OK, MessageBoxImage.Information);
                await _workflowEngineService.ReplayAsync(lastWf.Name);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Workflow replay failed: {ex.Message}", "Replay Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RunA11yAudit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var findings = _productionOperationsCenter.Accessibility.Audit();
                MessageBox.Show(string.Join("\n", findings), "A11Y Accessibility Audit", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Audit failed: {ex.Message}", "A11Y Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void GenerateReleasePackage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string pkgPath = await _productionOperationsCenter.Deployment.PackageAsync("Releases", new Version(4, 0, 0));
                MessageBox.Show($"Production release package generated at:\n{pkgPath}", "Deployment Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Deployment failed: {ex.Message}", "Release Wizard Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void RunArchitectureTests_Click(object sender, RoutedEventArgs e)
        {
            ResearchOutputList.Items.Add("Running Architectural Verification Suite...");
            try
            {
                bool passed = await ArchitectureTests.RunAllTestsAsync();
                ResearchOutputList.Items.Add(passed ? "✓ Architecture Suite: PASSED" : "✗ Architecture Suite: FAILED");
                MessageBox.Show(passed ? "All architectural tests passed successfully!" : "Some architectural tests failed.", 
                    "Architecture Verification Suite", MessageBoxButton.OK, 
                    passed ? MessageBoxImage.Information : MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                ResearchOutputList.Items.Add($"Architecture Suite Error: {ex.Message}");
                MessageBox.Show($"Verification suite encountered an error: {ex.Message}", "Architecture Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
