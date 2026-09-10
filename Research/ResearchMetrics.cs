using System;

namespace AirGestureAI.Research
{
    /// <summary>
    /// Represents standard metric outputs evaluated during gesture research and model benchmarking.
    /// </summary>
    public class ResearchMetrics
    {
        /// <summary>Gets or sets the percentage of correctly classified gestures.</summary>
        public double Accuracy { get; set; }

        /// <summary>Gets or sets the precision score (True Positives / (True Positives + False Positives)).</summary>
        public double Precision { get; set; }

        /// <summary>Gets or sets the recall score (True Positives / (True Positives + False Negatives)).</summary>
        public double Recall { get; set; }

        /// <summary>Gets or sets the F1-score (harmonic mean of precision and recall).</summary>
        public double F1Score => (Precision + Recall) > 0 ? 2 * (Precision * Recall) / (Precision + Recall) : 0;

        /// <summary>Gets or sets the average processing latency in milliseconds.</summary>
        public double AverageLatencyMs { get; set; }

        /// <summary>Gets or sets the memory overhead in megabytes.</summary>
        public double MemoryOverheadMb { get; set; }
    }
}
