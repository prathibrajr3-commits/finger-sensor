using System.Collections.Generic;
using System.Linq;

namespace AirGestureAI.Models
{
    /// <summary>
    /// Holds tracking data for a single hand, including specific landmarks.
    /// </summary>
    public class HandData
    {
        /// <summary>
        /// Gets or sets a value indicating whether a hand is currently detected.
        /// </summary>
        public bool IsDetected { get; set; }

        /// <summary>
        /// Gets or sets the wrist landmark.
        /// </summary>
        public Landmark? Wrist { get; set; }

        /// <summary>
        /// Gets or sets the thumb tip landmark.
        /// </summary>
        public Landmark? ThumbTip { get; set; }

        /// <summary>
        /// Gets or sets the index fingertip landmark.
        /// </summary>
        public Landmark? IndexTip { get; set; }

        /// <summary>
        /// Gets or sets the palm center landmark.
        /// </summary>
        public Landmark? PalmCenter { get; set; }

        /// <summary>
        /// Gets or sets the list of all hand landmarks (if fully tracked).
        /// </summary>
        public List<Landmark> Landmarks { get; set; } = new List<Landmark>();
    }
}
