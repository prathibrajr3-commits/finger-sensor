namespace AirGestureAI.Models
{
    /// <summary>
    /// Specifies the type of hand gesture recognized by the system.
    /// </summary>
    public enum GestureType
    {
        /// <summary>
        /// No gesture detected or active.
        /// </summary>
        None,

        /// <summary>
        /// Hand moving upwards, triggers scrolling up.
        /// </summary>
        ScrollUp,

        /// <summary>
        /// Hand moving downwards, triggers scrolling down.
        /// </summary>
        ScrollDown,

        /// <summary>
        /// Open palm hand posture, triggers play/pause keyboard action (Space key).
        /// </summary>
        OpenPalm
    }
}
