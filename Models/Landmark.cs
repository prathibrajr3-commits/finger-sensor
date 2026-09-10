namespace AirGestureAI.Models
{
    /// <summary>
    /// Represents a 3D coordinate point (normalized from 0.0 to 1.0) for a hand landmark.
    /// </summary>
    public class Landmark
    {
        /// <summary>
        /// Gets or sets the X coordinate (horizontal, 0.0 at left, 1.0 at right).
        /// </summary>
        public float X { get; set; }

        /// <summary>
        /// Gets or sets the Y coordinate (vertical, 0.0 at top, 1.0 at bottom).
        /// </summary>
        public float Y { get; set; }

        /// <summary>
        /// Gets or sets the Z coordinate (depth/distance from camera).
        /// </summary>
        public float Z { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Landmark"/> class.
        /// </summary>
        public Landmark() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="Landmark"/> class with specific coordinates.
        /// </summary>
        public Landmark(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }
}
