using System;
using AirGestureAI.Models;

namespace AirGestureAI.GestureRecognition
{
    /// <summary>
    /// Holds candidate gesture classification result produced by the feature extractor.
    /// </summary>
    public sealed class GestureResult
    {
        /// <summary>Gets the candidate gesture type.</summary>
        public GestureType Gesture { get; }

        /// <summary>Gets the classification confidence score (0.0 to 1.0).</summary>
        public double Confidence { get; }

        /// <summary>Gets a human-readable explanation of why this gesture was classified.</summary>
        public string Reason { get; }

        /// <summary>Gets the timestamp of classification.</summary>
        public DateTime Timestamp { get; } = DateTime.UtcNow;

        /// <summary>
        /// Initializes a new instance of <see cref="GestureResult"/>.
        /// </summary>
        public GestureResult(GestureType gesture, double confidence, string reason)
        {
            Gesture = gesture;
            Confidence = confidence;
            Reason = reason;
        }

        /// <summary>Constant representing no candidate gesture.</summary>
        public static GestureResult None { get; } = new GestureResult(GestureType.None, 0.0, "No gesture candidate");

        /// <inheritdoc/>
        public override string ToString() => $"[GestureResult {Gesture} ({Confidence:P0}): {Reason}]";
    }
}
