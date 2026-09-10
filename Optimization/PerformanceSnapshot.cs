using System;
using System.Diagnostics;
using AirGestureAI.Utilities;

namespace AirGestureAI.Optimization
{
    /// <summary>
    /// Captures a point-in-time performance snapshot including CPU, RAM, GC stats,
    /// and thread counts. Used to feed the Release Center performance dashboard.
    /// </summary>
    public sealed class PerformanceSnapshot
    {
        /// <summary>Gets the UTC time this snapshot was captured.</summary>
        public DateTime CapturedAtUtc { get; private init; }

        /// <summary>Gets the working set memory in MB at snapshot time.</summary>
        public double WorkingSetMb { get; private init; }

        /// <summary>Gets the GC total memory in MB at snapshot time.</summary>
        public double GcMemoryMb { get; private init; }

        /// <summary>Gets the total number of active managed threads.</summary>
        public int ThreadCount { get; private init; }

        /// <summary>Gets the Gen 0 GC collection count since process start.</summary>
        public int Gen0Collections { get; private init; }

        /// <summary>Gets the Gen 1 GC collection count since process start.</summary>
        public int Gen1Collections { get; private init; }

        /// <summary>Gets the Gen 2 GC collection count since process start.</summary>
        public int Gen2Collections { get; private init; }

        /// <summary>
        /// Captures and returns a current <see cref="PerformanceSnapshot"/>.
        /// </summary>
        public static PerformanceSnapshot Capture()
        {
            var proc = Process.GetCurrentProcess();
            return new PerformanceSnapshot
            {
                CapturedAtUtc   = DateTime.UtcNow,
                WorkingSetMb    = proc.WorkingSet64 / (1024.0 * 1024.0),
                GcMemoryMb      = GC.GetTotalMemory(forceFullCollection: false) / (1024.0 * 1024.0),
                ThreadCount     = proc.Threads.Count,
                Gen0Collections = GC.CollectionCount(0),
                Gen1Collections = GC.CollectionCount(1),
                Gen2Collections = GC.CollectionCount(2),
            };
        }

        /// <inheritdoc/>
        public override string ToString() =>
            $"RAM: {WorkingSetMb:F1} MB | GC: {GcMemoryMb:F1} MB | Threads: {ThreadCount} " +
            $"| GC Gen0/1/2: {Gen0Collections}/{Gen1Collections}/{Gen2Collections}";
    }
}
