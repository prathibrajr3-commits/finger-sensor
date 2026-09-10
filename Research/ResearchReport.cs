using System;
using System.Text;

namespace AirGestureAI.Research
{
    /// <summary>
    /// Utility class that handles compiling research results and exporting them in multiple formats.
    /// </summary>
    public class ResearchReport
    {
        private readonly ResearchProject _project;
        private readonly ResearchMetrics _metrics;

        /// <summary>
        /// Initializes a new instance of the <see cref="ResearchReport"/> class.
        /// </summary>
        public ResearchReport(ResearchProject project, ResearchMetrics metrics)
        {
            _project = project ?? throw new ArgumentNullException(nameof(project));
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        }

        /// <summary>
        /// Generates a JSON representation of the research metrics.
        /// </summary>
        public string ToJson()
        {
            return $$"""
            {
              "projectId": "{{_project.Id}}",
              "projectName": "{{_project.Name}}",
              "description": "{{_project.Description}}",
              "timestamp": "{{DateTime.UtcNow:o}}",
              "metrics": {
                "accuracy": {{_metrics.Accuracy:F4}},
                "precision": {{_metrics.Precision:F4}},
                "recall": {{_metrics.Recall:F4}},
                "f1Score": {{_metrics.F1Score:F4}},
                "averageLatencyMs": {{_metrics.AverageLatencyMs:F2}},
                "memoryOverheadMb": {{_metrics.MemoryOverheadMb:F2}}
              }
            }
            """;
        }

        /// <summary>
        /// Generates a CSV representation of the research metrics.
        /// </summary>
        public string ToCsv()
        {
            var sb = new StringBuilder();
            sb.AppendLine("ProjectId,ProjectName,Timestamp,Accuracy,Precision,Recall,F1Score,LatencyMs,MemoryMb");
            sb.AppendLine($"{_project.Id},{_project.Name},{DateTime.UtcNow:o},{_metrics.Accuracy:F4},{_metrics.Precision:F4},{_metrics.Recall:F4},{_metrics.F1Score:F4},{_metrics.AverageLatencyMs:F2},{_metrics.MemoryOverheadMb:F2}");
            return sb.ToString();
        }

        /// <summary>
        /// Generates an HTML representation of the research metrics.
        /// </summary>
        public string ToHtml()
        {
            return $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="UTF-8">
                <title>Research Project Report - {{_project.Name}}</title>
                <style>
                    body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #121212; color: #E0E0E0; margin: 40px; }
                    h1 { color: #0D6EFD; }
                    table { border-collapse: collapse; width: 100%; margin-top: 20px; background-color: #1E1E1E; }
                    th, td { padding: 12px; text-align: left; border-bottom: 1px solid #333; }
                    th { background-color: #2A2A2A; color: #0D6EFD; }
                    .highlight { font-weight: bold; color: #22C55E; }
                </style>
            </head>
            <body>
                <h1>Research Report: {{_project.Name}}</h1>
                <p><strong>Description:</strong> {{_project.Description}}</p>
                <p><strong>Date Compiled:</strong> {{DateTime.UtcNow}} UTC</p>
                <p><strong>Project ID:</strong> {{_project.Id}}</p>

                <h2>Performance Evaluation Metrics</h2>
                <table>
                    <thead>
                        <tr>
                            <th>Metric</th>
                            <th>Value</th>
                        </tr>
                    </thead>
                    <tbody>
                        <tr><td>Accuracy</td><td class="highlight">{{_metrics.Accuracy * 100:F2}}%</td></tr>
                        <tr><td>Precision</td><td>{{_metrics.Precision:F4}}</td></tr>
                        <tr><td>Recall</td><td>{{_metrics.Recall:F4}}</td></tr>
                        <tr><td>F1-Score</td><td class="highlight">{{_metrics.F1Score:F4}}</td></tr>
                        <tr><td>Average Latency</td><td>{{_metrics.AverageLatencyMs:F2}} ms</td></tr>
                        <tr><td>Memory Footprint</td><td>{{_metrics.MemoryOverheadMb:F2}} MB</td></tr>
                    </tbody>
                </table>
            </body>
            </html>
            """;
        }
    }
}
