using System;
using System.Collections.Generic;

namespace AirGestureAI.Learning
{
    /// <summary>
    /// Holds personalized weights, gesture thresholds, and baseline calibration coordinate sets for an individual user.
    /// </summary>
    public class LearningProfile
    {
        /// <summary>Gets or sets the username or profile identifier.</summary>
        public string ProfileName { get; set; } = "DefaultUser";

        /// <summary>Gets or sets custom activation thresholds mapped by gesture name.</summary>
        public Dictionary<string, double> PersonalizedThresholds { get; set; } = new();

        /// <summary>Gets or sets custom coordinate weight offsets adjusted during on-device training.</summary>
        public List<double> FeatureWeights { get; set; } = new();

        /// <summary>Gets or sets when this profile was last retrained.</summary>
        public DateTime LastTrainedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Initializes a new instance of the <see cref="LearningProfile"/> class.
        /// </summary>
        public LearningProfile()
        {
            // Set up baseline defaults
            PersonalizedThresholds["ScrollUp"]   = 0.50;
            PersonalizedThresholds["ScrollDown"] = 0.50;
            PersonalizedThresholds["OpenPalm"]   = 0.75;

            // Mock weights for hand landmarks (21 points * 3 coords = 63 weights)
            for (int i = 0; i < 63; i++)
            {
                FeatureWeights.Add(1.0);
            }
        }
    }
}
