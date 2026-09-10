using System;
using System.Collections.Generic;

namespace AirGestureAI.XR
{
    /// <summary>
    /// Processes hand coordinate paths and represents gesture motions in physical 3D space.
    /// </summary>
    public class SpatialGesture
    {
        /// <summary>Gets the name of the spatial gesture.</summary>
        public string GestureName { get; init; } = "SpatialSwipe";

        /// <summary>Gets the coordinate points along the gesture trajectory.</summary>
        public List<SpatialCoordinate> Path { get; init; } = new();

        /// <summary>
        /// Registers a path point to track 3D coordinates.
        /// </summary>
        public void AddPoint(SpatialCoordinate coord)
        {
            Path.Add(coord);
            if (Path.Count > 100) Path.RemoveAt(0);
        }
    }
}
