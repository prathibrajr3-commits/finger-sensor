using System;
using AirGestureAI.Models;

namespace AirGestureAI.GestureRecognition
{
    /// <summary>
    /// Event arguments containing detailed information about a gesture lifecycle event.
    /// </summary>
    public class GestureEventArgs : EventArgs
    {
        /// <summary>Gets the type of gesture.</summary>
        public GestureType Gesture { get; }

        /// <summary>Gets the gesture confidence score (0.0 to 1.0).</summary>
        public double Confidence { get; }

        /// <summary>Gets the state machine state at the time of the event.</summary>
        public EngineGestureState State { get; }

        /// <summary>Gets the UTC timestamp of the event.</summary>
        public DateTime Timestamp { get; } = DateTime.UtcNow;

        /// <summary>
        /// Initializes a new instance of <see cref="GestureEventArgs"/>.
        /// </summary>
        public GestureEventArgs(GestureType gesture, double confidence = 1.0, EngineGestureState state = EngineGestureState.Recognized)
        {
            Gesture = gesture;
            Confidence = confidence;
            State = state;
        }

        /// <inheritdoc/>
        public override string ToString() => $"[GestureEvent {Gesture} confidence={Confidence:P0} state={State}]";
    }
}
