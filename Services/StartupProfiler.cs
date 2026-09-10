using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using AirGestureAI.Utilities;

namespace AirGestureAI.Services
{
    /// <summary>
    /// Profiles the startup pipeline phases and generates a diagnostic report.
    /// </summary>
    public sealed class StartupProfiler
    {
        private readonly Stopwatch _totalStopwatch = Stopwatch.StartNew();
        private long _diBuildTimeMs;
        private long _mainWindowCreationTimeMs;
        private long _uiShownTimeMs;
        private long _aiLoadTimeMs;
        private long _pluginLoadTimeMs;
        private long _cameraInitializationTimeMs;
        private readonly string _reportPath;

        /// <summary>
        /// Initializes a new instance of <see cref="StartupProfiler"/>.
        /// </summary>
        /// <param name="baseDirectory">The application base directory for diagnostics.</param>
        public StartupProfiler(string baseDirectory)
        {
            _reportPath = Path.Combine(baseDirectory, "Diagnostics", "startup_report.json");
            Directory.CreateDirectory(Path.GetDirectoryName(_reportPath)!);
        }

        /// <summary>Records the Dependency Injection container build time.</summary>
        public void RecordDiBuild(long ms) => _diBuildTimeMs = ms;

        /// <summary>Records the MainWindow instance creation time.</summary>
        public void RecordMainWindowCreation(long ms) => _mainWindowCreationTimeMs = ms;

        /// <summary>Records the time taken until UI becomes responsive/shown.</summary>
        public void RecordUiShown(long ms) => _uiShownTimeMs = ms;

        /// <summary>Records the ONNX / AI Engine load latency.</summary>
        public void RecordAiLoad(long ms) => _aiLoadTimeMs = ms;

        /// <summary>Records the plugin discovery and registration load time.</summary>
        public void RecordPluginLoad(long ms) => _pluginLoadTimeMs = ms;

        /// <summary>Records the camera connection initialization time.</summary>
        public void RecordCameraInitialization(long ms) => _cameraInitializationTimeMs = ms;

        /// <summary>
        /// Finalizes the profiling session, stops the total stopwatch, and saves the JSON report.
        /// </summary>
        public void FinalizeProfile()
        {
            _totalStopwatch.Stop();
            var totalStartupTimeMs = _totalStopwatch.ElapsedMilliseconds;

            var report = new
            {
                Timestamp = DateTime.UtcNow.ToString("O"),
                DiBuildTimeMs = _diBuildTimeMs,
                MainWindowCreationTimeMs = _mainWindowCreationTimeMs,
                UiShownTimeMs = _uiShownTimeMs,
                AiLoadTimeMs = _aiLoadTimeMs,
                PluginLoadTimeMs = _pluginLoadTimeMs,
                CameraInitializationTimeMs = _cameraInitializationTimeMs,
                TotalStartupTimeMs = totalStartupTimeMs
            };

            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(report, options);
                File.WriteAllText(_reportPath, json);
                Logger.Info($"StartupProfiler: Saved startup report to '{_reportPath}' ({totalStartupTimeMs}ms total).");
            }
            catch (Exception ex)
            {
                Logger.Error("StartupProfiler: Failed to write startup report", ex);
            }
        }
    }
}
