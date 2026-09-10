using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AirGestureAI.Utilities;

namespace AirGestureAI.Services
{
    /// <summary>
    /// Monitors and tracks resource allocations (Mats, Bitmaps, Tasks, Pipes, etc.)
    /// using WeakReferences to identify potential leaks and generate memory reports.
    /// </summary>
    public sealed class MemoryDiagnosticsService
    {
        private readonly string _reportPath;
        private readonly List<WeakReferenceHolder> _trackedObjects = new();
        private readonly object _lock = new();

        /// <summary>
        /// Initializes a new instance of <see cref="MemoryDiagnosticsService"/>.
        /// </summary>
        /// <param name="baseDirectory">Base folder path to store output reports.</param>
        public MemoryDiagnosticsService(string baseDirectory)
        {
            _reportPath = Path.Combine(baseDirectory, "Diagnostics", "memory_report.json");
            Directory.CreateDirectory(Path.GetDirectoryName(_reportPath)!);
        }

        /// <summary>
        /// Tracks an object with a descriptive tag.
        /// </summary>
        /// <param name="obj">The object instance to track.</param>
        /// <param name="tag">A descriptive tag identifying the category or source.</param>
        public void Track(object obj, string tag)
        {
            if (obj == null) return;
            lock (_lock)
            {
                // Avoid duplicate tracking of the exact same reference
                if (_trackedObjects.Any(h => h.Reference.Target == obj)) return;

                _trackedObjects.Add(new WeakReferenceHolder(obj, tag));
            }
        }

        /// <summary>
        /// Untracks an object, removing it from validation checks.
        /// </summary>
        /// <param name="obj">The object instance to untrack.</param>
        public void Untrack(object obj)
        {
            if (obj == null) return;
            lock (_lock)
            {
                _trackedObjects.RemoveAll(h => h.Reference.Target == obj);
            }
        }

        /// <summary>
        /// Captures a memory diagnostics report, analyzing leaks and writing to disk.
        /// </summary>
        public void GenerateReport()
        {
            lock (_lock)
            {
                // Cleanup dead references
                _trackedObjects.RemoveAll(h => !h.Reference.IsAlive);

                var categories = new Dictionary<string, int>();
                var leaks = new List<object>();

                var potentialLeaks = _trackedObjects
                    .Where(h => h.Reference.IsAlive && (DateTime.UtcNow - h.TrackedTime).TotalSeconds > 10)
                    .Select(h => new
                    {
                        h.TypeName,
                        h.Tag,
                        LifetimeSeconds = (DateTime.UtcNow - h.TrackedTime).TotalSeconds
                    })
                    .ToList();

                foreach (var h in _trackedObjects)
                {
                    if (categories.ContainsKey(h.Tag))
                        categories[h.Tag]++;
                    else
                        categories[h.Tag] = 1;
                }

                var report = new
                {
                    Timestamp = DateTime.UtcNow.ToString("O"),
                    TotalTrackedObjects = _trackedObjects.Count,
                    Categories = categories,
                    PotentialLeaksCount = potentialLeaks.Count,
                    PotentialLeaks = potentialLeaks
                };

                try
                {
                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    };
                    var json = JsonSerializer.Serialize(report, options);
                    File.WriteAllText(_reportPath, json);
                    Logger.Info($"MemoryDiagnostics: Saved memory report with {potentialLeaks.Count} potential leaks to '{_reportPath}'.");
                }
                catch (Exception ex)
                {
                    Logger.Error("MemoryDiagnostics: Failed to write memory report", ex);
                }
            }
        }
    }

    internal sealed class WeakReferenceHolder
    {
        public WeakReference Reference { get; }
        public string TypeName { get; }
        public string Tag { get; }
        public DateTime TrackedTime { get; }

        public WeakReferenceHolder(object obj, string tag)
        {
            Reference = new WeakReference(obj);
            TypeName = obj.GetType().FullName ?? "Unknown";
            Tag = tag;
            TrackedTime = DateTime.UtcNow;
        }
    }
}
