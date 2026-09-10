namespace AirGestureAI.HoverSelection
{
    /// <summary>
    /// Specifies the lifecycle states of the Hover Selection Engine's dwell timer.
    /// </summary>
    public enum SelectionState
    {
        /// <summary>
        /// No active target under the cursor. Timer is idle.
        /// </summary>
        None,

        /// <summary>
        /// Cursor is positioned over a target. Waiting for the cursor to remain
        /// stable before the dwell countdown begins.
        /// </summary>
        Hovering,

        /// <summary>
        /// Cursor has been stable over the target long enough to begin the dwell
        /// countdown. Progress is being accumulated.
        /// </summary>
        Counting,

        /// <summary>
        /// The dwell timer completed. The target has been selected. This state
        /// is held for one engine tick, then the engine resets to None to prevent
        /// duplicate selections.
        /// </summary>
        Selected,

        /// <summary>
        /// The dwell timer was interrupted — either the cursor moved away from the
        /// target, the target changed, or tracking was lost. Progress is discarded.
        /// </summary>
        Cancelled
    }
}
