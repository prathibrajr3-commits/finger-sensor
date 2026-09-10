using System;
using System.Collections.Generic;

namespace AirGestureAI.XR
{
    /// <summary>
    /// Controls augmented/virtual reality spatial session states and device mappings.
    /// </summary>
    public sealed class XRSession
    {
        private bool _isActive;
        private readonly List<SpatialAnchor> _anchors = new();

        /// <summary>Gets whether the spatial session is active.</summary>
        public bool IsActive => _isActive;

        /// <summary>Gets the list of active spatial anchors.</summary>
        public IReadOnlyList<SpatialAnchor> Anchors => _anchors;

        /// <summary>
        /// Initializes a new instance of the <see cref="XRSession"/> class.
        /// </summary>
        public XRSession()
        {
            // Register a baseline spatial anchor
            _anchors.Add(new SpatialAnchor { Name = "DesktopCenter", Coordinate = new SpatialCoordinate(0, 0, 0.6) });
        }

        /// <summary>
        /// Initializes the XR spatial session.
        /// </summary>
        public void StartSession()
        {
            _isActive = true;
        }

        /// <summary>
        /// Concludes the XR session.
        /// </summary>
        public void StopSession()
        {
            _isActive = false;
        }

        /// <summary>
        /// Registers a new spatial anchor in 3D space.
        /// </summary>
        public void CreateAnchor(string name, SpatialCoordinate coord)
        {
            _anchors.Add(new SpatialAnchor { Name = name, Coordinate = coord });
        }
    }
}
