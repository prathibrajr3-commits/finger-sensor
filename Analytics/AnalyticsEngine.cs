using System;
using System.IO;
using AirGestureAI.Models;

namespace AirGestureAI.Analytics
{
    /// <summary>
    /// Central manager for app performance monitoring and analytics reports exports.
    /// </summary>
    public sealed class AnalyticsEngine
    {
        private readonly UsageCollector _usage = new();
        private readonly GestureAnalytics _gestures = new();
        private readonly PerformanceAnalytics _performance = new();
        private readonly PredictionAnalytics _predictions = new();
        private readonly VisualizationExporter _exporter = new();

        /// <summary>Gets the usage statistics collector.</summary>
        public UsageCollector Usage => _usage;

        /// <summary>Gets the gesture statistics collector.</summary>
        public GestureAnalytics Gestures => _gestures;

        /// <summary>Gets the performance logs collector.</summary>
        public PerformanceAnalytics Performance => _performance;

        /// <summary>Gets the prediction logs collector.</summary>
        public PredictionAnalytics Predictions => _predictions;

        /// <summary>
        /// Collects current system usage landmarks.
        /// </summary>
        public void CollectUsage(HandData handData)
        {
            if (handData == null) return;
            // Simple mock collection: register metrics
            double mockCpu = 1.2 + (Random.Shared.NextDouble() * 3.0);
            double mockRam = 145.0 + (Random.Shared.NextDouble() * 15.0);
            double mockLatency = 4.0 + (Random.Shared.NextDouble() * 2.0);
            double mockFps = 30.0 + (Random.Shared.NextDouble() * 2.0);

            _performance.LogMetrics(mockCpu, mockRam, mockLatency, mockFps);
        }

        /// <summary>
        /// Registers a gesture trigger event.
        /// </summary>
        public void CollectGesture(GestureType gesture)
        {
            double mockConfidence = 0.65 + (Random.Shared.NextDouble() * 0.30);
            _gestures.LogGesture(gesture.ToString(), mockConfidence);
            _predictions.AnalyzeConfidence(mockConfidence);
        }

        /// <summary>
        /// Exports analytics files (JSON, CSV, HTML) to the workspace directory.
        /// </summary>
        public string ExportReports(string outputDirectory)
        {
            Directory.CreateDirectory(outputDirectory);

            var htmlPath = Path.Combine(outputDirectory, "analytics_dashboard.html");
            var jsonPath = Path.Combine(outputDirectory, "analytics_data.json");
            var csvPath  = Path.Combine(outputDirectory, "analytics_history.csv");

            _exporter.ExportHtmlDashboard(htmlPath, _performance, _gestures);

            // Write JSON mock
            File.WriteAllText(jsonPath, $$"""
            {
              "sessionDurationSeconds": {{_usage.CurrentSessionDurationSeconds:F2}},
              "totalSessions": {{_usage.TotalSessions}},
              "anomaliesCount": {{_predictions.AnomalyCount}}
            }
            """);

            // Write CSV mock
            File.WriteAllText(csvPath, "Metric,Value\nSessionDurationSeconds," + _usage.CurrentSessionDurationSeconds.ToString("F2") + "\nTotalSessions," + _usage.TotalSessions + "\nAnomaliesCount," + _predictions.AnomalyCount + "\n");

            return htmlPath;
        }
    }
}
