using System;
using System.Collections.Generic;

namespace AirGestureAI.Analytics
{
    /// <summary>
    /// Tracks usage statistics, such as session durations and active application windows.
    /// </summary>
    public class UsageCollector
    {
        private readonly DateTime _startTime = DateTime.UtcNow;
        private int _totalSessions = 1;

        /// <summary>Gets the current session duration in seconds.</summary>
        public double CurrentSessionDurationSeconds => (DateTime.UtcNow - _startTime).TotalSeconds;

        /// <summary>Gets the total sessions opened in the current lifecycle.</summary>
        public int TotalSessions => _totalSessions;

        /// <summary>
        /// Increments the session count tracker.
        /// </summary>
        public void RegisterSessionStart()
        {
            _totalSessions++;
        }
    }
}
