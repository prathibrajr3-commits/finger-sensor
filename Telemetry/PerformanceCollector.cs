using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using AirGestureAI.Utilities;

namespace AirGestureAI.Telemetry
{
    /// <summary>
    /// Collects and persists offline performance statistics (CPU, RAM, latency, FPS).
    /// No data ever leaves the local machine.
    /// </summary>
    public sealed class PerformanceCollector
    {
        private readonly string _outputDirectory;
        private readonly List<PerformanceSample> _samples = new();
        private readonly object _lock = new object();

        /// <summary>
        /// Initializes a new <see cref="PerformanceCollector"/>.
        /// </summary>
        /// <param name="outputDirectory">Directory where telemetry JSON files are saved.</param>
        public PerformanceCollector(string outputDirectory)
        {
            _outputDirectory = outputDirectory ?? throw new ArgumentNullException(nameof(outputDirectory));
            Directory.CreateDirectory(outputDirectory);
        }

        /// <summary>
        /// Records a single performance sample.
        /// </summary>
        public void Record(double cpuPercent, double ramMb, double latencyMs, double fps)
        {
            lock (_lock)
            {
                _samples.Add(new PerformanceSample
                {
                    TimestampUtc = DateTime.UtcNow,
                    CpuPercent   = cpuPercent,
                    RamMb        = ramMb,
                    LatencyMs    = latencyMs,
                    Fps          = fps,
                });

                // Keep last 1000 samples in memory
                if (_samples.Count > 1000) _samples.RemoveAt(0);
            }
        }

        /// <summary>
        /// Returns a read-only snapshot of the current in-memory samples.
        /// </summary>
        public IReadOnlyList<PerformanceSample> GetSamples()
        {
            lock (_lock) { return _samples.ToArray(); }
        }

        /// <summary>
        /// Flushes all in-memory samples to a JSON file on disk.
        /// </summary>
        public void Flush()
        {
            PerformanceSample[] snapshot;
            lock (_lock)
            {
                snapshot = _samples.ToArray();
                _samples.Clear();
            }

            if (snapshot.Length == 0) return;

            var path = Path.Combine(_outputDirectory,
                $"perf_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json");

            File.WriteAllText(path, JsonSerializer.Serialize(snapshot,
                new JsonSerializerOptions { WriteIndented = false }));

            Logger.Info($"PerformanceCollector: Flushed {snapshot.Length} sample(s) → '{path}'");
        }
    }

    /// <summary>A single recorded performance data point.</summary>
    public sealed class PerformanceSample
    {
        public DateTime TimestampUtc { get; init; }
        public double   CpuPercent   { get; init; }
        public double   RamMb        { get; init; }
        public double   LatencyMs    { get; init; }
        public double   Fps          { get; init; }
    }
}
