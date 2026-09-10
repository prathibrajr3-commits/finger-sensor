using System;

namespace AirGestureAI.Experiments
{
    /// <summary>
    /// Represents an experimental feature flag toggled dynamically.
    /// </summary>
    public class FeatureFlag
    {
        /// <summary>Gets or sets the feature flag unique key.</summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>Gets or sets whether this feature is enabled.</summary>
        public bool IsEnabled { get; set; }

        /// <summary>Gets or sets the active variant label (e.g. "Control", "Treatment_A").</summary>
        public string Variant { get; set; } = "Control";
    }
}
