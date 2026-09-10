using System;

namespace AirGestureAI.XR
{
    /// <summary>
    /// Represents a processed frame containing pixel depth measurements.
    /// </summary>
    public class DepthFrame
    {
        /// <summary>Gets the frame width in pixels.</summary>
        public int Width { get; init; } = 320;

        /// <summary>Gets the frame height in pixels.</summary>
        public int Height { get; init; } = 240;

        /// <summary>Gets the timestamp of the depth frame.</summary>
        public long Timestamp { get; init; } = DateTime.UtcNow.Ticks;

        /// <summary>
        /// Retrieves the depth value at a specific pixel coordinate.
        /// </summary>
        public double GetDepthAt(int x, int y)
        {
            // Simulate distance from camera (in meters)
            double distance = 0.5 + (0.005 * x) - (0.002 * y);
            return Math.Clamp(distance, 0.10, 5.0);
        }
    }
}
