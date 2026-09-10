using System;
using System.Collections.Generic;

namespace AirGestureAI.XR
{
    /// <summary>
    /// Interfaces with physical XR or spatial runtimes to supply hand-tracking and coordinate maps.
    /// </summary>
    public class XRInputProvider
    {
        /// <summary>
        /// Retrieves the coordinates of tracked hand joints (e.g. wrist, index fingertip).
        /// </summary>
        public List<SpatialCoordinate> GetTrackedJoints()
        {
            // Simulate 3D hand landmarks
            return new List<SpatialCoordinate>
            {
                new SpatialCoordinate(0.0, 0.0, 0.5), // Wrist
                new SpatialCoordinate(0.02, 0.08, 0.48), // Index
                new SpatialCoordinate(-0.02, 0.07, 0.49) // Thumb
            };
        }
    }
}
