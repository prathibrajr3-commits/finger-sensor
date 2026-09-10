using System;
using System.Threading;
using System.Threading.Tasks;
using AirGestureAI.AgentRuntime;
using AirGestureAI.Analytics;
using AirGestureAI.Camera;
using AirGestureAI.Configuration;
using AirGestureAI.Cursor;
using AirGestureAI.GestureAI;
using AirGestureAI.GestureRecognition;
using AirGestureAI.HoverSelection;
using AirGestureAI.Input;
using AirGestureAI.HandTracking;
using AirGestureAI.Learning;
using AirGestureAI.Models;
using AirGestureAI.Semantic;
using OpenCvSharp;

namespace AirGestureAI.Services
{
    /// <summary>
    /// Coordinates operations between camera capture, hand tracking, virtual cursor,
    /// hover selection, gesture detection, and input simulation.
    /// v4.0: Gestures are now routed through SemanticIntentEngine and AgentOrchestrator
    /// before raw input simulation is performed (when UseAgentRouting is enabled).
    /// </summary>
    public class AppCoordinator : IDisposable
    {
        private readonly ICameraProvider _cameraProvider;
        private readonly IHandTracker _handTracker;
        private readonly ICursorEngine _cursorEngine;
        private readonly IHoverEngine _hoverEngine;
        private readonly IGestureDetector _gestureDetector;
        private readonly IInputSimulator _inputSimulator;
        private readonly PipelineManager _pipelineManager;
        private readonly AdaptiveLearningEngine _adaptiveLearning;
        private readonly AnalyticsEngine _analytics;

        // v4.0 Agentic routing subsystems
        private readonly SemanticIntentEngine _semanticIntentEngine;
        private readonly GestureSequenceEngine _gestureSequenceEngine;
        private readonly AgentOrchestrator _agentOrchestrator;
        private readonly AppConfig _config;

        private bool _isInitialized;

        /// <summary>
        /// Occurs when a camera frame has been processed and is ready for rendering in the live preview.
        /// </summary>
        public event EventHandler<FrameProcessedEventArgs>? FrameProcessed;

        /// <summary>
        /// Occurs when hand tracking landmark processing is completed.
        /// </summary>
        public event EventHandler<HandTrackedEventArgs>? HandTracked;

        /// <summary>
        /// Occurs when a gesture has been officially validated and recognized.
        /// </summary>
        public event EventHandler<GestureEventArgs>? GestureRecognized;

        /// <summary>
        /// Gets a value indicating whether the coordination loop is running.
        /// </summary>
        public bool IsRunning => _pipelineManager.IsRunning;

        /// <summary>
        /// Initializes a new instance of the <see cref="AppCoordinator"/> class.
        /// </summary>
        public AppCoordinator(
            ICameraProvider cameraProvider,
            IHandTracker handTracker,
            ICursorEngine cursorEngine,
            IHoverEngine hoverEngine,
            IGestureDetector gestureDetector,
            IInputSimulator inputSimulator,
            PipelineManager pipelineManager,
            AdaptiveLearningEngine adaptiveLearning,
            AnalyticsEngine analytics,
            SemanticIntentEngine semanticIntentEngine,
            GestureSequenceEngine gestureSequenceEngine,
            AgentOrchestrator agentOrchestrator,
            AppConfig config)
        {
            _cameraProvider       = cameraProvider;
            _handTracker          = handTracker;
            _cursorEngine         = cursorEngine;
            _hoverEngine          = hoverEngine;
            _gestureDetector      = gestureDetector;
            _inputSimulator       = inputSimulator;
            _pipelineManager      = pipelineManager;
            _adaptiveLearning     = adaptiveLearning     ?? throw new ArgumentNullException(nameof(adaptiveLearning));
            _analytics            = analytics            ?? throw new ArgumentNullException(nameof(analytics));
            _semanticIntentEngine = semanticIntentEngine ?? throw new ArgumentNullException(nameof(semanticIntentEngine));
            _gestureSequenceEngine = gestureSequenceEngine ?? throw new ArgumentNullException(nameof(gestureSequenceEngine));
            _agentOrchestrator    = agentOrchestrator    ?? throw new ArgumentNullException(nameof(agentOrchestrator));
            _config               = config               ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Starts the camera stream and hand tracking processor.
        /// </summary>
        /// <param name="cameraIndex">The webcam index to use.</param>
        public async Task StartAsync(int cameraIndex)
        {
            if (IsRunning) return;

            Utilities.Logger.Info("Starting AppCoordinator via PipelineManager...");

            if (!_isInitialized)
            {
                // Subscribe to events
                _cameraProvider.FrameCaptured += OnFrameCaptured;
                _cameraProvider.CameraError   += OnCameraError;
                _handTracker.HandTracked       += OnHandTracked;
                _handTracker.TrackerError      += OnTrackerError;
                _gestureDetector.GestureRecognized += OnGestureRecognized;
                _isInitialized = true;
            }

            await _pipelineManager.StartAsync(cameraIndex);

            Utilities.Logger.Info("AppCoordinator started successfully.");
        }

        /// <summary>
        /// Stops active operations.
        /// </summary>
        public async Task StopAsync()
        {
            if (!IsRunning) return;

            Utilities.Logger.Info("Stopping AppCoordinator via PipelineManager...");

            await _pipelineManager.StopAsync();

            Utilities.Logger.Info("AppCoordinator stopped.");
        }

        private void OnFrameCaptured(object? sender, FrameEventArgs e)
        {
            // Clone the frame synchronously on the capture thread before dispatching it asynchronously
            var frameClone = e.Frame.Clone();
            var timestamp  = e.Timestamp;
            _ = Task.Run(async () =>
            {
                try
                {
                    if (_handTracker.IsTracking)
                    {
                        await _handTracker.ProcessFrameAsync(frameClone, timestamp);
                    }
                }
                catch (Exception ex)
                {
                    Utilities.Logger.Error("Error sending frame to tracker", ex);
                }
                finally
                {
                    frameClone.Dispose();
                }
            });

            try
            {
                // Convert Mat to BitmapSource synchronously on the capture thread.
                // This is extremely safe because e.Frame is guaranteed to be allocated until this handler returns.
                var bitmap = OpenCvSharp.WpfExtensions.BitmapSourceConverter.ToBitmapSource(e.Frame);

                // Freeze the BitmapSource so it can be read from the WPF UI thread without thread affinity issues.
                bitmap.Freeze();

                // Trigger UI rendering with the frozen BitmapSource
                FrameProcessed?.Invoke(this, new FrameProcessedEventArgs(bitmap, new HandData { IsDetected = false }));
            }
            catch (Exception ex)
            {
                Utilities.Logger.Error("Error converting camera frame to BitmapSource", ex);
            }
        }

        private void OnHandTracked(object? sender, HandTrackedEventArgs e)
        {
            // Update Cursor Engine
            _cursorEngine.Update(e.HandData);

            // Update Gesture detector
            _gestureDetector.Process(e.HandData);

            // v4.0: Feed tracking data into the temporal GestureSequenceEngine (fire-and-forget)
            _ = Task.Run(() => _gestureSequenceEngine.ProcessFrameAsync(e.HandData));

            // Phase 27: Feed tracking data into analytics
            _analytics.CollectUsage(e.HandData);

            // Forward to external subscribers (e.g. UI dashboard)
            HandTracked?.Invoke(this, e);
        }

        private void OnGestureRecognized(object? sender, GestureEventArgs e)
        {
            Utilities.Logger.Info($"Gesture validated and triggered: {e.Gesture}");

            // Phase 27: Feed gesture into adaptive learning and analytics
            _adaptiveLearning.ProcessGesture(e.Gesture);
            _analytics.CollectGesture(e.Gesture);

            // Forward event to UI subscribers
            GestureRecognized?.Invoke(this, e);

            // Phase 10: Route gesture through the plugin framework first
            var context    = _pipelineManager.ContextEngine.CurrentContext;
            bool isOverridden = _pipelineManager.PluginManager.RouteGesture(e.Gesture, context);

            if (isOverridden)
            {
                Utilities.Logger.Info($"Gesture {e.Gesture} was overridden by an active plugin adapter.");
                return;
            }

            if (_config.UseAgentRouting)
            {
                // v4.0: Route gesture asynchronously through SemanticIntentEngine → AgentOrchestrator
                // before dispatching to raw input. Fire-and-forget from the capture thread.
                _ = Task.Run(() => DispatchGestureThroughAgentPipelineAsync(e.Gesture));
            }
            else
            {
                // Legacy direct dispatch path (UseAgentRouting = false)
                ExecuteRawGestureInput(e.Gesture);
            }
        }

        /// <summary>
        /// v4.0 agentic dispatch: classifies the gesture semantically, routes through
        /// AgentOrchestrator, then falls back to raw input simulation if needed.
        /// </summary>
        private async Task DispatchGestureThroughAgentPipelineAsync(GestureType gesture,
            CancellationToken ct = default)
        {
            try
            {
                // 1. Classify gesture intent via SemanticIntentEngine
                var prompt = $"Gesture detected: {gesture}. User is performing hand gesture control.";
                var intent = await _semanticIntentEngine.ClassifyAsync(prompt, ct);

                Utilities.Logger.Info(
                    $"AppCoordinator: Intent classified — Tag='{intent.Tag}' " +
                    $"Confidence={intent.Confidence:P0} Explanation='{intent.Explanation}'");

                // 2. Dispatch through AgentOrchestrator (GestureAgent)
                await _agentOrchestrator.DispatchGestureAsync(gesture.ToString(), intent.Explanation, ct);

                // 3. Still execute the raw input action so physical control is preserved
                ExecuteRawGestureInput(gesture);
            }
            catch (Exception ex)
            {
                // Never let agent failures break gesture control
                Utilities.Logger.Error($"AppCoordinator: Agent pipeline failed for gesture '{gesture}'. Falling back.", ex);
                ExecuteRawGestureInput(gesture);
            }
        }

        /// <summary>
        /// Direct input simulation — the pre-v4.0 gesture dispatch path.
        /// Still called after agent routing completes to preserve physical control.
        /// </summary>
        private void ExecuteRawGestureInput(GestureType gesture)
        {
            switch (gesture)
            {
                case GestureType.ScrollUp:
                    _inputSimulator.ScrollUp();
                    break;
                case GestureType.ScrollDown:
                    _inputSimulator.ScrollDown();
                    break;
                case GestureType.OpenPalm:
                    _inputSimulator.PressSpace();
                    break;
            }
        }

        private void OnCameraError(object? sender, string error)
        {
            Utilities.Logger.Error($"Camera Provider Error: {error}");
        }

        private void OnTrackerError(object? sender, string error)
        {
            Utilities.Logger.Error($"Hand Tracker Error: {error}");
        }

        /// <summary>
        /// Disposes all coordinated modules.
        /// </summary>
        public void Dispose()
        {
            // Unsubscribe
            if (_isInitialized)
            {
                _cameraProvider.FrameCaptured       -= OnFrameCaptured;
                _cameraProvider.CameraError         -= OnCameraError;
                _handTracker.HandTracked             -= OnHandTracked;
                _handTracker.TrackerError            -= OnTrackerError;
                _gestureDetector.GestureRecognized   -= OnGestureRecognized;
                _isInitialized = false;
            }

            _cameraProvider.Dispose();
            _handTracker.Dispose();
            _cursorEngine.Dispose();
            _hoverEngine.Dispose();
        }
    }

    /// <summary>
    /// Event arguments containing coordinates and visual frame data for rendering.
    /// </summary>
    public class FrameProcessedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the processed camera frame as a WPF-compatible ImageSource.
        /// </summary>
        public System.Windows.Media.ImageSource PreviewImage { get; }

        /// <summary>
        /// Gets the detected hand data containing current landmarks.
        /// </summary>
        public HandData HandData { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FrameProcessedEventArgs"/> class.
        /// </summary>
        public FrameProcessedEventArgs(System.Windows.Media.ImageSource previewImage, HandData handData)
        {
            PreviewImage = previewImage;
            HandData     = handData;
        }
    }
}
