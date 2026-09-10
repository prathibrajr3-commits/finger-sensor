using System;
using AirGestureAI.Configuration;
using AirGestureAI.Models;

namespace AirGestureAI.GestureRecognition
{
    /// <summary>
    /// Validates candidate gestures for confidence, noise filtering, and temporal stability.
    /// </summary>
    public sealed class GestureValidator
    {
        private readonly AppConfig _config;

        private GestureType _candidateType = GestureType.None;
        private DateTime _candidateStartTime = DateTime.MinValue;

        /// <summary>
        /// Initializes a new instance of <see cref="GestureValidator"/>.
        /// </summary>
        public GestureValidator(AppConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Resets the validator state.
        /// </summary>
        public void Reset()
        {
            _candidateType = GestureType.None;
            _candidateStartTime = DateTime.MinValue;
        }

        /// <summary>
        /// Validates a candidate gesture result. Returns true if the gesture has remained stable for the required duration.
        /// </summary>
        public bool Validate(GestureResult result, HandFeatures features, out double elapsedMs)
        {
            elapsedMs = 0;

            if (result == null || result.Gesture == GestureType.None
                || !features.IsHandDetected
                || features.Confidence < _config.GestureMinConfidence
                || result.Confidence  < _config.GestureMinConfidence)
            {
                Reset();
                return false;
            }

            var now = DateTime.UtcNow;

            // Gesture change check
            if (result.Gesture != _candidateType)
            {
                _candidateType = result.Gesture;
                _candidateStartTime = now;
                return false;
            }

            // Accumulate duration for the same candidate gesture
            elapsedMs = (now - _candidateStartTime).TotalMilliseconds;

            // Check stability duration threshold (e.g., 200 ms)
            if (elapsedMs >= _config.GestureStabilityMs)
            {
                return true;
            }

            return false;
        }
    }
}
