using System.Windows;

namespace AirGestureAI.GestureRecognition
{
    /// <summary>
    /// Holds features extracted from hand landmarks for gesture classification.
    /// </summary>
    public sealed class HandFeatures
    {
        /// <summary>Gets a value indicating whether hand landmarks are detected.</summary>
        public bool IsHandDetected { get; set; }

        /// <summary>Gets or sets the palm center position in 2D coordinates (0.0-1.0).</summary>
        public Point PalmCenter { get; set; }

        /// <summary>Gets or sets the wrist position in 2D coordinates (0.0-1.0).</summary>
        public Point Wrist { get; set; }

        /// <summary>Gets or sets vertical hand velocity (negative = moving up, positive = moving down).</summary>
        public double VelocityY { get; set; }

        /// <summary>Gets or sets horizontal hand velocity (negative = left, positive = right).</summary>
        public double VelocityX { get; set; }

        /// <summary>Gets or sets overall hand speed magnitude.</summary>
        public double MotionMagnitude { get; set; }

        /// <summary>Gets or sets whether the thumb is extended.</summary>
        public bool IsThumbExtended { get; set; }

        /// <summary>Gets or sets whether the index finger is extended.</summary>
        public bool IsIndexExtended { get; set; }

        /// <summary>Gets or sets whether the middle finger is extended.</summary>
        public bool IsMiddleExtended { get; set; }

        /// <summary>Gets or sets whether the ring finger is extended.</summary>
        public bool IsRingExtended { get; set; }

        /// <summary>Gets or sets whether the pinky finger is extended.</summary>
        public bool IsPinkyExtended { get; set; }

        /// <summary>Gets or sets score (0.0 to 1.0) indicating open palm posture likelihood.</summary>
        public double OpenPalmScore { get; set; }

        /// <summary>Gets or sets score (0.0 to 1.0) indicating motion stability (1.0 = smooth/steady, 0.0 = noisy/jittery).</summary>
        public double StabilityScore { get; set; }

        /// <summary>Gets or sets tracking confidence (0.0 to 1.0).</summary>
        public double Confidence { get; set; }
    }
}
