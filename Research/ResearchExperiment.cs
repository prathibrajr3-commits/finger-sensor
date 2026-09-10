using System;

namespace AirGestureAI.Research
{
    /// <summary>
    /// Represents an active research experiment configuration testing specific parameter variants.
    /// </summary>
    public class ResearchExperiment
    {
        /// <summary>Gets the unique ID of the experiment.</summary>
        public Guid Id { get; init; } = Guid.NewGuid();

        /// <summary>Gets or sets the research project ID this experiment belongs to.</summary>
        public Guid ProjectId { get; set; }

        /// <summary>Gets or sets the experimental variant name (e.g. "Variant_A", "Variant_B").</summary>
        public string Variant { get; set; } = "Control";

        /// <summary>Gets or sets parameters under test, serialized as key-value configurations.</summary>
        public string ParametersJson { get; set; } = "{}";

        /// <summary>Gets or sets the start date of the experiment.</summary>
        public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>Gets or sets the status of the experiment.</summary>
        public bool IsCompleted { get; set; }
    }
}
