using System;

namespace AirGestureAI.XR
{
    /// <summary>
    /// Represents a point in physical 3D coordinate space mapped by depth sensors or XR cameras.
    /// </summary>
    public struct SpatialCoordinate : IEquatable<SpatialCoordinate>
    {
        /// <summary>Gets the X position in meters.</summary>
        public double X { get; init; }

        /// <summary>Gets the Y position in meters.</summary>
        public double Y { get; init; }

        /// <summary>Gets the Z position (depth) in meters.</summary>
        public double Z { get; init; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SpatialCoordinate"/> struct.
        /// </summary>
        public SpatialCoordinate(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <inheritdoc/>
        public bool Equals(SpatialCoordinate other) =>
            Math.Abs(X - other.X) < 0.0001 && Math.Abs(Y - other.Y) < 0.0001 && Math.Abs(Z - other.Z) < 0.0001;

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is SpatialCoordinate other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(X, Y, Z);

        /// <summary>Compares two coordinate vectors.</summary>
        public static bool operator ==(SpatialCoordinate left, SpatialCoordinate right) => left.Equals(right);
        /// <summary>Compares two coordinate vectors.</summary>
        public static bool operator !=(SpatialCoordinate left, SpatialCoordinate right) => !left.Equals(right);
    }
}
