using System;
using System.Collections.Generic;
using AirGestureAI.Research;

namespace AirGestureAI.Experiments
{
    /// <summary>
    /// Manages active feature flags, schedules experiments, and collects variant outcomes.
    /// </summary>
    public sealed class ExperimentManager
    {
        private readonly List<ExperimentProfile> _activeExperiments = new();
        private readonly Dictionary<string, FeatureFlag> _flags = new();
        private readonly VariantEvaluator _evaluator = new();

        /// <summary>Gets the list of active experiments.</summary>
        public IReadOnlyList<ExperimentProfile> ActiveExperiments => _activeExperiments;

        /// <summary>Gets the dictionary of registered feature flags.</summary>
        public IReadOnlyDictionary<string, FeatureFlag> Flags => _flags;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExperimentManager"/> class.
        /// </summary>
        public ExperimentManager()
        {
            // Register default flags
            _flags["EnhancedSmoothing"] = new FeatureFlag { Key = "EnhancedSmoothing", IsEnabled = true, Variant = "Treatment_A" };
            _flags["MultiCameraSupport"] = new FeatureFlag { Key = "MultiCameraSupport", IsEnabled = false, Variant = "Control" };

            // Register default experiment
            _activeExperiments.Add(new ExperimentProfile
            {
                Name            = "Double Exponential vs Kalmam Filter",
                AllocationRatio = 0.50
            });
        }

        /// <summary>
        /// Resolves whether a feature key is enabled.
        /// </summary>
        public bool IsFeatureEnabled(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;
            return _flags.TryGetValue(key, out var flag) && flag.IsEnabled;
        }

        /// <summary>
        /// Evaluates control metrics against treatment metrics and returns an evaluation result.
        /// </summary>
        public ExperimentResult EvaluateExperiment(int index, ResearchMetrics control, ResearchMetrics treatment)
        {
            if (index < 0 || index >= _activeExperiments.Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            return _evaluator.Evaluate(_activeExperiments[index], control, treatment);
        }
    }
}
