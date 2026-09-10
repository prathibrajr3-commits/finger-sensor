namespace AirGestureAI.Input
{
    /// <summary>
    /// Defines operations for simulating mouse wheel scrolling and keyboard inputs using OS APIs.
    /// </summary>
    public interface IInputSimulator
    {
        /// <summary>
        /// Simulates a single mouse wheel scroll upward.
        /// </summary>
        void ScrollUp();

        /// <summary>
        /// Simulates a single mouse wheel scroll downward.
        /// </summary>
        void ScrollDown();

        /// <summary>
        /// Simulates pressing and releasing the Spacebar key (Play/Pause action).
        /// </summary>
        void PressSpace();
    }
}
