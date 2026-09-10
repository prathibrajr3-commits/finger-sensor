using System;
using System.Collections.Generic;

namespace AirGestureAI.Analytics
{
    /// <summary>
    /// Analyzes coordinate prediction confidence and detects anomaly clusters (e.g. tracking drift or sudden loss).
    /// </summary>
    public class PredictionAnalytics
    {
        private int _anomalyCount;

        /// <summary>Gets the current count of detected prediction anomalies.</summary>
        public int AnomalyCount => _anomalyCount;

        /// <summary>
        /// Analyzes gesture confidence and increments the anomaly tracker if confidence drops below threshold.
        /// </summary>
        public bool AnalyzeConfidence(double confidence)
        {
            if (confidence > 0.0 && confidence < 0.35)
            {
                _anomalyCount++;
                return true; // Anomaly detected (suspiciously low confidence)
            }
            return false;
        }

        /// <summary>
        /// Resets the anomaly counter.
        /// </summary>
        public void Reset()
        {
            _anomalyCount = 0;
        }
    }
}
