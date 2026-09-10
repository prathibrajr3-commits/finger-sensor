using System;

namespace AirGestureAI.XR
{
    /// <summary>
    /// Represents a persistent spatial reference anchor mapping virtual objects to real-world coordinates.
    /// </summary>
    public class SpatialAnchor
    {
        /// <summary>Gets the unique ID of the spatial anchor.</summary>
        public Guid Id { get; init; } = Guid.NewGuid();

        /// <summary>Gets or sets the display name or label of the anchor.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Gets or sets the coordinate vector.</summary>
        public SpatialCoordinate Coordinate { get; set; }

        /// <summary>Gets or sets the tracking confidence rating (0.0 to 1.0).</summary>
        public double TrackingConfidence { get; set; } = 1.0;
    }
}
