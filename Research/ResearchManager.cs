using System;
using System.Collections.Generic;
using System.IO;
using AirGestureAI.Utilities;

namespace AirGestureAI.Research
{
    /// <summary>
    /// Coordinates loaded research projects, dataset recording sessions, and generates consolidated research packs.
    /// </summary>
    public sealed class ResearchManager
    {
        private readonly List<ResearchProject> _projects = new();
        private readonly ResearchDataset _currentDataset = new();

        /// <summary>Gets the currently active dataset session.</summary>
        public ResearchDataset CurrentDataset => _currentDataset;

        /// <summary>
        /// Initializes a new instance of the <see cref="ResearchManager"/> class.
        /// </summary>
        public ResearchManager()
        {
            // Register a default project
            _projects.Add(new ResearchProject
            {
                Name        = "On-Device Neural Adaptation",
                Description = "Tracks baseline drift in hand-landmark coordinate offsets across various ambient light environments."
            });
        }

        /// <summary>
        /// Retrieves all registered research projects.
        /// </summary>
        public IReadOnlyList<ResearchProject> GetProjects() => _projects;

        /// <summary>
        /// Evaluates current metrics and exports a complete research pack (JSON, CSV, HTML) to the workspace directory.
        /// </summary>
        public string ExportResearchPack(string outputDirectory, ResearchMetrics metrics)
        {
            Directory.CreateDirectory(outputDirectory);
            var project = _projects[0];
            var report  = new ResearchReport(project, metrics);

            var jsonPath = Path.Combine(outputDirectory, "research_metrics.json");
            var csvPath  = Path.Combine(outputDirectory, "research_metrics.csv");
            var htmlPath = Path.Combine(outputDirectory, "research_dashboard.html");

            File.WriteAllText(jsonPath, report.ToJson());
            File.WriteAllText(csvPath, report.ToCsv());
            File.WriteAllText(htmlPath, report.ToHtml());

            Logger.Info($"ResearchManager: Exported research package to '{outputDirectory}'");
            return htmlPath;
        }
    }
}
