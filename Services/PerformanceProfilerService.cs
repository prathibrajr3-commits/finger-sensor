using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AirGestureAI.Utilities;

namespace AirGestureAI.Services
{
    /// <summary>
    /// Tracks and profiles application performance metrics (CPU, RAM, GPU, GC, ThreadPool, IPC, Frame rate, etc.)
    /// and generates a performance report with recommendations.
    /// </summary>
    public sealed class PerformanceProfilerService : IAsyncDisposable
    {
        private readonly string _reportPath;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _profilingTask;
        private readonly List<PerformanceSnapshot> _history = new();
        private readonly object _lock = new();

        // High frequency transient metric stores
        private readonly ConcurrentQueue<double> _ipcLatencies = new();
        private readonly ConcurrentQueue<double> _frameLatencies = new();
        private readonly ConcurrentQueue<double> _gestureLatencies = new();
        private readonly ConcurrentQueue<double> _workflowExecutions = new();
        private readonly ConcurrentQueue<double> _pluginLoads = new();
        private readonly ConcurrentQueue<double> _aiResponses = new();

        private Process? _currentProcess;
        private TimeSpan _lastCpuTime = TimeSpan.Zero;
        private DateTime _lastCpuCheck = DateTime.UtcNow;

        /// <summary>
        /// Initializes a new instance of <see cref="PerformanceProfilerService"/>.
        /// </summary>
        /// <param name="baseDirectory">Base folder path to store output reports.</param>
        public PerformanceProfilerService(string baseDirectory)
        {
            _reportPath = Path.Combine(baseDirectory, "Diagnostics", "performance_report.json");
            Directory.CreateDirectory(Path.GetDirectoryName(_reportPath)!);
            _currentProcess = Process.GetCurrentProcess();
            _lastCpuTime = _currentProcess.TotalProcessorTime;
            _lastCpuCheck = DateTime.UtcNow;

            _profilingTask = Task.Run(ProfilingLoopAsync);
        }

        // ── Recording APIs ──────────────────────────────────────────────────────

        public void RecordIpcLatency(double ms) => _ipcLatencies.Enqueue(ms);
        public void RecordFrameLatency(double ms) => _frameLatencies.Enqueue(ms);
        public void RecordGestureLatency(double ms) => _gestureLatencies.Enqueue(ms);
        public void RecordWorkflowExecution(double ms) => _workflowExecutions.Enqueue(ms);
        public void RecordPluginLoad(double ms) => _pluginLoads.Enqueue(ms);
        public void RecordAiResponse(double ms) => _aiResponses.Enqueue(ms);

        // ── Background Collector ────────────────────────────────────────────────

        private async Task ProfilingLoopAsync()
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    await timer.WaitForNextTickAsync(_cts.Token);
                    CollectSnapshot();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Error("PerformanceProfiler: Error collecting performance metrics", ex);
                }
            }
        }

        private void CollectSnapshot()
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                double cpu = GetCpuUsage();
                double ram = _currentProcess!.WorkingSet64 / (1024.0 * 1024.0);
                double gpu = GetMockGpuUsage(); // Simulating GPU telemetry query
                int gen0 = GC.CollectionCount(0);
                int gen1 = GC.CollectionCount(1);
                int gen2 = GC.CollectionCount(2);

                // Safe GC Pause calculation
                double gcPause = 0.0;
                try
                {
                    gcPause = GC.GetTotalPauseDuration().TotalMilliseconds;
                }
                catch
                {
                    // Fallback for older runtime frameworks
                }

                ThreadPool.GetAvailableThreads(out var workerAvailable, out _);
                ThreadPool.GetMinThreads(out var workerMin, out _);
                int pendingQueue = Math.Max(0, workerMin - workerAvailable);

                // Drain transient metrics
                double ipc = DrainAverage(_ipcLatencies);
                double frame = DrainAverage(_frameLatencies);
                double gesture = DrainAverage(_gestureLatencies);
                double workflow = DrainAverage(_workflowExecutions);
                double plugin = DrainAverage(_pluginLoads);
                double ai = DrainAverage(_aiResponses);

                var snapshot = new PerformanceSnapshot
                {
                    TimestampUtc = now,
                    CpuUsagePercent = cpu,
                    MemoryMb = ram,
                    GpuUsagePercent = gpu,
                    Gen0Collections = gen0,
                    Gen1Collections = gen1,
                    Gen2Collections = gen2,
                    GcPauseMs = gcPause,
                    ThreadPoolQueueCount = pendingQueue,
                    IpcLatencyMs = ipc,
                    FrameLatencyMs = frame,
                    GestureLatencyMs = gesture,
                    WorkflowExecutionMs = workflow,
                    PluginLoadMs = plugin,
                    AiResponseMs = ai
                };

                _history.Add(snapshot);
                // Keep last 1 hour of rolling history (3600 seconds)
                if (_history.Count > 3600)
                {
                    _history.RemoveAt(0);
                }
            }
        }

        private double GetCpuUsage()
        {
            try
            {
                var now = DateTime.UtcNow;
                _currentProcess!.Refresh();
                var cpuTime = _currentProcess.TotalProcessorTime;
                var elapsed = now - _lastCpuCheck;
                if (elapsed.TotalMilliseconds <= 0) return 0.0;

                var diff = cpuTime - _lastCpuTime;
                _lastCpuTime = cpuTime;
                _lastCpuCheck = now;

                double usage = (diff.TotalMilliseconds / (elapsed.TotalMilliseconds * Environment.ProcessorCount)) * 100.0;
                return Math.Clamp(usage, 0.0, 100.0);
            }
            catch
            {
                return 0.0;
            }
        }

        private static double GetMockGpuUsage()
        {
            // Fallback mock GPU load
            return 12.5;
        }

        private static double DrainAverage(ConcurrentQueue<double> queue)
        {
            var list = new List<double>();
            while (queue.TryDequeue(out double val))
            {
                list.Add(val);
            }
            return list.Count > 0 ? list.Average() : 0.0;
        }

        // ── Report Generation ───────────────────────────────────────────────────

        /// <summary>
        /// Generates the performance JSON report.
        /// </summary>
        public void GenerateReport()
        {
            lock (_lock)
            {
                if (_history.Count == 0)
                {
                    CollectSnapshot();
                }

                var cpu = _history.Select(s => s.CpuUsagePercent).ToList();
                var ram = _history.Select(s => s.MemoryMb).ToList();
                var gpu = _history.Select(s => s.GpuUsagePercent).ToList();
                var ipc = _history.Select(s => s.IpcLatencyMs).Where(v => v > 0).ToList();
                var frame = _history.Select(s => s.FrameLatencyMs).Where(v => v > 0).ToList();
                var gesture = _history.Select(s => s.GestureLatencyMs).Where(v => v > 0).ToList();
                var ai = _history.Select(s => s.AiResponseMs).Where(v => v > 0).ToList();

                var report = new
                {
                    Timestamp = DateTime.UtcNow.ToString("O"),
                    TotalSamplesCollected = _history.Count,
                    Averages = new
                    {
                        CpuUsagePercent = cpu.DefaultIfEmpty(0).Average(),
                        MemoryMb = ram.DefaultIfEmpty(0).Average(),
                        GpuUsagePercent = gpu.DefaultIfEmpty(0).Average(),
                        IpcLatencyMs = ipc.DefaultIfEmpty(0).Average(),
                        FrameLatencyMs = frame.DefaultIfEmpty(0).Average(),
                        GestureLatencyMs = gesture.DefaultIfEmpty(0).Average(),
                        AiResponseMs = ai.DefaultIfEmpty(0).Average()
                    },
                    Max = new
                    {
                        CpuUsagePercent = cpu.DefaultIfEmpty(0).Max(),
                        MemoryMb = ram.DefaultIfEmpty(0).Max(),
                        GpuUsagePercent = gpu.DefaultIfEmpty(0).Max(),
                        IpcLatencyMs = ipc.DefaultIfEmpty(0).Max(),
                        FrameLatencyMs = frame.DefaultIfEmpty(0).Max(),
                        GestureLatencyMs = gesture.DefaultIfEmpty(0).Max(),
                        AiResponseMs = ai.DefaultIfEmpty(0).Max()
                    },
                    P95 = new
                    {
                        CpuUsagePercent = GetPercentile(cpu, 95),
                        MemoryMb = GetPercentile(ram, 95),
                        GpuUsagePercent = GetPercentile(gpu, 95),
                        IpcLatencyMs = GetPercentile(ipc, 95),
                        FrameLatencyMs = GetPercentile(frame, 95),
                        GestureLatencyMs = GetPercentile(gesture, 95),
                        AiResponseMs = GetPercentile(ai, 95)
                    },
                    Hotspots = GetHotspots(cpu, ram, ipc, frame),
                    Recommendations = GetRecommendations(cpu, ram, ipc, frame)
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
                    Logger.Info($"PerformanceProfiler: Saved performance report to '{_reportPath}'.");
                }
                catch (Exception ex)
                {
                    Logger.Error("PerformanceProfiler: Failed to write performance report", ex);
                }
            }
        }

        private static double GetPercentile(List<double> values, double percentile)
        {
            if (values.Count == 0) return 0.0;
            var sorted = values.OrderBy(v => v).ToList();
            int idx = (int)Math.Ceiling(percentile / 100.0 * sorted.Count) - 1;
            return sorted[Math.Clamp(idx, 0, sorted.Count - 1)];
        }

        private static List<string> GetHotspots(List<double> cpu, List<double> ram, List<double> ipc, List<double> frame)
        {
            var hotspots = new List<string>();
            if (cpu.DefaultIfEmpty(0).Max() > 80.0) hotspots.Add("High CPU spikes detected (>80%).");
            if (ram.DefaultIfEmpty(0).Max() > 250.0) hotspots.Add("Memory footprint exceeded 250MB target threshold.");
            if (ipc.DefaultIfEmpty(0).Max() > 100.0) hotspots.Add("IPC channel latency spikes (>100ms) observed.");
            if (frame.DefaultIfEmpty(0).Max() > 33.3) hotspots.Add("Frame processing latency exceeded 33.3ms (dropped frame threat).");
            return hotspots;
        }

        private static List<string> GetRecommendations(List<double> cpu, List<double> ram, List<double> ipc, List<double> frame)
        {
            var recs = new List<string>();
            if (cpu.DefaultIfEmpty(0).Average() > 40.0) recs.Add("Optimize ONNX model threads or reduce pipeline processing rate.");
            if (ram.DefaultIfEmpty(0).Average() > 200.0) recs.Add("Schedule periodic cache trimming in PerformanceOptimizer.");
            if (ipc.DefaultIfEmpty(0).Average() > 20.0) recs.Add("Verify Named Pipe buffer sizes or transition to isolated background threads.");
            if (frame.DefaultIfEmpty(0).Average() > 25.0) recs.Add("Check camera frame capture resolution and reduce OpenCV frame scaling overhead.");
            if (recs.Count == 0) recs.Add("Performance conforms to RTM criteria. No actions needed.");
            return recs;
        }

        // ── Disposal ────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            try { await _profilingTask; } catch (OperationCanceledException) { }
            _cts.Dispose();
            _currentProcess?.Dispose();
        }
    }

    internal sealed class PerformanceSnapshot
    {
        public DateTime TimestampUtc { get; set; }
        public double CpuUsagePercent { get; set; }
        public double MemoryMb { get; set; }
        public double GpuUsagePercent { get; set; }
        public int Gen0Collections { get; set; }
        public int Gen1Collections { get; set; }
        public int Gen2Collections { get; set; }
        public double GcPauseMs { get; set; }
        public int ThreadPoolQueueCount { get; set; }
        public double IpcLatencyMs { get; set; }
        public double FrameLatencyMs { get; set; }
        public double GestureLatencyMs { get; set; }
        public double WorkflowExecutionMs { get; set; }
        public double PluginLoadMs { get; set; }
        public double AiResponseMs { get; set; }
    }
}
