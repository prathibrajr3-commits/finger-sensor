namespace AirGestureAI.Models
{
    /// <summary>
    /// Specifies the hover selection and tracking states of the virtual cursor.
    /// </summary>
    public enum CursorState
    {
        /// <summary>
        /// Cursor is hidden completely (Opacity = 0, Visibility = Hidden).
        /// </summary>
        Hidden,

        /// <summary>
        /// Cursor is fading in.
        /// </summary>
        Appearing,

        /// <summary>
        /// Hand is detected, cursor is active and ready (Blue).
        /// </summary>
        Ready,

        /// <summary>
        /// Cursor is actively tracking index finger movement.
        /// </summary>
        Tracking,

        /// <summary>
        /// Cursor is stationary over a target area, starting selection countdown (Yellow).
        /// </summary>
        Hovering,

        /// <summary>
        /// Cursor hovered over target for threshold time, selection triggered (Green).
        /// </summary>
        Selected,

        /// <summary>
        /// Hand lost during tracking (Red).
        /// </summary>
        HandLost,

        /// <summary>
        /// Cursor is fading out.
        /// </summary>
        FadingOut
    }
}
