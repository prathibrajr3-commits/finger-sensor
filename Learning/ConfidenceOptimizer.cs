using System;

namespace AirGestureAI.Learning
{
    /// <summary>
    /// Calculates personalized activation thresholds based on confidence histograms to minimize false triggers.
    /// </summary>
    public class ConfidenceOptimizer
    {
        /// <summary>
        /// Computes optimal confidence threshold adjustments based on false-positive rates.
        /// </summary>
        public double Optimize(string gesture, double currentThreshold, double falseTriggerRate)
        {
            if (falseTriggerRate > 0.10)
            {
                // Too many false triggers: increase the threshold to make it more restrictive
                return Math.Min(currentThreshold + 0.05, 0.95);
            }
            if (falseTriggerRate < 0.02)
            {
                // Extremely safe: decrease threshold slightly to increase responsiveness
                return Math.Max(currentThreshold - 0.03, 0.35);
            }
            return currentThreshold;
        }
    }
}
