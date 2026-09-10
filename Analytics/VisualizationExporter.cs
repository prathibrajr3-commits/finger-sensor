using System;
using System.IO;
using System.Text;

namespace AirGestureAI.Analytics
{
    /// <summary>
    /// Formats aggregated telemetry and exports interactive visual reports.
    /// </summary>
    public class VisualizationExporter
    {
        /// <summary>
        /// Generates a CSS-styled HTML analytics dashboard report and saves it to a file.
        /// </summary>
        public void ExportHtmlDashboard(string path, PerformanceAnalytics performance, GestureAnalytics gestures)
        {
            if (performance == null) throw new ArgumentNullException(nameof(performance));
            if (gestures == null) throw new ArgumentNullException(nameof(gestures));

            var sb = new StringBuilder();
            sb.Append("<h2>Active Gesture Frequency</h2><ul>");
            foreach (var kvp in gestures.Counts)
            {
                sb.Append($"<li><strong>{kvp.Key}</strong>: {kvp.Value} triggers</li>");
            }
            sb.Append("</ul>");

            var html = $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="UTF-8">
                <title>AirGesture AI - Analytics Dashboard</title>
                <style>
                    body { font-family: 'Segoe UI', sans-serif; background-color: #121212; color: #E0E0E0; margin: 40px; }
                    h1 { color: #0D6EFD; border-bottom: 2px solid #333; padding-bottom: 10px; }
                    .card { background-color: #1E1E1E; padding: 20px; border-radius: 8px; margin-bottom: 20px; box-shadow: 0 4px 6px rgba(0,0,0,0.3); }
                    .metric { font-size: 24px; color: #22C55E; font-weight: bold; }
                </style>
            </head>
            <body>
                <h1>AirGesture AI Telemetry & Usage Analytics</h1>
                <div class="card">
                    <h2>Session Summary</h2>
                    <p>Report Generated: {{DateTime.UtcNow}} UTC</p>
                </div>
                <div class="card">
                    {{sb}}
                </div>
                <div class="card">
                    <h2>Performance Benchmarks</h2>
                    <p>Logged FPS datapoints: {{performance.FpsHistory.Count}}</p>
                    <p>Logged CPU datapoints: {{performance.CpuHistory.Count}}</p>
                </div>
            </body>
            </html>
            """;

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, html, Encoding.UTF8);
        }
    }
}
