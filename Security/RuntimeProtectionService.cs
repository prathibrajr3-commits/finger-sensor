using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AirGestureAI.Security;
using AirGestureAI.Services;
using AirGestureAI.Utilities;

namespace AirGestureAI.Security
{
    /// <summary>
    /// Monitors the running application for tampering, vault corruption, authentication failures,
    /// and repeated failed plugin loads. Generates structured warnings through <see cref="LoggingService"/>.
    /// </summary>
    public sealed class RuntimeProtectionService : IAsyncDisposable
    {
        private readonly LoggingService _loggingService;
        private readonly string _appBaseDirectory;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _monitorTask;

        // State tracking
        private int _ipcAuthFailures;
        private int _pluginLoadFailures;
        private string _lastKnownBinaryHash = string.Empty;
        private bool _vaultCorruptionReported;

        private const int IpcFailureAlertThreshold   = 5;
        private const int PluginFailureAlertThreshold = 3;
        private static readonly TimeSpan MonitorInterval = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Initializes a new instance of <see cref="RuntimeProtectionService"/>.
        /// </summary>
        /// <param name="loggingService">The application logging service for structured security events.</param>
        /// <param name="appBaseDirectory">The application installation directory.</param>
        public RuntimeProtectionService(LoggingService loggingService, string appBaseDirectory)
        {
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
            _appBaseDirectory = appBaseDirectory;

            // Baseline the binary hash at startup
            var exePath = Path.Combine(_appBaseDirectory, "AirGestureAI.exe");
            if (File.Exists(exePath))
                _lastKnownBinaryHash = ComputeHash(exePath);

            _monitorTask = Task.Run(MonitorLoopAsync);
        }

        // ── Public counters called by other subsystems ──────────────────────────

        /// <summary>Records a failed IPC authentication attempt and triggers alerts when threshold is exceeded.</summary>
        public void RecordIpcAuthFailure()
        {
            int count = Interlocked.Increment(ref _ipcAuthFailures);
            Logger.Warn($"RuntimeProtection: IPC authentication failure #{count}.");
            _loggingService.Warning($"RuntimeProtection: IPC auth failure #{count}", "RuntimeProtection");

            if (count >= IpcFailureAlertThreshold)
            {
                Logger.Warn($"RuntimeProtection: ALERT — {count} consecutive IPC authentication failures detected! Possible brute-force or invalid client.");
                _loggingService.Warning($"RuntimeProtection: {count} consecutive IPC auth failures — possible unauthorized client.", "RuntimeProtection");
            }
        }

        /// <summary>Resets the IPC authentication failure counter on a successful authentication.</summary>
        public void RecordIpcAuthSuccess() => Interlocked.Exchange(ref _ipcAuthFailures, 0);

        /// <summary>Records a failed plugin load attempt and triggers alerts when threshold is exceeded.</summary>
        public void RecordPluginLoadFailure(string pluginName)
        {
            int count = Interlocked.Increment(ref _pluginLoadFailures);
            Logger.Warn($"RuntimeProtection: Plugin load failure #{count} for '{pluginName}'.");
            _loggingService.Warning($"RuntimeProtection: Plugin '{pluginName}' failed to load (failure #{count}).", "RuntimeProtection");

            if (count >= PluginFailureAlertThreshold)
            {
                Logger.Warn($"RuntimeProtection: ALERT — {count} plugin load failures. Possible corrupt plugin directory or tampered assembly.");
                _loggingService.Warning($"RuntimeProtection: {count} plugin load failures — possible tampered plugin assemblies.", "RuntimeProtection");
            }
        }

        // ── Background Monitor ──────────────────────────────────────────────────

        private async Task MonitorLoopAsync()
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(MonitorInterval, _cts.Token).ConfigureAwait(false);
                    await RunMonitorChecksAsync().ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Error("RuntimeProtection: Monitor loop encountered unexpected error", ex);
                }
            }
        }

        private async Task RunMonitorChecksAsync()
        {
            await Task.Run(() =>
            {
                CheckBinaryModification();
                CheckVaultCorruption();
                CheckConfigurationValidity();
            }, _cts.Token).ConfigureAwait(false);
        }

        private void CheckBinaryModification()
        {
            var exePath = Path.Combine(_appBaseDirectory, "AirGestureAI.exe");
            if (!File.Exists(exePath)) return;

            var currentHash = ComputeHash(exePath);
            if (string.IsNullOrEmpty(_lastKnownBinaryHash))
            {
                _lastKnownBinaryHash = currentHash;
                return;
            }

            if (!string.Equals(currentHash, _lastKnownBinaryHash, StringComparison.OrdinalIgnoreCase))
            {
                var msg = $"CRITICAL: Binary modification detected! Hash changed from {_lastKnownBinaryHash} to {currentHash}.";
                Logger.Warn($"RuntimeProtection: {msg}");
                _loggingService.Warning($"RuntimeProtection: {msg}", "RuntimeProtection");
                _lastKnownBinaryHash = currentHash; // Update baseline to avoid repeat alerts
            }
        }

        private void CheckVaultCorruption()
        {
            var vaultPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AirGestureAI",
                "SecureVault");

            if (!Directory.Exists(vaultPath)) return;

            foreach (var vf in Directory.GetFiles(vaultPath, "*.vault"))
            {
                try
                {
                    var content = File.ReadAllText(vf).Trim();
                    Convert.FromBase64String(content); // Will throw if not valid base64 DPAPI blob
                }
                catch
                {
                    if (!_vaultCorruptionReported)
                    {
                        var msg = $"Vault file corruption detected in '{Path.GetFileName(vf)}'.";
                        Logger.Warn($"RuntimeProtection: {msg}");
                        _loggingService.Warning($"RuntimeProtection: {msg}", "RuntimeProtection");
                        _vaultCorruptionReported = true;
                    }
                    return;
                }
            }

            _vaultCorruptionReported = false;
        }

        private void CheckConfigurationValidity()
        {
            var configPaths = new[]
            {
                Path.Combine(_appBaseDirectory, "appsettings.json"),
                Path.Combine(_appBaseDirectory, "config.json"),
            };

            foreach (var cfg in configPaths)
            {
                if (!File.Exists(cfg)) continue;
                try
                {
                    var content = File.ReadAllText(cfg);
                    using var doc = JsonDocument.Parse(content);
                    return; // Valid — stop checking
                }
                catch (JsonException jex)
                {
                    var msg = $"Configuration file '{Path.GetFileName(cfg)}' is now malformed: {jex.Message}";
                    Logger.Warn($"RuntimeProtection: {msg}");
                    _loggingService.Warning($"RuntimeProtection: {msg}", "RuntimeProtection");
                    return;
                }
            }
        }

        // ── Utilities ────────────────────────────────────────────────────────────

        private static string ComputeHash(string path)
        {
            try
            {
                using var sha = SHA256.Create();
                using var stream = File.OpenRead(path);
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToUpperInvariant();
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            try { await _monitorTask.ConfigureAwait(false); } catch (OperationCanceledException) { }
            _cts.Dispose();
        }
    }
}
