namespace AirGestureAI.GestureRecognition
{
    /// <summary>
    /// Specifies the lifecycle states of the Gesture Recognition Engine's state machine.
    /// </summary>
    public enum EngineGestureState
    {
        /// <summary>
        /// No gesture candidate active. System is listening for new hand movement/posture.
        /// </summary>
        Idle,

        /// <summary>
        /// A candidate gesture has been initially detected in the current frame.
        /// </summary>
        Detecting,

        /// <summary>
        /// Candidate gesture is being validated for continuous stability over the required duration.
        /// </summary>
        Validating,

        /// <summary>
        /// Gesture has been successfully validated and officially recognized.
        /// </summary>
        Recognized,

        /// <summary>
        /// Cooldown period after gesture recognition to prevent duplicate triggers.
        /// </summary>
        Cooldown
    }
}
