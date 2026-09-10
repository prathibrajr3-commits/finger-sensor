using System;

namespace AirGestureAI.HoverSelection
{
    /// <summary>
    /// Event arguments raised when the hover selection state changes or a target is selected.
    /// </summary>
    public sealed class SelectionEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the target that the event relates to. May be <see langword="null"/>
        /// for <c>TargetLost</c> events where no target is under the cursor.
        /// </summary>
        public TargetInfo? Target { get; }

        /// <summary>
        /// Gets the current selection state at the time of the event.
        /// </summary>
        public SelectionState State { get; }

        /// <summary>
        /// Gets the hover progress as a value between 0.0 and 1.0.
        /// <list type="bullet">
        ///   <item>0.0 = dwell just started.</item>
        ///   <item>1.0 = dwell complete (selection triggered).</item>
        /// </list>
        /// Meaningful only when <see cref="State"/> is <see cref="SelectionState.Counting"/>
        /// or <see cref="SelectionState.Selected"/>. Zero otherwise.
        /// </summary>
        public double Progress { get; }

        /// <summary>
        /// Gets the elapsed dwell time in milliseconds at the moment the event was raised.
        /// </summary>
        public double ElapsedMs { get; }

        /// <summary>
        /// Gets the UTC timestamp when the event was created.
        /// </summary>
        public DateTime Timestamp { get; } = DateTime.UtcNow;

        /// <summary>
        /// Initializes a new instance of <see cref="SelectionEventArgs"/>.
        /// </summary>
        public SelectionEventArgs(TargetInfo? target, SelectionState state, double progress, double elapsedMs)
        {
            Target    = target;
            State     = state;
            Progress  = progress;
            ElapsedMs = elapsedMs;
        }

        /// <inheritdoc/>
        public override string ToString() =>
            $"[SelectionEvent state={State} progress={Progress:P0} target={Target?.Id ?? "none"}]";
    }
}
