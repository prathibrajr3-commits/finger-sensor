using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Windows;
using AirGestureAI.Configuration;
using AirGestureAI.Utilities;

namespace AirGestureAI.HoverSelection
{
    /// <summary>
    /// Production Hover Selection Engine for AirGesture AI — Phase 5.
    ///
    /// <para>Pipeline position:</para>
    /// <code>
    ///   CursorEngine.CursorMoved  →  HoverEngine.UpdatePosition
    ///   CursorEngine.StateChanged →  HoverEngine.UpdatePosition (isTracking flag)
    ///       HoverEngine events    →  Future Gesture Engine / Application Plugin
    /// </code>
    ///
    /// <para>State machine:</para>
    /// <code>
    ///   None ──► Hovering ──► Counting ──► Selected ──► None
    ///             │               │
    ///             │ (cursor away) │ (cursor away / tracking lost / target changed)
    ///             ▼               ▼
    ///          Cancelled ──────────────────────────────────────────────────► None
    /// </code>
    ///
    /// <para>Threading model:</para>
    /// <para>
    /// <see cref="UpdatePosition"/> is expected to be called from the WPF Dispatcher
    /// (the CursorEngine render timer already runs there). All internal state is
    /// therefore single-threaded on the UI thread. The target registry uses a
    /// <see cref="ConcurrentDictionary{TKey,TValue}"/> so that external code can
    /// register/unregister targets from any thread safely.
    /// </para>
    /// </summary>
    public sealed class HoverEngine : IHoverEngine
    {
        // ── Configuration ──────────────────────────────────────────────────────
        private readonly AppConfig _config;

        // ── Target registry ────────────────────────────────────────────────────
        // ConcurrentDictionary allows thread-safe writes from any thread while
        // reads during UpdatePosition happen on the UI thread.
        private readonly ConcurrentDictionary<string, TargetInfo> _targets =
            new ConcurrentDictionary<string, TargetInfo>(StringComparer.Ordinal);

        // ── Dwell timer state ──────────────────────────────────────────────────
        private SelectionState _state  = SelectionState.None;
        private TargetInfo?    _currentTarget;
        private DateTime       _dwellStartTime;
        private double         _progress;

        // ── Tracking loss handling ─────────────────────────────────────────────
        // When tracking is lost the dwell timer is paused.
        // _pausedElapsedMs accumulates time so we resume from where we left off.
        private bool   _trackingLost;
        private double _pausedElapsedMs;

        // ── Duplicate-selection guard ──────────────────────────────────────────
        // Ensures TargetSelected fires exactly once per dwell completion.
        private bool _selectionFired;

        // ── Stability window ──────────────────────────────────────────────────
        // Cursor must remain within HoverRadiusPixels of the entry point for the
        // full dwell duration. Checked each UpdatePosition call.
        private Point _hoverEntryPoint;

        // ── Logging throttle ──────────────────────────────────────────────────
        private DateTime _lastProgressLog = DateTime.MinValue;
        private readonly TimeSpan _progressLogInterval = TimeSpan.FromSeconds(0.5);

        // ── Disposal ──────────────────────────────────────────────────────────
        private bool _isDisposed;

        // ── IHoverEngine events ────────────────────────────────────────────────

        /// <inheritdoc/>
        public event EventHandler<SelectionEventArgs>? HoverStarted;

        /// <inheritdoc/>
        public event EventHandler<SelectionEventArgs>? HoverProgress;

        /// <inheritdoc/>
        public event EventHandler<SelectionEventArgs>? HoverCancelled;

        /// <inheritdoc/>
        public event EventHandler<SelectionEventArgs>? TargetSelected;

        /// <inheritdoc/>
        public event EventHandler<SelectionEventArgs>? TargetLost;

        // ── IHoverEngine state ─────────────────────────────────────────────────

        /// <inheritdoc/>
        public SelectionState CurrentState => _state;

        /// <inheritdoc/>
        public TargetInfo? CurrentTarget => _currentTarget;

        /// <inheritdoc/>
        public double Progress => _progress;

        // ── Constructor ────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a new <see cref="HoverEngine"/> instance.
        /// </summary>
        public HoverEngine(AppConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            Logger.Info("HoverEngine created.");
        }

        // ── IHoverEngine – Target Registry ────────────────────────────────────

        /// <inheritdoc/>
        public void RegisterTarget(TargetInfo target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            _targets[target.Id] = target;
            Logger.Info($"HoverEngine: target registered — {target}");
        }

        /// <inheritdoc/>
        public void RegisterTargets(IEnumerable<TargetInfo> targets)
        {
            if (targets == null) throw new ArgumentNullException(nameof(targets));
            foreach (var t in targets)
                RegisterTarget(t);
        }

        /// <inheritdoc/>
        public void UnregisterTarget(string id)
        {
            if (_targets.TryRemove(id, out var removed))
            {
                Logger.Info($"HoverEngine: target unregistered — id={id}");

                // If this was the active target, cancel the hover.
                if (_currentTarget?.Id == id)
                    CancelHover(reason: "target unregistered");
            }
        }

        /// <inheritdoc/>
        public void ClearTargets()
        {
            _targets.Clear();
            CancelHover(reason: "targets cleared");
            Logger.Info("HoverEngine: all targets cleared.");
        }

        // ── IHoverEngine – Primary Update Entry Point ─────────────────────────

        /// <inheritdoc/>
        /// <remarks>
        /// Must be called from the WPF UI thread (the CursorEngine render timer already
        /// ensures this).
        /// </remarks>
        public void UpdatePosition(Point position, bool isTracking)
        {
            if (_isDisposed) return;

            // ── Tracking loss handling ────────────────────────────────────────
            if (!isTracking)
            {
                HandleTrackingLost();
                return;
            }

            // Tracking resumed after a loss — restart dwell from zero to prevent
            // accidental selections caused by trembling or re-entry noise.
            if (_trackingLost)
                HandleTrackingResumed();

            // ── Hit-test: find target under cursor ───────────────────────────
            TargetInfo? hit = HitTest(position);

            if (hit == null)
            {
                HandleNoTarget();
                return;
            }

            // ── Target change detection ───────────────────────────────────────
            if (_currentTarget == null || _currentTarget.Id != hit.Id)
            {
                HandleTargetChanged(hit, position);
                return;
            }

            // ── Same target — run dwell logic ─────────────────────────────────
            RunDwellTick(position);
        }

        /// <inheritdoc/>
        public void Reset()
        {
            CancelHover(reason: "engine reset");
            _trackingLost    = false;
            _pausedElapsedMs = 0;
            Logger.Info("HoverEngine reset.");
        }

        // ── Private – State handlers ──────────────────────────────────────────

        private void HandleTrackingLost()
        {
            if (_trackingLost) return; // already paused

            _trackingLost = true;

            if (_state == SelectionState.Counting)
            {
                // Accumulate elapsed time so we can resume from here.
                _pausedElapsedMs += (DateTime.UtcNow - _dwellStartTime).TotalMilliseconds;
                Logger.Info($"HoverEngine: tracking lost — dwell paused at {_pausedElapsedMs:F0} ms.");
            }
            else if (_state == SelectionState.Hovering || _state == SelectionState.Counting)
            {
                CancelHover(reason: "tracking lost");
            }
        }

        private void HandleTrackingResumed()
        {
            Logger.Info("HoverEngine: tracking resumed — dwell timer reset to avoid accidental selection.");
            _trackingLost    = false;
            _pausedElapsedMs = 0; // Full reset on resume — safety-first.
            CancelHover(reason: "tracking resumed — safety reset");
        }

        private void HandleNoTarget()
        {
            if (_state != SelectionState.None && _state != SelectionState.Cancelled)
            {
                bool hadTarget = _currentTarget != null;
                CancelHover(reason: "cursor left all targets");

                if (hadTarget)
                {
                    var args = MakeArgs(null, SelectionState.None, 0, 0);
                    TargetLost?.Invoke(this, args);
                }
            }
        }

        private void HandleTargetChanged(TargetInfo newTarget, Point position)
        {
            // Cancel any in-progress dwell for the previous target.
            if (_state == SelectionState.Hovering || _state == SelectionState.Counting)
                CancelHover(reason: $"target changed to '{newTarget.DisplayName}'");

            // Begin hover on the new target.
            _currentTarget    = newTarget;
            _hoverEntryPoint  = position;
            _pausedElapsedMs  = 0;
            _selectionFired   = false;

            TransitionState(SelectionState.Hovering);
            Logger.Info($"HoverEngine: hover started on '{newTarget.DisplayName}' (id={newTarget.Id}).");

            var args = MakeArgs(_currentTarget, SelectionState.Hovering, 0, 0);
            HoverStarted?.Invoke(this, args);
        }

        private void RunDwellTick(Point position)
        {
            // ── Stability check ───────────────────────────────────────────────
            // If the cursor has drifted beyond the hover radius from where it
            // entered the target, restart the dwell. This prevents accidental
            // selections caused by hand tremor that keeps the cursor technically
            // inside a large target while still moving.
            double dx = position.X - _hoverEntryPoint.X;
            double dy = position.Y - _hoverEntryPoint.Y;
            if (Math.Sqrt(dx * dx + dy * dy) > _config.HoverRadiusPixels)
            {
                // Cursor drifted — restart dwell from the new position.
                _hoverEntryPoint = position;
                _dwellStartTime  = DateTime.UtcNow;
                _pausedElapsedMs = 0;

                if (_state == SelectionState.Counting)
                {
                    TransitionState(SelectionState.Hovering);
                    Logger.Info($"HoverEngine: cursor drifted beyond radius on '{_currentTarget!.DisplayName}' — dwell restarted.");
                }
                return;
            }

            // ── Transition Hovering → Counting ────────────────────────────────
            if (_state == SelectionState.Hovering)
            {
                _dwellStartTime = DateTime.UtcNow;
                TransitionState(SelectionState.Counting);
            }

            // ── Accumulate elapsed and compute progress ───────────────────────
            double elapsed = _pausedElapsedMs + (DateTime.UtcNow - _dwellStartTime).TotalMilliseconds;
            double totalMs = Math.Max(1, _config.HoverDurationMs);
            _progress      = Math.Min(1.0, elapsed / totalMs);

            // ── Throttled progress event ──────────────────────────────────────
            var now = DateTime.UtcNow;
            if (now - _lastProgressLog >= _progressLogInterval)
            {
#if DEBUG
                Logger.Info($"[DEBUG] HoverEngine progress: {_progress:P0} on '{_currentTarget!.DisplayName}'");
#endif
                _lastProgressLog = now;
            }

            var progressArgs = MakeArgs(_currentTarget, SelectionState.Counting, _progress, elapsed);
            HoverProgress?.Invoke(this, progressArgs);

            // ── Selection trigger ─────────────────────────────────────────────
            if (_progress >= 1.0 && !_selectionFired)
            {
                _selectionFired = true;
                _progress = 1.0;

                Logger.Info($"HoverEngine: target SELECTED — '{_currentTarget!.DisplayName}' (id={_currentTarget.Id}) after {elapsed:F0} ms.");
                TransitionState(SelectionState.Selected);

                var selectedArgs = MakeArgs(_currentTarget, SelectionState.Selected, 1.0, elapsed);
                TargetSelected?.Invoke(this, selectedArgs);

                // Auto-reset: prevents duplicate fire on subsequent ticks.
                // The caller receives the Selected event first, then the engine clears.
                ResetAfterSelection();
            }
        }

        // ── Private – CancelHover ─────────────────────────────────────────────

        private void CancelHover(string reason)
        {
            if (_state == SelectionState.None || _state == SelectionState.Cancelled)
                return; // Nothing to cancel.

            var cancelledTarget = _currentTarget;
            double elapsedMs    = _state == SelectionState.Counting
                ? _pausedElapsedMs + (DateTime.UtcNow - _dwellStartTime).TotalMilliseconds
                : 0;

            _currentTarget   = null;
            _progress        = 0;
            _selectionFired  = false;
            _pausedElapsedMs = 0;

            Logger.Info($"HoverEngine: hover cancelled — reason: {reason}.");
            TransitionState(SelectionState.Cancelled);

            var args = MakeArgs(cancelledTarget, SelectionState.Cancelled, 0, elapsedMs);
            HoverCancelled?.Invoke(this, args);

            // Immediately transition to None so the engine is ready for new input.
            TransitionState(SelectionState.None);
        }

        private void ResetAfterSelection()
        {
            // Brief pause in Selected state before returning to None.
            // (State is already Selected; we just clear bookkeeping.)
            _currentTarget   = null;
            _progress        = 0;
            _selectionFired  = false;
            _pausedElapsedMs = 0;
            TransitionState(SelectionState.None);
        }

        // ── Private – Hit Testing ─────────────────────────────────────────────

        /// <summary>
        /// Returns the first registered target whose bounding rect contains
        /// <paramref name="position"/>, or <see langword="null"/> if none.
        /// Iteration order is not guaranteed (ConcurrentDictionary). If overlapping
        /// targets are needed, callers should provide non-overlapping rects or
        /// sort by z-order before registering.
        /// </summary>
        private TargetInfo? HitTest(Point position)
        {
            foreach (var kv in _targets)
            {
                if (kv.Value.Contains(position))
                    return kv.Value;
            }
            return null;
        }

        // ── Private – State machine ───────────────────────────────────────────

        private void TransitionState(SelectionState next)
        {
            if (_state == next) return;
            Logger.Info($"HoverEngine state: {_state} → {next}");
            _state = next;
        }

        // ── Private – Event arg factory ───────────────────────────────────────

        private static SelectionEventArgs MakeArgs(
            TargetInfo? target, SelectionState state, double progress, double elapsedMs)
            => new SelectionEventArgs(target, state, progress, elapsedMs);

        // ── IDisposable ────────────────────────────────────────────────────────

        /// <summary>
        /// Resets the engine state and releases all event subscriptions.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            Reset();
            _targets.Clear();

            // Null out event handlers to free subscriber references.
            HoverStarted   = null;
            HoverProgress  = null;
            HoverCancelled = null;
            TargetSelected = null;
            TargetLost     = null;

            Logger.Info("HoverEngine disposed.");
        }
    }
}
