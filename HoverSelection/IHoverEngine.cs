using System;
using System.Collections.Generic;
using System.Windows;

namespace AirGestureAI.HoverSelection
{
    /// <summary>
    /// Contract for the Hover Selection Engine.
    ///
    /// <para>
    /// The hover engine sits between the Cursor Engine and the Gesture Engine in the
    /// processing pipeline:
    /// <code>
    ///   Cursor Engine  →  IHoverEngine  →  Gesture Engine / Application Plugin
    /// </code>
    /// It is responsible for determining which registered target is under the cursor,
    /// running a configurable dwell timer, and raising selection events. It knows
    /// nothing about gestures, Windows input, or application-specific behaviour.
    /// </para>
    /// </summary>
    public interface IHoverEngine : IDisposable
    {
        // ── Events ────────────────────────────────────────────────────────────

        /// <summary>
        /// Raised when the cursor enters a registered target region and hover begins.
        /// </summary>
        event EventHandler<SelectionEventArgs>? HoverStarted;

        /// <summary>
        /// Raised on every engine tick while the dwell timer is counting.
        /// Provides progress (0.0–1.0) for rendering a progress ring or similar UI.
        /// </summary>
        event EventHandler<SelectionEventArgs>? HoverProgress;

        /// <summary>
        /// Raised when hover is interrupted before selection completes (cursor moved,
        /// target changed, or tracking lost).
        /// </summary>
        event EventHandler<SelectionEventArgs>? HoverCancelled;

        /// <summary>
        /// Raised exactly once when the dwell timer reaches 100 % for a target.
        /// The engine resets immediately after raising this event so duplicate
        /// selections cannot occur.
        /// </summary>
        event EventHandler<SelectionEventArgs>? TargetSelected;

        /// <summary>
        /// Raised when the cursor leaves all registered targets (enters empty space).
        /// </summary>
        event EventHandler<SelectionEventArgs>? TargetLost;

        // ── State ─────────────────────────────────────────────────────────────

        /// <summary>Gets the current selection state of the engine.</summary>
        SelectionState CurrentState { get; }

        /// <summary>
        /// Gets the target that is currently under the cursor, or <see langword="null"/>
        /// if no registered target is hit.
        /// </summary>
        TargetInfo? CurrentTarget { get; }

        /// <summary>
        /// Gets the current dwell progress (0.0 = just started, 1.0 = complete).
        /// Zero when <see cref="CurrentState"/> is not <see cref="SelectionState.Counting"/>
        /// or <see cref="SelectionState.Selected"/>.
        /// </summary>
        double Progress { get; }

        // ── Target Registry ───────────────────────────────────────────────────

        /// <summary>
        /// Registers a selectable target. If a target with the same <see cref="TargetInfo.Id"/>
        /// already exists it is replaced.
        /// </summary>
        void RegisterTarget(TargetInfo target);

        /// <summary>
        /// Registers multiple targets at once, replacing any that share an existing ID.
        /// </summary>
        void RegisterTargets(IEnumerable<TargetInfo> targets);

        /// <summary>
        /// Removes the target with the specified <paramref name="id"/>.
        /// If it was the active hover target the timer is cancelled first.
        /// </summary>
        void UnregisterTarget(string id);

        /// <summary>
        /// Removes all registered targets and cancels any active hover session.
        /// </summary>
        void ClearTargets();

        // ── Cursor Integration ────────────────────────────────────────────────

        /// <summary>
        /// Called by the Cursor Engine each time the cursor position changes.
        /// This is the primary input into the hover engine.
        /// </summary>
        /// <param name="position">Current cursor position in screen DIPs.</param>
        /// <param name="isTracking">
        /// <see langword="true"/> if hand tracking is active; <see langword="false"/>
        /// if tracking is lost (pauses the dwell timer).
        /// </param>
        void UpdatePosition(Point position, bool isTracking);

        /// <summary>
        /// Resets the engine to its initial state, cancelling any active hover session.
        /// Call when the cursor engine is hidden or the application session ends.
        /// </summary>
        void Reset();
    }
}
