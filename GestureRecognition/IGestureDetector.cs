using System;
using AirGestureAI.Models;

namespace AirGestureAI.GestureRecognition
{
    /// <summary>
    /// Defines operations for recognizing gestures (up, down, open palm), validating them, and managing lifecycle events.
    /// </summary>
    public interface IGestureDetector
    {
        /// <summary>Occurs when a candidate gesture is first detected.</summary>
        event EventHandler<GestureEventArgs>? GestureDetected;

        /// <summary>Occurs when a candidate gesture passes stability validation.</summary>
        event EventHandler<GestureEventArgs>? GestureValidated;

        /// <summary>Occurs when a gesture has been officially validated and recognized.</summary>
        event EventHandler<GestureEventArgs>? GestureRecognized;

        /// <summary>Occurs when an in-progress gesture candidate is cancelled or interrupted.</summary>
        event EventHandler<GestureEventArgs>? GestureCancelled;

        /// <summary>Gets the current active gesture type.</summary>
        GestureType ActiveGesture { get; }

        /// <summary>Gets the current state of the gesture state machine.</summary>
        EngineGestureState CurrentState { get; }

        /// <summary>
        /// Processes the current frame's hand tracking data to detect gestures.
        /// </summary>
        /// <param name="handData">The detected hand tracking landmarks.</param>
        void Process(HandData handData);

        /// <summary>
        /// Resets the internal gesture state and history buffers.
        /// </summary>
        void Reset();
    }
}
