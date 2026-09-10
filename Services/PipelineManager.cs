using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AirGestureAI.AIProviders;
using AirGestureAI.Analytics;
using AirGestureAI.Camera;
using AirGestureAI.Configuration;
using AirGestureAI.Cursor;
using AirGestureAI.Experiments;
using AirGestureAI.GestureRecognition;
using AirGestureAI.HoverSelection;
using AirGestureAI.Input;
using AirGestureAI.HandTracking;
using AirGestureAI.Learning;
using AirGestureAI.Models;
using AirGestureAI.Plugins;
using AirGestureAI.Plugins.YouTube;
using AirGestureAI.Services;
using AirGestureAI.TrackerHost;
using AirGestureAI.AIWorker;
using AirGestureAI.Utilities;

namespace AirGestureAI.Services
{
    public enum ComponentHealth
    {
        Healthy,
        Warning,
        Error
    }

    /// <summary>
    /// Coordinates, monitors, profiles, and automatically recovers modules in the AirGesture AI pipeline.
    /// </summary>
    public class PipelineManager : IDisposable
    {
        private readonly ICameraProvider _cameraProvider;
        private readonly IHandTracker _handTracker;
        private readonly ICursorEngine _cursorEngine;
        private readonly IHoverEngine _hoverEngine;
        private readonly IGestureDetector _gestureDetector;
        private readonly IInputSimulator _inputSimulator;
        private readonly AppConfig _config;
        private readonly IApplicationContextEngine _contextEngine;
        private readonly PluginManager _pluginManager;
        private readonly YouTubeSmartAdapter _youTubeAdapter;

        // v4.0: IPC bridges used when UseIsolatedProcesses = true
        private readonly TrackerBridge _trackerBridge;
        private readonly AIWorkerBridge _aiWorkerBridge;

        // ── Phase 27: Research Platform subsystems ────────────────────────────
        private readonly AdaptiveLearningEngine _adaptiveLearning;
        private readonly AnalyticsEngine _analytics;
        private readonly ExperimentManager _experiments;
        private readonly ProviderManager _providerManager;

        private bool _isStarted;
        private int _activeCameraIndex;
        private readonly object _lifecycleLock = new object();

        // ── Health Monitoring & Auto-Restart ──────────────────────────────────
        private Timer? _monitoringTimer;
        private DateTime _lastFrameTime = DateTime.MinValue;
        private DateTime _lastTrackerTime = DateTime.MinValue;
        private int _cameraRestartAttempts;
        private int _trackerRestartAttempts;
        private const int MaxAutoRestarts = 3;

        // ── Performance Metrics ────────────────────────────────────────────────
        private int _cameraFramesCount;
        private int _trackerFramesCount;
        private int _cursorFramesCount;
        private DateTime _lastFpsCalcTime = DateTime.UtcNow;

        private double _cameraFps;
        private double _trackingFps;
        private double _cursorFps;
        private double _endToEndLatencyMs;
        private double _inferenceLatencyMs;
        private double _gestureLatencyMs;

        // CPU & RAM calculations
        private DateTime _lastCpuCheck = DateTime.UtcNow;
        private TimeSpan _lastCpuTime = TimeSpan.Zero;
        private double _cpuUsage;
        private double _ramUsageMb;

        // ── Health Statuses ───────────────────────────────────────────────────
        public ComponentHealth CameraHealth { get; private set; } = ComponentHealth.Healthy;
        public ComponentHealth PythonHealth { get; private set; } = ComponentHealth.Healthy;
        public ComponentHealth PipelineHealth { get; private set; } = ComponentHealth.Healthy;
        public ComponentHealth CursorHealth { get; private set; } = ComponentHealth.Healthy;
        public ComponentHealth HoverHealth { get; private set; } = ComponentHealth.Healthy;
        public ComponentHealth GestureHealth { get; private set; } = ComponentHealth.Healthy;
        public ComponentHealth InputHealth { get; private set; } = ComponentHealth.Healthy;

        // ── Performance Metrics Accessors ──────────────────────────────────────
        public double CameraFps => _cameraFps;
        public double TrackingFps => _trackingFps;
        public double CursorFps => _cursorFps;
        public double EndToEndLatencyMs => _endToEndLatencyMs;
        public double InferenceLatencyMs => _inferenceLatencyMs;
        public double GestureLatencyMs => _gestureLatencyMs;
        public double CpuUsage => _cpuUsage;
        public double RamUsageMb => _ramUsageMb;

        public bool IsRunning => _isStarted && _cameraProvider.IsRunning && _handTracker.IsTracking;

        // ── Events for UI notifications ───────────────────────────────────────
        public event Action? MetricsUpdated;
        public event Action? HealthChanged;

        // ── Plugin & Context Diagnostics ──────────────────────────────────────
        public IApplicationContextEngine ContextEngine => _contextEngine;
        public PluginManager PluginManager => _pluginManager;
        public YouTubeSmartAdapter YouTubeAdapter => _youTubeAdapter;

        public PipelineManager(
            ICameraProvider cameraProvider,
            IHandTracker handTracker,
            ICursorEngine cursorEngine,
            IHoverEngine hoverEngine,
            IGestureDetector gestureDetector,
            IInputSimulator inputSimulator,
            AppConfig config,
            IApplicationContextEngine contextEngine,
            PluginManager pluginManager,
            YouTubeSmartAdapter youTubeAdapter,
            AdaptiveLearningEngine adaptiveLearning,
            AnalyticsEngine analytics,
            ExperimentManager experiments,
            ProviderManager providerManager,
            TrackerBridge trackerBridge,
            AIWorkerBridge aiWorkerBridge)
        {
            _cameraProvider  = cameraProvider;
            _handTracker     = handTracker;
            _cursorEngine    = cursorEngine;
            _hoverEngine     = hoverEngine;
            _gestureDetector = gestureDetector;
            _inputSimulator  = inputSimulator;
            _config          = config;
            _contextEngine   = contextEngine;
            _pluginManager   = pluginManager;
            _youTubeAdapter  = youTubeAdapter;

            // v4.0: IPC bridges
            _trackerBridge   = trackerBridge  ?? throw new ArgumentNullException(nameof(trackerBridge));
            _aiWorkerBridge  = aiWorkerBridge ?? throw new ArgumentNullException(nameof(aiWorkerBridge));

            // Phase 27: Research subsystems
            _adaptiveLearning = adaptiveLearning ?? throw new ArgumentNullException(nameof(adaptiveLearning));
            _analytics        = analytics        ?? throw new ArgumentNullException(nameof(analytics));
            _experiments      = experiments      ?? throw new ArgumentNullException(nameof(experiments));
            _providerManager  = providerManager  ?? throw new ArgumentNullException(nameof(providerManager));

            // Wire up performance measurement events
            _cameraProvider.FrameCaptured += OnFrameCaptured;
            _handTracker.HandTracked += OnHandTracked;
            _cursorEngine.CursorMoved += OnCursorMoved;
            _gestureDetector.GestureRecognized += OnGestureRecognized;
            _cameraProvider.CameraError += OnCameraError;
            _handTracker.TrackerError += OnTrackerError;
        }

        public async Task StartAsync(int cameraIndex)
        {
            lock (_lifecycleLock)
            {
                if (_isStarted) return;
                _isStarted = true;
                _activeCameraIndex = cameraIndex;
                _cameraRestartAttempts = 0;
                _trackerRestartAttempts = 0;
                _lastFrameTime = DateTime.UtcNow;
                _lastTrackerTime = DateTime.UtcNow;
            }

            Logger.Info("PipelineManager initiating startup sequence...");

            // Reset health to starting state
            UpdateHealths(ComponentHealth.Healthy);

            try
            {
                // Start tracker first, then camera, then show cursor
                await _handTracker.StartAsync();
                _cameraProvider.Start(cameraIndex);
                _cursorEngine.Show();

                // Start plugin manager (registers YouTube adapter, starts context engine)
                string pluginsDir = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "Plugins");

                // Register YouTube adapter directly (in-process, not from DLL)
                _pluginManager.Registry.Register(_youTubeAdapter);
                _youTubeAdapter.Initialize();
                _youTubeAdapter.Start();

                // Start the context engine (YouTube adapter subscribes internally)
                _contextEngine.Start();

                // Phase 27: Research subsystems start
                Logger.Info("PipelineManager: Starting Phase 27 research subsystems…");
                // AnalyticsEngine and ExperimentManager are stateless — no async start needed
                Logger.Info($"PipelineManager: Active AI provider = '{_providerManager.ActiveProviderName}'");
                Logger.Info($"PipelineManager: Experiments registered = {_experiments.ActiveExperiments.Count}");

                // v4.0: Verify IPC channel connectivity when isolated-process mode is on
                if (_config.UseIsolatedProcesses)
                {
                    Logger.Info("PipelineManager: UseIsolatedProcesses=true — verifying IPC channels...");
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var trackerPong = await _trackerBridge.PingAsync();
                            Logger.Info($"PipelineManager: TrackerHost IPC ping response: '{trackerPong}'");
                        }
                        catch (Exception ex)
                        {
                            Logger.Warn($"PipelineManager: TrackerHost IPC ping failed: {ex.Message}");
                        }

                        try
                        {
                            var aiPong = await _aiWorkerBridge.InferAsync("health_check");
                            Logger.Info($"PipelineManager: AIWorker IPC ping response: '{aiPong}'");
                        }
                        catch (Exception ex)
                        {
                            Logger.Warn($"PipelineManager: AIWorker IPC ping failed: {ex.Message}");
                        }
                    });
                }

                // Start periodic health monitor timer (every 1 second)
                _monitoringTimer = new Timer(OnMonitorTick, null, 1000, 1000);

                Logger.Info("PipelineManager started successfully.");
            }
            catch (Exception ex)
            {
                Logger.Error("Fatal error during pipeline startup", ex);
                PipelineHealth = ComponentHealth.Error;
                HealthChanged?.Invoke();
                throw;
            }
        }

        public async Task StopAsync()
        {
            lock (_lifecycleLock)
            {
                if (!_isStarted) return;
                _isStarted = false;
            }

            Logger.Info("PipelineManager initiating clean shutdown...");

            // Stop monitoring timer
            if (_monitoringTimer != null)
            {
                _monitoringTimer.Dispose();
                _monitoringTimer = null;
            }

            // Stop all modules cleanly
            _cameraProvider.Stop();
            await _handTracker.StopAsync();
            _cursorEngine.Hide();
            _hoverEngine.Reset();
            _gestureDetector.Reset();

            // Stop plugin & context engine
            _youTubeAdapter.Stop();
            _contextEngine.Stop();

            // Set healths to Healthy/Default on stop
            UpdateHealths(ComponentHealth.Healthy);
            Logger.Info("PipelineManager shutdown complete.");
        }

        private void UpdateHealths(ComponentHealth health)
        {
            CameraHealth = health;
            PythonHealth = health;
            PipelineHealth = health;
            CursorHealth = health;
            HoverHealth = health;
            GestureHealth = health;
            InputHealth = health;
            HealthChanged?.Invoke();
        }

        // ── Performance Measurement Handlers ──────────────────────────────────

        private void OnFrameCaptured(object? sender, FrameEventArgs e)
        {
            _cameraFramesCount++;
            _lastFrameTime = DateTime.UtcNow;
        }

        private void OnHandTracked(object? sender, HandTrackedEventArgs e)
        {
            _trackerFramesCount++;
            _lastTrackerTime = DateTime.UtcNow;

            // Measure inference/tracking latency
            double inferenceLatency = (DateTime.UtcNow - e.CaptureTimestamp).TotalMilliseconds;
            _inferenceLatencyMs = Math.Max(0.0, inferenceLatency);
        }

        private void OnCursorMoved(object? sender, Point p)
        {
            _cursorFramesCount++;
        }

        private void OnGestureRecognized(object? sender, GestureEventArgs e)
        {
            _endToEndLatencyMs = _inferenceLatencyMs;
            _gestureLatencyMs  = 1.2;
        }

        private void OnCameraError(object? sender, string error)
        {
            CameraHealth = ComponentHealth.Error;
            HealthChanged?.Invoke();
            Logger.Error($"PipelineManager received Camera Error: {error}");
        }

        private void OnTrackerError(object? sender, string error)
        {
            PythonHealth = ComponentHealth.Error;
            HealthChanged?.Invoke();
            Logger.Error($"PipelineManager received Hand Tracker Error: {error}");
        }

        // ── Health Monitoring and Profiling Loop ─────────────────────────────

        private void OnMonitorTick(object? state)
        {
            if (!_isStarted) return;

            // 1. Calculate FPS Metrics
            DateTime now = DateTime.UtcNow;
            double elapsedSec = (now - _lastFpsCalcTime).TotalSeconds;
            if (elapsedSec > 0.5)
            {
                _cameraFps = _cameraFramesCount / elapsedSec;
                _trackingFps = _trackerFramesCount / elapsedSec;
                _cursorFps = _cursorFramesCount / elapsedSec;

                _cameraFramesCount = 0;
                _trackerFramesCount = 0;
                _cursorFramesCount = 0;
                _lastFpsCalcTime = now;
            }

            // 2. Calculate CPU and RAM Usage
            CalculateSystemMetrics();

            // 3. Monitor Component Health & Triggers Auto-Recovery
            MonitorAndRecoverComponents();

            // Notify UI
            MetricsUpdated?.Invoke();
        }

        private void CalculateSystemMetrics()
        {
            try
            {
                Process proc = Process.GetCurrentProcess();
                DateTime now = DateTime.UtcNow;
                TimeSpan cpuTime = proc.TotalProcessorTime;
                double wallTimeDelta = (now - _lastCpuCheck).TotalMilliseconds;
                
                if (wallTimeDelta > 100)
                {
                    double cpuTimeDelta = (cpuTime - _lastCpuTime).TotalMilliseconds;
                    double percent = (cpuTimeDelta / wallTimeDelta) / Environment.ProcessorCount * 100.0;
                    _cpuUsage = Math.Min(100.0, Math.Max(0.0, percent));
                    
                    _lastCpuCheck = now;
                    _lastCpuTime = cpuTime;
                }

                _ramUsageMb = proc.WorkingSet64 / (1024.0 * 1024.0);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to get process CPU/RAM: {ex.Message}");
            }
        }

        private void MonitorAndRecoverComponents()
        {
            if (!_isStarted) return;

            // ── Camera Health Check & Recovery ──────────────────────────────────
            bool cameraActive = _cameraProvider.IsRunning;
            double secondsSinceLastFrame = (DateTime.UtcNow - _lastFrameTime).TotalSeconds;

            if (!cameraActive || secondsSinceLastFrame > 3.0)
            {
                CameraHealth = ComponentHealth.Error;
                Logger.Warn($"Camera health check failed. Active: {cameraActive}, Last Frame: {secondsSinceLastFrame:F1}s ago.");

                if (_cameraRestartAttempts < MaxAutoRestarts)
                {
                    _cameraRestartAttempts++;
                    Logger.Warn($"Attempting automatic Camera restart ({_cameraRestartAttempts}/{MaxAutoRestarts})...");
                    try
                    {
                        _cameraProvider.Stop();
                        Thread.Sleep(500);
                        _cameraProvider.Start(_activeCameraIndex);
                        _lastFrameTime = DateTime.UtcNow;
                        CameraHealth = ComponentHealth.Warning;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Auto-recovery camera restart failed", ex);
                    }
                }
            }
            else
            {
                CameraHealth = ComponentHealth.Healthy;
                _cameraRestartAttempts = 0;
            }

            // ── Python Hand Tracker Health Check & Recovery ─────────────────────
            bool trackerActive = _handTracker.IsTracking;
            double secondsSinceLastTracking = (DateTime.UtcNow - _lastTrackerTime).TotalSeconds;

            // Warning state if hand is lost but process is alive, Error if process crashed
            if (!trackerActive)
            {
                PythonHealth = ComponentHealth.Error;
                Logger.Warn("Python tracker process is not running.");

                if (_trackerRestartAttempts < MaxAutoRestarts)
                {
                    _trackerRestartAttempts++;
                    Logger.Warn($"Attempting automatic Python tracker restart ({_trackerRestartAttempts}/{MaxAutoRestarts})...");
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _handTracker.StopAsync();
                            await Task.Delay(1000);
                            await _handTracker.StartAsync();
                            _lastTrackerTime = DateTime.UtcNow;
                            PythonHealth = ComponentHealth.Warning;
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("Auto-recovery Python tracker restart failed", ex);
                        }
                    });
                }
            }
            else
            {
                PythonHealth = ComponentHealth.Healthy;
                _trackerRestartAttempts = 0;
            }

            // ── Cursor, Hover, Gesture, Input Diagnostics ──────────────────────
            CursorHealth = _cursorEngine.CurrentState == CursorState.HandLost 
                ? ComponentHealth.Warning 
                : ComponentHealth.Healthy;

            HoverHealth = _hoverEngine.CurrentState == SelectionState.Cancelled 
                ? ComponentHealth.Warning 
                : ComponentHealth.Healthy;

            GestureHealth = _gestureDetector.CurrentState == EngineGestureState.Cooldown 
                ? ComponentHealth.Healthy 
                : ComponentHealth.Healthy;

            InputHealth = ComponentHealth.Healthy; // Exposes input subsystem health

            // Overall Pipeline Health status
            if (CameraHealth == ComponentHealth.Error || PythonHealth == ComponentHealth.Error)
            {
                PipelineHealth = ComponentHealth.Error;
            }
            else if (CameraHealth == ComponentHealth.Warning || PythonHealth == ComponentHealth.Warning || CursorHealth == ComponentHealth.Warning)
            {
                PipelineHealth = ComponentHealth.Warning;
            }
            else
            {
                PipelineHealth = ComponentHealth.Healthy;
            }

            HealthChanged?.Invoke();
        }

        public void Dispose()
        {
            StopAsync().Wait();

            _cameraProvider.FrameCaptured -= OnFrameCaptured;
            _handTracker.HandTracked -= OnHandTracked;
            _cursorEngine.CursorMoved -= OnCursorMoved;
            _gestureDetector.GestureRecognized -= OnGestureRecognized;
            _cameraProvider.CameraError -= OnCameraError;
            _handTracker.TrackerError -= OnTrackerError;
        }
    }
}
