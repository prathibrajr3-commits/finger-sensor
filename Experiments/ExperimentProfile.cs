using System;

namespace AirGestureAI.Experiments
{
    /// <summary>
    /// Configures hypothesis, duration parameters, and metrics parameters for an A/B test.
    /// </summary>
    public class ExperimentProfile
    {
        /// <summary>Gets the unique ID of the experiment profile.</summary>
        public Guid Id { get; init; } = Guid.NewGuid();

        /// <summary>Gets or sets the experiment name.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Gets or sets the allocation ratio (e.g. 0.50 means 50% treatment).</summary>
        public double AllocationRatio { get; set; } = 0.50;

        /// <summary>Gets or sets when this experiment starts.</summary>
        public DateTime StartTimeUtc { get; set; } = DateTime.UtcNow;

        /// <summary>Gets or sets when this experiment concludes.</summary>
        public DateTime EndTimeUtc { get; set; } = DateTime.UtcNow.AddDays(7);
    }
}
