using System;

namespace AirGestureAI.Experiments
{
    /// <summary>
    /// Holds the statistical evaluation summary of a concluded experiment.
    /// </summary>
    public class ExperimentResult
    {
        /// <summary>Gets or sets the experiment ID.</summary>
        public Guid ExperimentId { get; set; }

        /// <summary>Gets or sets the winning variant description.</summary>
        public string WinnerVariant { get; set; } = "Control";

        /// <summary>Gets or sets the accuracy lift metric.</summary>
        public double AccuracyLift { get; set; }

        /// <summary>Gets or sets the latency differential in milliseconds.</summary>
        public double LatencyDeltaMs { get; set; }

        /// <summary>Gets or sets the statistical confidence level (0.0 to 1.0).</summary>
        public double Confidence { get; set; }
    }
}
