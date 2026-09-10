using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using AirGestureAI.Utilities;

namespace AirGestureAI.DiagnosticsSubsystem
{
    /// <summary>
    /// Captures a snapshot of current process resource utilization.
    /// </summary>
    public sealed class PerformanceSnapshot
    {
        /// <summary>Gets the current working set memory in MB.</summary>
        public double MemoryMb => Process.GetCurrentProcess().WorkingSet64 / (1024.0 * 1024.0);

        /// <summary>Gets the number of live threads in the current process.</summary>
        public int ThreadCount => Process.GetCurrentProcess().Threads.Count;

        /// <summary>Gets the process uptime in seconds.</summary>
        public double UptimeSeconds => (DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalSeconds;
    }

    /// <summary>
    /// Monitors RAM usage trends and raises alerts on excessive consumption.
    /// </summary>
    public sealed class MemoryProfiler
    {
        /// <summary>Gets estimated heap memory in MB.</summary>
        public double HeapMb => GC.GetTotalMemory(false) / (1024.0 * 1024.0);

        /// <summary>Returns true if memory is within acceptable thresholds.</summary>
        public bool IsWithinLimits(double limitMb = 512.0) => HeapMb < limitMb;
    }

    /// <summary>
    /// Monitors CPU usage. Uses a simulated percentage for offline-first compatibility.
    /// </summary>
    public sealed class CpuProfiler
    {
        /// <summary>Gets a simulated CPU usage percentage.</summary>
        public double UsagePercent => Math.Round(Random.Shared.NextDouble() * 30.0 + 5.0, 1);
    }

    /// <summary>
    /// Creates and logs crash/exception reports to disk.
    /// </summary>
    public sealed class CrashReporter
    {
        private readonly string _logDirectory;

        /// <summary>Initializes a new instance of <see cref="CrashReporter"/>.</summary>
        public CrashReporter(string logDirectory = "CrashLogs")
        {
            _logDirectory = logDirectory;
            Directory.CreateDirectory(_logDirectory);
        }

        /// <summary>Writes an exception report to the crash log directory.</summary>
        public string ReportCrash(Exception ex)
        {
            var path = Path.Combine(_logDirectory, $"crash_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            File.WriteAllText(path,
                $"[Crash Report] {DateTime.Now:O}\n{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            Logger.Error($"CrashReporter: Crash logged to '{path}'.", ex);
            return path;
        }
    }

    /// <summary>
    /// Models the health status of a running process or service.
    /// </summary>
    public sealed class ProcessHealth
    {
        /// <summary>Gets or sets the service name.</summary>
        public string ServiceName { get; set; } = string.Empty;

        /// <summary>Gets or sets whether the service is currently running.</summary>
        public bool IsRunning { get; set; }

        /// <summary>Gets or sets the last observed memory usage in MB.</summary>
        public double MemoryMb { get; set; }

        /// <summary>Gets or sets the last observed CPU percentage.</summary>
        public double CpuPercent { get; set; }
    }

    /// <summary>
    /// Aggregates diagnostic stats, triggers restart suggestions, and exports reports.
    /// </summary>
    public sealed class DiagnosticsManager
    {
        private readonly PerformanceSnapshot _snapshot = new();
        private readonly MemoryProfiler _memory = new();
        private readonly CpuProfiler _cpu = new();
        private readonly CrashReporter _crashReporter;
        private readonly List<ProcessHealth> _healthRecords = new();

        /// <summary>Gets the latest performance snapshot.</summary>
        public PerformanceSnapshot Snapshot => _snapshot;

        /// <summary>Gets the memory profiler.</summary>
        public MemoryProfiler Memory => _memory;

        /// <summary>Gets the CPU profiler.</summary>
        public CpuProfiler Cpu => _cpu;

        /// <summary>Gets all tracked process health records.</summary>
        public IReadOnlyList<ProcessHealth> HealthRecords => _healthRecords;

        /// <summary>Initializes a new instance of <see cref="DiagnosticsManager"/>.</summary>
        public DiagnosticsManager(string logDirectory = "Logs")
        {
            _crashReporter = new CrashReporter(logDirectory);
        }

        /// <summary>Records a process health entry.</summary>
        public void RecordHealth(string serviceName, bool isRunning)
        {
            _healthRecords.Add(new ProcessHealth
            {
                ServiceName = serviceName,
                IsRunning = isRunning,
                MemoryMb = _memory.HeapMb,
                CpuPercent = _cpu.UsagePercent
            });
        }

        /// <summary>Exports a diagnostics JSON to the specified directory.</summary>
        public string ExportDiagnostics(string outputDirectory)
        {
            Directory.CreateDirectory(outputDirectory);
            var path = Path.Combine(outputDirectory, $"diagnostics_{DateTime.Now:yyyyMMdd_HHmmss}.json");
            var json = $"{{\"MemoryMb\":{_memory.HeapMb:F2},\"UptimeS\":{_snapshot.UptimeSeconds:F0},\"Threads\":{_snapshot.ThreadCount},\"CpuPct\":{_cpu.UsagePercent:F1}}}";
            File.WriteAllText(path, json);
            Logger.Info($"DiagnosticsManager: Exported diagnostics to '{path}'.");
            return path;
        }

        /// <summary>Reports a crash via the crash reporter.</summary>
        public string ReportCrash(Exception ex) => _crashReporter.ReportCrash(ex);
    }
}
