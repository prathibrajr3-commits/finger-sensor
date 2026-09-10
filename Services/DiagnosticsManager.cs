using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AirGestureAI.Utilities;

namespace AirGestureAI.Services
{
    /// <summary>
    /// Orchestrates and schedules the collection of all application diagnostics:
    /// Performance, Memory, Accessibility, System configuration, and Plugin states.
    /// Writes all structured JSON reports to the diagnostics export directory.
    /// </summary>
    public sealed class DiagnosticsManager : IDisposable
    {
        private readonly string _diagnosticsDirectory;
        private readonly PerformanceProfilerService _profiler;
        private readonly MemoryDiagnosticsService _memoryDiag;
        private readonly AccessibilityVerifierService _accessibilityVerifier;
        private readonly SystemDiagnosticsService _systemDiag;
        private readonly PluginDiagnosticsService _pluginDiag;

        private readonly CancellationTokenSource _cts = new();
        private readonly Task _scheduleTask;

        private static readonly TimeSpan SlowInterval = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Initializes a new instance of <see cref="DiagnosticsManager"/>.
        /// </summary>
        public DiagnosticsManager(
            PerformanceProfilerService profiler,
            MemoryDiagnosticsService memoryDiag,
            AccessibilityVerifierService accessibilityVerifier,
            SystemDiagnosticsService systemDiag,
            PluginDiagnosticsService pluginDiag)
        {
            _profiler = profiler ?? throw new ArgumentNullException(nameof(profiler));
            _memoryDiag = memoryDiag ?? throw new ArgumentNullException(nameof(memoryDiag));
            _accessibilityVerifier = accessibilityVerifier ?? throw new ArgumentNullException(nameof(accessibilityVerifier));
            _systemDiag = systemDiag ?? throw new ArgumentNullException(nameof(systemDiag));
            _pluginDiag = pluginDiag ?? throw new ArgumentNullException(nameof(pluginDiag));

            _diagnosticsDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AirGestureAI",
                "Diagnostics");
            Directory.CreateDirectory(_diagnosticsDirectory);

            _scheduleTask = Task.Run(DiagnosticsScheduleLoopAsync);
            Logger.Info($"DiagnosticsManager: Orchestrated diagnostics initialized at '{_diagnosticsDirectory}'.");
        }

        /// <summary>
        /// Manually triggers an immediate run of all diagnostic reports.
        /// </summary>
        public void RunAll()
        {
            try
            {
                _profiler.GenerateReport();
                _memoryDiag.GenerateReport();
                _accessibilityVerifier.VerifyAccessibility();
                _systemDiag.GenerateReport();
                _pluginDiag.GenerateReport();
                Logger.Info("DiagnosticsManager: Successfully generated all diagnostic reports.");
            }
            catch (Exception ex)
            {
                Logger.Error("DiagnosticsManager: Error manual-generating diagnostics", ex);
            }
        }

        private async Task DiagnosticsScheduleLoopAsync()
        {
            using var timer = new PeriodicTimer(SlowInterval);
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    await timer.WaitForNextTickAsync(_cts.Token);
                    RunAll();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Error("DiagnosticsManager: Error in diagnostics scheduler loop", ex);
                }
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _cts.Cancel();
            try { _scheduleTask.Wait(500); } catch { }
            _cts.Dispose();
        }
    }
}
