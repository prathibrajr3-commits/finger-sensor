using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AirGestureAI.Utilities;

namespace AirGestureAI.Optimization
{
    /// <summary>
    /// Coordinates system-level performance optimizations, background scheduling,
    /// CPU and memory pressure monitoring, and cache trimming policies.
    /// </summary>
    public sealed class PerformanceOptimizer : IAsyncDisposable
    {
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _monitorTask;
        private bool _applied;
        private double _maxAllowedMemoryMb = 250.0;
        private Action? _cacheTrimCallback;

        /// <summary>
        /// Initializes a new instance of the <see cref="PerformanceOptimizer"/> class.
        /// </summary>
        public PerformanceOptimizer()
        {
            _monitorTask = Task.Run(MonitorResourcesLoopAsync);
        }

        /// <summary>
        /// Gets or sets the memory threshold in MB above which cache trimming is automatically triggered.
        /// </summary>
        public double MaxAllowedMemoryMb
        {
            get => _maxAllowedMemoryMb;
            set => _maxAllowedMemoryMb = value > 0 ? value : throw new ArgumentException("Memory limit must be positive.");
        }

        /// <summary>
        /// Sets a callback to execute when resource pressures require cache trimming.
        /// </summary>
        public void RegisterCacheTrimming(Action callback)
        {
            _cacheTrimCallback = callback;
        }

        /// <summary>
        /// Applies system thread tuning, GC configurations, and process priority upgrades.
        /// </summary>
        public void Apply()
        {
            if (_applied) return;
            _applied = true;

            // Tune ThreadPool to handle highly concurrent async named pipes efficiently
            ThreadPool.GetMinThreads(out _, out var ioMin);
            ThreadPool.SetMinThreads(16, ioMin); // Set minimum worker threads to 16 for IPC concurrency

            try
            {
                Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;
                Logger.Info("PerformanceOptimizer: Process execution priority set to High.");
            }
            catch (Exception ex)
            {
                Logger.Warn($"PerformanceOptimizer: Could not upgrade process execution priority. {ex.Message}");
            }

            // Optimize GC latency mode for real-time responsiveness
            System.Runtime.GCSettings.LatencyMode = System.Runtime.GCLatencyMode.SustainedLowLatency;
            Logger.Info("PerformanceOptimizer: GC latency mode set to SustainedLowLatency.");
            Logger.Info("PerformanceOptimizer: Low-latency execution optimizations successfully applied.");
        }

        /// <summary>
        /// Asynchronously schedules and executes a deferred background task.
        /// </summary>
        public void ScheduleBackgroundTask(Func<CancellationToken, Task> task, string taskLabel)
        {
            if (task == null) throw new ArgumentNullException(nameof(task));

            _ = Task.Run(async () =>
            {
                try
                {
                    Logger.Info($"PerformanceOptimizer: Running background task '{taskLabel}'…");
                    await task(_cts.Token).ConfigureAwait(false);
                    Logger.Info($"PerformanceOptimizer: Background task '{taskLabel}' completed successfully.");
                }
                catch (OperationCanceledException)
                {
                    Logger.Warn($"PerformanceOptimizer: Background task '{taskLabel}' cancelled.");
                }
                catch (Exception ex)
                {
                    Logger.Error($"PerformanceOptimizer: Error executing background task '{taskLabel}'", ex);
                }
            });
        }

        /// <summary>
        /// Trims system resources, forces GC collection, and clears active caches.
        /// </summary>
        public void TrimCaches()
        {
            Logger.Info("PerformanceOptimizer: Memory footprint limit crossed. Executing cache trimming and forcing Gen2 Garbage Collection.");
            try
            {
                _cacheTrimCallback?.Invoke();
            }
            catch (Exception ex)
            {
                Logger.Error("PerformanceOptimizer: Cache trim callback failed.", ex);
            }

            GC.Collect(2, GCCollectionMode.Forced, true, true);
        }

        private async Task MonitorResourcesLoopAsync()
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
            var process = Process.GetCurrentProcess();

            try
            {
                while (await timer.WaitForNextTickAsync(_cts.Token).ConfigureAwait(false))
                {
                    _cts.Token.ThrowIfCancellationRequested();

                    // Measure memory footprint (WorkingSet)
                    process.Refresh();
                    double workingSetMb = process.WorkingSet64 / (1024.0 * 1024.0);

                    if (workingSetMb > _maxAllowedMemoryMb)
                    {
                        Logger.Warn($"PerformanceOptimizer: Memory usage ({workingSetMb:F1} MB) is higher than target limit ({_maxAllowedMemoryMb} MB).");
                        TrimCaches();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal exit
            }
            catch (Exception ex)
            {
                Logger.Error("PerformanceOptimizer: Resource monitoring loop encountered an exception.", ex);
            }
        }

        /// <summary>
        /// Asynchronously releases resources, stops monitoring tasks, and restores standard priorities.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            try
            {
                await _monitorTask.ConfigureAwait(false);
            }
            catch
            {
                // Ignore task cancellation exceptions
            }

            _cts.Dispose();

            // Revert process priority on shutdown
            try
            {
                Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.Normal;
            }
            catch
            {
                // Ignore
            }

            System.Runtime.GCSettings.LatencyMode = System.Runtime.GCLatencyMode.Interactive;
            Logger.Info("PerformanceOptimizer: Execution defaults restored.");
        }
    }
}
