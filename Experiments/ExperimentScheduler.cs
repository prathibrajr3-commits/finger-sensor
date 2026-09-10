using System;

namespace AirGestureAI.Experiments
{
    /// <summary>
    /// Schedules and determines active dates and eligibility windows for research experiments.
    /// </summary>
    public class ExperimentScheduler
    {
        /// <summary>
        /// Checks if the experiment is currently active based on the system date.
        /// </summary>
        public bool IsExperimentActive(ExperimentProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            var now = DateTime.UtcNow;
            return now >= profile.StartTimeUtc && now <= profile.EndTimeUtc;
        }
    }
}
