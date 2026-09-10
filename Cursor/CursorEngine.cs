using System;
using System.Windows;
using System.Windows.Threading;
using AirGestureAI.Configuration;
using AirGestureAI.HoverSelection;
using AirGestureAI.Models;
using AirGestureAI.Utilities;

namespace AirGestureAI.Cursor
{
    /// <summary>
    /// Production cursor engine for AirGesture AI — Phase 4.
    ///
    /// Responsibilities:
    ///   • Receives raw MediaPipe normalised (0–1) hand landmark coordinates
    ///     from the AI Engine via <see cref="Update"/>.
    ///   • Maps those coordinates to DPI-aware WPF screen pixels and clamps
    ///     them within the primary screen boundaries.
    ///   • Applies Double Exponential Smoothing (Holt's linear trend) to
    ///     eliminate jitter without introducing noticeable lag.
    ///   • Drives a <see cref="CursorWindow"/> overlay that lives on top of all
    ///     other windows and is click-through at the Win32 level.
    ///   • Maintains an explicit state machine:
    ///       Hidden → Appearing → Tracking → HandLost → FadingOut → Hidden
    ///   • Fades the cursor out after 4 s of no movement (configurable).
    ///   • Freezes at the last known position when tracking is temporarily lost.
    ///   • Renders position updates on an independent DispatcherTimer so the
    ///     cursor feels smooth even when camera frame delivery is uneven.
    ///   • Logs all state transitions and, in DEBUG builds, throttled position
    ///     telemetry and FPS counters.
    /// </summary>
    public class CursorEngine : ICursorEngine
    {
        // ── Dependencies ──────────────────────────────────────────────────────
        private readonly AppConfig _config;
        private readonly DoubleExponentialFilter _filter;

        // ── Overlay window (created on UI thread) ─────────────────────────────
        private CursorWindow? _window;

        // ── State ─────────────────────────────────────────────────────────────
        private CursorState _currentState = CursorState.Hidden;
        private Point _smoothedPosition   = new Point(0, 0);
        private Point _lastRenderedPosition;
        private bool  _isDisposed;

        // ── Inactivity tracking ───────────────────────────────────────────────
        private DateTime _lastMovementTime = DateTime.UtcNow;

        // ── Independent render timer ──────────────────────────────────────────
        // Fires on the WPF Dispatcher at ~60 Hz so position updates are smooth
        // regardless of how frequently the AI Engine delivers landmarks.
        private DispatcherTimer? _renderTimer;

        // ── Thread-safe latest landmark ───────────────────────────────────────
        // Written by the tracking thread, read by the render timer on the UI thread.
        private volatile bool   _pendingDetected;
        private volatile float  _pendingNormX;
        private volatile float  _pendingNormY;
        private volatile bool   _hasNewLandmark;

        // ── Screen metrics (DPI-aware) ────────────────────────────────────────
        private double _screenWidth;
        private double _screenHeight;

#if DEBUG
        // ── DEBUG: FPS / telemetry counters ──────────────────────────────────
        private int    _debugRenderTicks;
        private double _debugFpsAccumulatorSec;
        private DateTime _debugLastFpsLog = DateTime.UtcNow;
        private DateTime _debugLastPositionLog = DateTime.UtcNow;
        private readonly TimeSpan _debugPositionLogInterval = TimeSpan.FromSeconds(1);
        private readonly TimeSpan _debugFpsLogInterval      = TimeSpan.FromSeconds(5);
#endif

        // ── ICursorEngine ─────────────────────────────────────────────────────

        /// <inheritdoc/>
        public event EventHandler<Point>?       CursorMoved;

        /// <inheritdoc/>
        public event EventHandler<CursorState>? StateChanged;

        /// <inheritdoc/>
        public Point CurrentPosition => _smoothedPosition;

        /// <inheritdoc/>
        public CursorState CurrentState => _currentState;

        private readonly IHoverEngine? _hoverEngine;

        // ── Constructor ───────────────────────────────────────────────────────

        /// <summary>
        /// Creates a new <see cref="CursorEngine"/> instance.
        /// The overlay window is NOT yet visible — call <see cref="Show"/> to display it.
        /// </summary>
        public CursorEngine(AppConfig config, IHoverEngine? hoverEngine = null)
        {
            _config      = config ?? throw new ArgumentNullException(nameof(config));
            _hoverEngine = hoverEngine;
            _filter      = new DoubleExponentialFilter(
                alpha: _config.SmoothingFactor,
                beta:  _config.TrendSmoothingFactor);

            if (_hoverEngine != null)
            {
                _hoverEngine.HoverStarted   += OnHoverStarted;
                _hoverEngine.TargetSelected += OnTargetSelected;
                _hoverEngine.HoverCancelled += OnHoverCancelled;
                _hoverEngine.TargetLost     += OnTargetLost;
            }

            Logger.Info("CursorEngine created with IHoverEngine integration.");
        }

        // ── ICursorEngine – Show / Hide ───────────────────────────────────────

        /// <summary>
        /// Creates the overlay window (if not yet created) and starts the independent
        /// rendering timer. Must be called from the application UI thread.
        /// </summary>
        public void Show()
        {
            EnsureWindow();
            EnsureRenderTimer();
            Logger.Info("CursorEngine shown — render timer started.");
        }

        /// <summary>
        /// Stops the rendering timer and hides the overlay window.
        /// </summary>
        public void Hide()
        {
            StopRenderTimer();
            DispatchToUI(() => _window?.HideImmediately());
            TransitionState(CursorState.Hidden);
            Logger.Info("CursorEngine hidden.");
        }

        // ── ICursorEngine – Update ────────────────────────────────────────────

        /// <summary>
        /// Called by the AI Engine (on a background thread) whenever new hand tracking
        /// data is available. Stores the latest landmark atomically; the render timer
        /// picks it up on the next UI tick.
        /// </summary>
        public void Update(HandData handData)
        {
            if (_isDisposed) return;

            if (handData.IsDetected && handData.IndexTip != null)
            {
                // Write atomically — these are volatile fields so the compiler and
                // CPU do not reorder stores past the _hasNewLandmark flag write.
                _pendingNormX   = handData.IndexTip.X;
                _pendingNormY   = handData.IndexTip.Y;
                _pendingDetected = true;
                _hasNewLandmark  = true; // publish — the render timer sees this
            }
            else
            {
                _pendingDetected = false;
                _hasNewLandmark  = true;
            }
        }

        // ── Private – Window / Timer lifecycle ───────────────────────────────

        private void EnsureWindow()
        {
            if (_window != null) return;

            // The CursorWindow is a WPF Window and MUST be created on the UI thread.
            DispatchToUISync(() =>
            {
                RefreshScreenMetrics();
                _window = new CursorWindow(_config);
                _window.FadeInCompleted  += OnFadeInCompleted;
                _window.FadeOutCompleted += OnFadeOutCompleted;

                // Position off-screen initially so the first Show() does not flash.
                _window.Left = -500;
                _window.Top  = -500;
                _window.Show();
                _window.Hide(); // immediately re-hide — Opacity is 0 from XAML
                Logger.Info("CursorWindow overlay created.");
            });
        }

        private void EnsureRenderTimer()
        {
            if (_renderTimer != null) return;

            DispatchToUISync(() =>
            {
                _renderTimer = new DispatcherTimer(DispatcherPriority.Render)
                {
                    Interval = TimeSpan.FromMilliseconds(_config.CursorTickIntervalMs)
                };
                _renderTimer.Tick += OnRenderTick;
                _renderTimer.Start();
            });
        }

        private void StopRenderTimer()
        {
            DispatchToUISync(() =>
            {
                if (_renderTimer == null) return;
                _renderTimer.Stop();
                _renderTimer.Tick -= OnRenderTick;
                _renderTimer = null;
            });
        }

        // ── Private – Render tick (UI thread, ~60 Hz) ─────────────────────────

        private void OnRenderTick(object? sender, EventArgs e)
        {
            if (_isDisposed || _window == null) return;

#if DEBUG
            _debugRenderTicks++;
            var now = DateTime.UtcNow;
            _debugFpsAccumulatorSec += _config.CursorTickIntervalMs / 1000.0;
            if (now - _debugLastFpsLog >= _debugFpsLogInterval)
            {
                double fps = _debugRenderTicks / (now - _debugLastFpsLog).TotalSeconds;
                Logger.Info($"[DEBUG] CursorEngine render FPS: {fps:F1}");
                _debugRenderTicks = 0;
                _debugLastFpsLog  = now;
            }
#endif

            // Consume the latest landmark snapshot.
            if (!_hasNewLandmark) return;
            _hasNewLandmark = false;

            bool detected = _pendingDetected;
            float normX   = _pendingNormX;
            float normY   = _pendingNormY;

            if (detected)
            {
                ProcessDetectedHand(normX, normY);
            }
            else
            {
                ProcessHandLost();
            }

            // Inactivity fade-out check (runs every tick).
            CheckInactivityTimeout();
        }

        // ── Private – Hand detected path ─────────────────────────────────────

        private void ProcessDetectedHand(float normX, float normY)
        {
            // Map normalised coords → DPI-aware screen pixels and clamp.
            var rawScreen = NormToScreen(normX, normY);

            // Apply Double Exponential Smoothing.
            // Reset the filter when the hand just returned from a lost state to
            // prevent a glide artefact from the previous position.
            bool wasLost = _currentState == CursorState.HandLost ||
                           _currentState == CursorState.Hidden    ||
                           _currentState == CursorState.FadingOut;
            if (wasLost)
            {
                _filter.Reset();
                // Seed the filter with the new raw position so there is no
                // glide from the old smoothed position.
                _filter.Filter(rawScreen);
            }

            Point smoothed = _filter.Filter(rawScreen);
            _smoothedPosition = smoothed;

            // Movement dead-zone check to determine inactivity.
            double dx = smoothed.X - _lastRenderedPosition.X;
            double dy = smoothed.Y - _lastRenderedPosition.Y;
            double displacement = Math.Sqrt(dx * dx + dy * dy);
            if (displacement > _config.MovementDeadZonePixels)
            {
                _lastMovementTime    = DateTime.UtcNow;
                _lastRenderedPosition = smoothed;
            }

            // Update overlay position.
            _window!.UpdatePosition(smoothed);
            CursorMoved?.Invoke(this, smoothed);

            // Forward to Hover Selection Engine
            _hoverEngine?.UpdatePosition(smoothed, isTracking: true);

#if DEBUG
            var now = DateTime.UtcNow;
            if (now - _debugLastPositionLog >= _debugPositionLogInterval)
            {
                Logger.Info($"[DEBUG] Cursor smoothed position: ({smoothed.X:F1}, {smoothed.Y:F1})");
                _debugLastPositionLog = now;
            }
#endif

            // State transitions on hand detected.
            switch (_currentState)
            {
                case CursorState.Hidden:
                case CursorState.FadingOut:
                case CursorState.HandLost:
                    Logger.Info("Hand detected — cursor fading in.");
                    TransitionState(CursorState.Appearing);
                    _window.FadeIn(immediate: false);
                    break;

                case CursorState.Appearing:
                    // Still fading in — no action needed.
                    break;

                case CursorState.Ready:
                case CursorState.Tracking:
                case CursorState.Hovering:
                case CursorState.Selected:
                    // Already visible — ensure state is Tracking if not hovering/selected
                    if (_currentState == CursorState.Ready || _currentState == CursorState.Appearing)
                        TransitionState(CursorState.Tracking);
                    break;
            }
        }

        // ── Private – Hand lost path ──────────────────────────────────────────

        private void ProcessHandLost()
        {
            // Notify Hover Engine that tracking was lost
            _hoverEngine?.UpdatePosition(_smoothedPosition, isTracking: false);

            if (_currentState == CursorState.HandLost ||
                _currentState == CursorState.Hidden   ||
                _currentState == CursorState.FadingOut)
                return; // already handled

            Logger.Info("Hand lost — freezing cursor at last valid position.");
            TransitionState(CursorState.HandLost);
            // Cursor stays at _smoothedPosition (last valid) — no reset.
        }

        // ── Private – Inactivity fade-out ────────────────────────────────────

        private void CheckInactivityTimeout()
        {
            if (_currentState != CursorState.Tracking &&
                _currentState != CursorState.Ready    &&
                _currentState != CursorState.Hovering &&
                _currentState != CursorState.Selected &&
                _currentState != CursorState.HandLost)
                return;

            double idleMs = (DateTime.UtcNow - _lastMovementTime).TotalMilliseconds;
            if (idleMs >= _config.InactivityTimeoutMs)
            {
                Logger.Info($"Cursor inactive for {idleMs:F0} ms — fading out.");
                TransitionState(CursorState.FadingOut);
                _window?.FadeOut();
            }
        }

        // ── Private – State machine ───────────────────────────────────────────

        private void TransitionState(CursorState next)
        {
            if (_currentState == next) return;
            Logger.Info($"Cursor state: {_currentState} → {next}");
            _currentState = next;
            StateChanged?.Invoke(this, next);

            // Update overlay window colors smoothly
            DispatchToUI(() => _window?.SetState(next));
        }

        // ── Private – Hover Engine Event Handlers ──────────────────────────────

        private void OnHoverStarted(object? sender, AirGestureAI.HoverSelection.SelectionEventArgs e)
        {
            if (_currentState == CursorState.Tracking || _currentState == CursorState.Ready)
            {
                TransitionState(CursorState.Hovering);
            }
        }

        private void OnTargetSelected(object? sender, AirGestureAI.HoverSelection.SelectionEventArgs e)
        {
            TransitionState(CursorState.Selected);
        }

        private void OnHoverCancelled(object? sender, AirGestureAI.HoverSelection.SelectionEventArgs e)
        {
            if (_currentState == CursorState.Hovering || _currentState == CursorState.Selected)
            {
                TransitionState(CursorState.Tracking);
            }
        }

        private void OnTargetLost(object? sender, AirGestureAI.HoverSelection.SelectionEventArgs e)
        {
            if (_currentState == CursorState.Hovering || _currentState == CursorState.Selected)
            {
                TransitionState(CursorState.Tracking);
            }
        }

        // ── Private – Fade callbacks ──────────────────────────────────────────

        private void OnFadeInCompleted(object? sender, EventArgs e)
        {
            // Only advance if we are still in Appearing — not if a quick hand-loss
            // happened during the fade-in animation.
            if (_currentState == CursorState.Appearing)
                TransitionState(CursorState.Tracking);
        }

        private void OnFadeOutCompleted(object? sender, EventArgs e)
        {
            TransitionState(CursorState.Hidden);
            Logger.Info("Cursor hidden after fade-out.");
        }

        // ── Private – Screen mapping ──────────────────────────────────────────

        /// <summary>
        /// Converts MediaPipe normalised coordinates (0–1) to WPF device-independent
        /// pixels for the primary screen, clamped to the screen boundary.
        /// </summary>
        private Point NormToScreen(float normX, float normY)
        {
            // MediaPipe mirrors the X axis relative to the camera image, but WPF
            // screen coordinates grow left-to-right. No flip is needed here because
            // PythonHandTracker is expected to supply already-mirrored coordinates.
            double x = normX * _screenWidth;
            double y = normY * _screenHeight;

            // Clamp so the cursor cannot leave the screen.
            double half = _config.CursorSize / 2.0;
            x = Math.Max(half, Math.Min(_screenWidth  - half, x));
            y = Math.Max(half, Math.Min(_screenHeight - half, y));

            return new Point(x, y);
        }

        /// <summary>
        /// Reads the primary screen size in WPF device-independent pixels.
        /// WPF's <see cref="SystemParameters"/> already accounts for DPI scaling,
        /// so no manual DPI conversion is required here.
        /// </summary>
        private void RefreshScreenMetrics()
        {
            // SystemParameters.PrimaryScreenWidth/Height are in device-independent
            // pixels (DIPs). On a 1920×1080 display at 100 % DPI that is 1920×1080;
            // at 150 % DPI it is still reported as 1920×1080 DIPs, but Windows
            // will scale the actual rendered pixels — which is exactly what we want.
            _screenWidth  = SystemParameters.PrimaryScreenWidth;
            _screenHeight = SystemParameters.PrimaryScreenHeight;
            Logger.Info($"Screen metrics: {_screenWidth}×{_screenHeight} DIPs.");
        }

        // ── Private – Thread dispatch helpers ────────────────────────────────

        private static void DispatchToUI(Action action)
        {
            if (Application.Current?.Dispatcher is Dispatcher d)
                d.BeginInvoke(DispatcherPriority.Render, action);
        }

        private static void DispatchToUISync(Action action)
        {
            if (Application.Current?.Dispatcher is Dispatcher d)
                d.Invoke(action);
        }

        // ── IDisposable ───────────────────────────────────────────────────────

        /// <summary>
        /// Stops timers and closes the overlay window cleanly, releasing all resources.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            StopRenderTimer();

            DispatchToUISync(() =>
            {
                if (_window != null)
                {
                    _window.FadeInCompleted  -= OnFadeInCompleted;
                    _window.FadeOutCompleted -= OnFadeOutCompleted;
                    _window.Close();
                    _window = null;
                }
            });

            if (_hoverEngine != null)
            {
                _hoverEngine.HoverStarted   -= OnHoverStarted;
                _hoverEngine.TargetSelected -= OnTargetSelected;
                _hoverEngine.HoverCancelled -= OnHoverCancelled;
                _hoverEngine.TargetLost     -= OnTargetLost;
            }

            Logger.Info("CursorEngine disposed.");
        }
    }
}
