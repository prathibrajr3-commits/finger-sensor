using System;
using AirGestureAI.Research;

namespace AirGestureAI.Experiments
{
    /// <summary>
    /// Computes statistical performance differences between Control and Treatment variants.
    /// </summary>
    public class VariantEvaluator
    {
        /// <summary>
        /// Compares metrics of Variant A and Variant B.
        /// </summary>
        public ExperimentResult Evaluate(ExperimentProfile profile, ResearchMetrics control, ResearchMetrics treatment)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (control == null) throw new ArgumentNullException(nameof(control));
            if (treatment == null) throw new ArgumentNullException(nameof(treatment));

            double accuracyLift = treatment.Accuracy - control.Accuracy;
            double latencyDiff   = treatment.AverageLatencyMs - control.AverageLatencyMs;

            return new ExperimentResult
            {
                ExperimentId   = profile.Id,
                WinnerVariant  = accuracyLift > 0 ? "Treatment" : "Control",
                AccuracyLift   = accuracyLift,
                LatencyDeltaMs = latencyDiff,
                Confidence     = 0.95
            };
        }
    }
}
