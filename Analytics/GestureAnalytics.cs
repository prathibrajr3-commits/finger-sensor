using System;
using System.Collections.Generic;

namespace AirGestureAI.Analytics
{
    /// <summary>
    /// Tracks gesture activation frequencies, accuracy distributions, and confidence drift history.
    /// </summary>
    public class GestureAnalytics
    {
        private readonly Dictionary<string, int> _counts = new();
        private readonly List<double> _confidenceHistory = new();

        /// <summary>Gets the dictionary containing all registered gesture counts.</summary>
        public IReadOnlyDictionary<string, int> Counts => _counts;

        /// <summary>Gets the historical confidence list.</summary>
        public IReadOnlyList<double> ConfidenceHistory => _confidenceHistory;

        /// <summary>
        /// Registers a gesture trigger event for frequency tracking.
        /// </summary>
        public void LogGesture(string gestureName, double confidence)
        {
            if (string.IsNullOrEmpty(gestureName)) return;

            if (!_counts.ContainsKey(gestureName))
                _counts[gestureName] = 0;
            
            _counts[gestureName]++;
            _confidenceHistory.Add(confidence);

            // Limit history size to prevent memory leaks
            if (_confidenceHistory.Count > 1000)
            {
                _confidenceHistory.RemoveAt(0);
            }
        }
    }
}
