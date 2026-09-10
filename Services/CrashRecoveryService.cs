using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AirGestureAI.Utilities;

namespace AirGestureAI.Services
{
    /// <summary>
    /// Describes the source from which a session was recovered.
    /// </summary>
    public enum RecoverySource
    {
        /// <summary>Recovery was not needed — application started normally.</summary>
        None,
        /// <summary>Recovered from the primary session_state.json.</summary>
        Primary,
        /// <summary>Recovered from session_state.json.bak.</summary>
        Backup,
        /// <summary>Recovered from the diagnostics autosave.</summary>
        Autosave,
        /// <summary>No valid state found — started with default state.</summary>
        Default,
        /// <summary>Started in Safe Mode with minimal state.</summary>
        SafeMode
    }

    /// <summary>Severity levels for recovery validation results.</summary>
    public enum ValidationSeverity
    {
        /// <summary>Component healthy.</summary>
        Healthy,
        /// <summary>Warning — degraded but functional.</summary>
        Warning,
        /// <summary>Recoverable error — auto-corrected.</summary>
        Recoverable,
        /// <summary>Critical — cannot proceed without intervention.</summary>
        Critical
    }

    /// <summary>Structured report describing a single recovery event.</summary>
    public sealed class RecoveryReport
    {
        /// <summary>Gets or sets the UTC timestamp of this recovery report.</summary>
        public string Timestamp { get; set; } = DateTime.UtcNow.ToString("O");

        /// <summary>Gets or sets whether an abnormal shutdown was detected on startup.</summary>
        public bool AbnormalShutdownDetected { get; set; }

        /// <summary>Gets or sets the source from which the session was recovered.</summary>
        public string RecoverySource { get; set; } = "None";

        /// <summary>Gets or sets the integrity status description.</summary>
        public string IntegrityStatus { get; set; } = "Unknown";

        /// <summary>Gets or sets whether restoration succeeded.</summary>
        public bool RestorationSuccess { get; set; }

        /// <summary>Gets or sets the list of files that were found corrupted.</summary>
        public List<string> CorruptedFiles { get; set; } = new();

        /// <summary>Gets or sets a description of any auto-repaired state.</summary>
        public string RepairedState { get; set; } = string.Empty;

        /// <summary>Gets or sets whether Safe Mode was activated.</summary>
        public bool SafeModeActive { get; set; }

        /// <summary>Gets or sets the recovery duration in milliseconds.</summary>
        public double RecoveryDurationMs { get; set; }

        /// <summary>Gets or sets any errors or warnings recorded during recovery.</summary>
        public List<string> Messages { get; set; } = new();

        /// <summary>Gets or sets the number of consecutive startup failures before this run.</summary>
        public int ConsecutiveFailureCount { get; set; }
    }

    /// <summary>Recovery state metadata containing failure counters.</summary>
    public sealed class RecoveryStateMetadata
    {
        /// <summary>Gets or sets the number of consecutive startup failures.</summary>
        public int ConsecutiveStartupFailures { get; set; }

        /// <summary>Gets or sets the number of consecutive recovery failures.</summary>
        public int ConsecutiveRecoveryFailures { get; set; }

        /// <summary>Gets or sets the timestamp of the last failure.</summary>
        public string? LastFailureTimestamp { get; set; }

        /// <summary>Gets or sets whether Safe Mode is active.</summary>
        public bool SafeModeActive { get; set; }

        /// <summary>Gets or sets the timestamp of the last successful startup.</summary>
        public string? LastSuccessfulStartup { get; set; }
    }

    /// <summary>
    /// Detects abnormal shutdowns via a lock file, orchestrates session recovery
    /// through the defined priority chain, activates Safe Mode after repeated
    /// failures, and generates structured recovery reports.
    /// </summary>
    public sealed class CrashRecoveryService : IAsyncDisposable
    {
        private const string LockFileName = "active_session.lock";
        private const string FailureCountFileName = "recovery_failure_count.txt";
        private const int SafeModeThreshold = 3;

        private readonly SessionStateManager _stateManager;
        private readonly LoggingService _logging;
        private readonly string _appDataPath;
        private readonly string _lockFilePath;
        private readonly string _failureCountPath;
        private readonly string _diagnosticsDirectory;

        private bool _lockFileCreated;
        private bool _disposed;

        private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

        /// <summary>Gets whether Safe Mode is currently active.</summary>
        public bool IsSafeMode { get; private set; }

        /// <summary>Gets the recovery report produced by the last recovery operation.</summary>
        public RecoveryReport? LastRecoveryReport { get; private set; }

        /// <summary>Gets whether an abnormal shutdown was detected on this startup.</summary>
        public bool AbnormalShutdownDetected { get; private set; }

        /// <summary>
        /// Initializes a new <see cref="CrashRecoveryService"/>.
        /// </summary>
        public CrashRecoveryService(SessionStateManager stateManager, LoggingService logging, string appDataPath)
        {
            _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
            _logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _appDataPath = appDataPath;
            _lockFilePath = Path.Combine(appDataPath, LockFileName);
            _failureCountPath = Path.Combine(appDataPath, FailureCountFileName);
            _diagnosticsDirectory = Path.Combine(appDataPath, "Diagnostics");
            Directory.CreateDirectory(_diagnosticsDirectory);
        }

        // ── Startup ───────────────────────────────────────────────────────────

        /// <summary>
        /// Runs startup recovery logic:
        /// 1. Detects abnormal shutdown via lock file.
        /// 2. Determines consecutive failure count.
        /// 3. Activates Safe Mode if threshold exceeded.
        /// 4. Attempts session restoration using the priority chain.
        /// 5. Writes the session lock file.
        /// 6. Generates recovery_report.json.
        /// Returns the recovered SessionState or null (caller uses default state).
        /// </summary>
        public async Task<SessionState?> RunStartupRecoveryAsync(CancellationToken cancellationToken = default)
        {
            var sw = Stopwatch.StartNew();
            var report = new RecoveryReport
            {
                Timestamp = DateTime.UtcNow.ToString("O")
            };

            // 1. Detect abnormal shutdown
            AbnormalShutdownDetected = File.Exists(_lockFilePath);
            report.AbnormalShutdownDetected = AbnormalShutdownDetected;

            if (AbnormalShutdownDetected)
            {
                _logging.Warning("CrashRecoveryService: Abnormal shutdown detected (lock file present).", "CrashRecoveryService");
                report.Messages.Add("Abnormal shutdown detected — previous session did not exit cleanly.");
                await IncrementFailureCountAsync().ConfigureAwait(false);
            }
            else
            {
                _logging.Information("CrashRecoveryService: Clean startup — no abnormal shutdown detected.", "CrashRecoveryService");
            }

            // 2. Read consecutive failure count
            var failureCount = await ReadFailureCountAsync().ConfigureAwait(false);
            report.ConsecutiveFailureCount = failureCount;

            // 3. Check Safe Mode threshold
            if (failureCount >= SafeModeThreshold)
            {
                IsSafeMode = true;
                report.SafeModeActive = true;
                _logging.Warning(
                    $"CrashRecoveryService: Safe Mode activated after {failureCount} consecutive failures.",
                    "CrashRecoveryService");
                report.Messages.Add($"Safe Mode activated after {failureCount} consecutive failures.");
            }

            // 4. Attempt session restoration
            SessionState? restored = null;
            var source = RecoverySource.None;

            if (!IsSafeMode)
            {
                (restored, source) = await AttemptRestorationAsync(report, cancellationToken).ConfigureAwait(false);
                if (restored == null)
                {
                    var state = await ReadRecoveryStateAsync().ConfigureAwait(false);
                    state.ConsecutiveRecoveryFailures++;
                    state.LastFailureTimestamp = DateTime.UtcNow.ToString("O");
                    await WriteRecoveryStateAsync(state).ConfigureAwait(false);
                }
            }
            else
            {
                source = RecoverySource.SafeMode;
                report.Messages.Add("Safe Mode: Skipping state restoration to avoid loading potentially corrupted state.");
                _logging.Warning("CrashRecoveryService: Safe Mode — bypassing state restoration.", "CrashRecoveryService");
            }

            report.RecoverySource = source.ToString();
            report.RestorationSuccess = restored != null;
            sw.Stop();
            report.RecoveryDurationMs = sw.Elapsed.TotalMilliseconds;

            // 5. Write the session lock file (signals we're now running)
            await WriteLockFileAsync().ConfigureAwait(false);

            // 6. Persist recovery report
            LastRecoveryReport = report;
            await WriteRecoveryReportAsync(report).ConfigureAwait(false);

            return restored;
        }

        /// <summary>
        /// Marks the application as having reached a confirmed stable state.
        /// Resets the failure counter and the lock file remains until clean exit.
        /// </summary>
        public async Task MarkStartupStableAsync()
        {
            await ResetFailureCountAsync().ConfigureAwait(false);
            _logging.Information("CrashRecoveryService: Startup marked stable — failure counter reset.", "CrashRecoveryService");
        }

        /// <summary>
        /// Performs clean shutdown: removes the session lock file.
        /// Must be called on a clean application exit.
        /// </summary>
        public async Task PerformCleanShutdownAsync()
        {
            _logging.Information("CrashRecoveryService: Performing clean shutdown.", "CrashRecoveryService");
            await RemoveLockFileAsync().ConfigureAwait(false);
            await WriteRecoveryReportAsync(LastRecoveryReport ?? new RecoveryReport()).ConfigureAwait(false);
        }

        // ── Restoration Chain ─────────────────────────────────────────────────

        private async Task<(SessionState? State, RecoverySource Source)> AttemptRestorationAsync(
            RecoveryReport report,
            CancellationToken ct)
        {
            // 1. Try primary session file
            if (File.Exists(_stateManager.StateFilePath))
            {
                var s = await _stateManager.TryLoadAndValidateFileAsync(_stateManager.StateFilePath).ConfigureAwait(false);
                if (s != null)
                {
                    report.IntegrityStatus = "Primary file valid.";
                    return (s, RecoverySource.Primary);
                }
                report.CorruptedFiles.Add(Path.GetFileName(_stateManager.StateFilePath));
                report.Messages.Add("Primary session file corrupted.");
                _logging.Warning("CrashRecoveryService: Primary session file corrupted.", "CrashRecoveryService");
            }

            // 2. Try backup
            if (File.Exists(_stateManager.BackupFilePath))
            {
                var s = await _stateManager.TryLoadAndValidateFileAsync(_stateManager.BackupFilePath).ConfigureAwait(false);
                if (s != null)
                {
                    report.IntegrityStatus = "Backup file valid.";
                    report.RepairedState = "Restored from backup.";
                    return (s, RecoverySource.Backup);
                }
                report.CorruptedFiles.Add(Path.GetFileName(_stateManager.BackupFilePath));
                report.Messages.Add("Backup session file also corrupted.");
            }

            // 3. Try diagnostics autosave
            var autosavePath = Path.Combine(_diagnosticsDirectory, "session_state_autosave.json");
            if (File.Exists(autosavePath))
            {
                var s = await _stateManager.TryLoadAndValidateFileAsync(autosavePath).ConfigureAwait(false);
                if (s != null)
                {
                    report.IntegrityStatus = "Autosave valid.";
                    report.RepairedState = "Restored from diagnostics autosave.";
                    return (s, RecoverySource.Autosave);
                }
                report.CorruptedFiles.Add("session_state_autosave.json");
                report.Messages.Add("Diagnostics autosave also corrupted.");
            }

            // 4. Default — no valid state available
            report.IntegrityStatus = "All recovery sources exhausted.";
            report.Messages.Add("No valid session state found — starting with default state.");
            _logging.Warning("CrashRecoveryService: All recovery sources exhausted. Using default state.", "CrashRecoveryService");
            return (null, RecoverySource.Default);
        }

        // ── Lock File ─────────────────────────────────────────────────────────

        private async Task WriteLockFileAsync()
        {
            try
            {
                var lockData = new
                {
                    ProcessId = Environment.ProcessId,
                    Timestamp = DateTime.UtcNow.ToString("O"),
                    AppVersion = "4.1 RTM",
                    MachineId = Environment.MachineName
                };
                var json = JsonSerializer.Serialize(lockData, JsonOpts);
                await File.WriteAllTextAsync(_lockFilePath, json).ConfigureAwait(false);
                _lockFileCreated = true;
                _logging.Debug("CrashRecoveryService: Session lock file written with metadata.", "CrashRecoveryService");
            }
            catch (Exception ex)
            {
                _logging.Warning($"CrashRecoveryService: Could not write lock file: {ex.Message}", "CrashRecoveryService");
            }
        }

        private Task RemoveLockFileAsync()
        {
            try
            {
                if (_lockFileCreated && File.Exists(_lockFilePath))
                {
                    File.Delete(_lockFilePath);
                    _lockFileCreated = false;
                    _logging.Debug("CrashRecoveryService: Session lock file removed (clean exit).", "CrashRecoveryService");
                }
            }
            catch (Exception ex)
            {
                _logging.Warning($"CrashRecoveryService: Could not remove lock file: {ex.Message}", "CrashRecoveryService");
            }
            return Task.CompletedTask;
        }

        // ── Failure Counter ───────────────────────────────────────────────────

        private async Task<RecoveryStateMetadata> ReadRecoveryStateAsync()
        {
            var path = Path.Combine(_appDataPath, "recovery_state.json");
            try
            {
                if (File.Exists(path))
                {
                    var json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
                    var metadata = JsonSerializer.Deserialize<RecoveryStateMetadata>(json);
                    if (metadata != null) return metadata;
                }
            }
            catch { }

            // Fallback to legacy recovery_failure_count.txt if it exists
            var legacyPath = Path.Combine(_appDataPath, "recovery_failure_count.txt");
            int legacyCount = 0;
            try
            {
                if (File.Exists(legacyPath))
                {
                    var text = await File.ReadAllTextAsync(legacyPath).ConfigureAwait(false);
                    int.TryParse(text.Trim(), out legacyCount);
                }
            }
            catch { }

            return new RecoveryStateMetadata
            {
                ConsecutiveStartupFailures = legacyCount,
                LastFailureTimestamp = legacyCount > 0 ? DateTime.UtcNow.ToString("O") : null,
                SafeModeActive = legacyCount >= SafeModeThreshold
            };
        }

        private async Task WriteRecoveryStateAsync(RecoveryStateMetadata state)
        {
            var path = Path.Combine(_appDataPath, "recovery_state.json");
            var legacyPath = Path.Combine(_appDataPath, "recovery_failure_count.txt");
            try
            {
                var json = JsonSerializer.Serialize(state, JsonOpts);
                await File.WriteAllTextAsync(path, json).ConfigureAwait(false);

                // Sync legacy file for backward compatibility/tests
                await File.WriteAllTextAsync(legacyPath, state.ConsecutiveStartupFailures.ToString()).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logging.Warning($"CrashRecoveryService: Could not write recovery state: {ex.Message}", "CrashRecoveryService");
            }
        }

        private async Task<int> ReadFailureCountAsync()
        {
            var state = await ReadRecoveryStateAsync().ConfigureAwait(false);
            return state.ConsecutiveStartupFailures;
        }

        private async Task IncrementFailureCountAsync()
        {
            var state = await ReadRecoveryStateAsync().ConfigureAwait(false);
            state.ConsecutiveStartupFailures++;
            state.LastFailureTimestamp = DateTime.UtcNow.ToString("O");
            if (state.ConsecutiveStartupFailures >= SafeModeThreshold)
            {
                state.SafeModeActive = true;
            }
            await WriteRecoveryStateAsync(state).ConfigureAwait(false);
        }

        private async Task ResetFailureCountAsync()
        {
            var state = await ReadRecoveryStateAsync().ConfigureAwait(false);
            state.ConsecutiveStartupFailures = 0;
            state.ConsecutiveRecoveryFailures = 0;
            state.SafeModeActive = false;
            state.LastSuccessfulStartup = DateTime.UtcNow.ToString("O");
            await WriteRecoveryStateAsync(state).ConfigureAwait(false);
        }

        // ── Report ────────────────────────────────────────────────────────────

        private async Task WriteRecoveryReportAsync(RecoveryReport report)
        {
            try
            {
                var path = Path.Combine(_diagnosticsDirectory, "recovery_report.json");
                var json = JsonSerializer.Serialize(report, JsonOpts);
                await File.WriteAllTextAsync(path, json).ConfigureAwait(false);
                _logging.Information("CrashRecoveryService: recovery_report.json written.", "CrashRecoveryService");
            }
            catch (Exception ex)
            {
                _logging.Warning($"CrashRecoveryService: Failed to write recovery_report.json: {ex.Message}", "CrashRecoveryService");
            }
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;
            await PerformCleanShutdownAsync().ConfigureAwait(false);
        }
    }
}
