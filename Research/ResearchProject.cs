using System;

namespace AirGestureAI.Research
{
    /// <summary>
    /// Represents a research study or project testing gesture modifications, new neural topologies, or user adaptation models.
    /// </summary>
    public class ResearchProject
    {
        /// <summary>Gets the unique identifier of the research project.</summary>
        public Guid Id { get; init; } = Guid.NewGuid();

        /// <summary>Gets or sets the display name of the research project.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Gets or sets the detailed description of the project goals.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Gets or sets the creation date in UTC.</summary>
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>Gets or sets the active status of the project.</summary>
        public bool IsActive { get; set; } = true;
    }
}
