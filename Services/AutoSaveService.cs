using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AirGestureAI.Utilities;

namespace AirGestureAI.Services
{
    /// <summary>
    /// Autosave metrics report serialized to autosave_report.json.
    /// </summary>
    public sealed class AutoSaveReport
    {
        /// <summary>Gets or sets the UTC timestamp of this report.</summary>
        public string Timestamp { get; set; } = DateTime.UtcNow.ToString("O");

        /// <summary>Gets or sets the total number of successful saves.</summary>
        public int SaveCount { get; set; }

        /// <summary>Gets or sets the number of skipped saves (dirty flag was false).</summary>
        public int SkippedSaves { get; set; }

        /// <summary>Gets or sets the total number of failed save attempts.</summary>
        public int FailedSaves { get; set; }

        /// <summary>Gets or sets the total number of retries across all save attempts.</summary>
        public int RetryCount { get; set; }

        /// <summary>Gets or sets the average save duration in milliseconds.</summary>
        public double AverageSaveDurationMs { get; set; }

        /// <summary>Gets or sets the UTC timestamp of the last successful save.</summary>
        public string? LastSuccessfulSave { get; set; }

        /// <summary>Gets or sets the UTC timestamp of the last failure.</summary>
        public string? LastFailure { get; set; }

        /// <summary>Gets or sets whether the session is currently dirty.</summary>
        public bool IsDirty { get; set; }

        /// <summary>Gets or sets the configured autosave interval in seconds.</summary>
        public int IntervalSeconds { get; set; }

        /// <summary>Gets or sets whether GZip compression is enabled.</summary>
        public bool CompressionEnabled { get; set; }
    }

    /// <summary>
    /// Provides a factory for the current session state, allowing AutoSaveService
    /// to capture state without a circular dependency on the application layer.
    /// </summary>
    public interface ISessionStateProvider
    {
        /// <summary>Returns the current application session state for persistence.</summary>
        Task<SessionState> CaptureCurrentStateAsync();

        /// <summary>Applies a restored session state to the application components.</summary>
        Task ApplyStateAsync(SessionState state);
    }

    /// <summary>
    /// Runs a background autosave loop that periodically persists the session
    /// state through <see cref="SessionStateManager"/>. Uses dirty-state tracking
    /// to skip unnecessary saves. Supports retry with exponential backoff.
    /// Never blocks the WPF UI thread.
    /// </summary>
    public sealed class AutoSaveService : IAsyncDisposable
    {
        private const int MaxRetries = 3;
        private const int BaseBackoffMs = 500;

        private readonly SessionStateManager _stateManager;
        private readonly ISessionStateProvider _stateProvider;
        private readonly LoggingService _logging;
        private readonly string _diagnosticsDirectory;

        private CancellationTokenSource? _cts;
        private Task? _saveLoop;

        private volatile bool _isDirty;
        private int _intervalSeconds;
        private bool _compressionEnabled;

        private readonly SemaphoreSlim _stateLock = new(1, 1);

        // Metrics (interlocked for thread-safety)
        private int _saveCount;
        private int _skippedSaves;
        private int _failedSaves;
        private int _retryCount;
        private long _totalSaveDurationMs;
        private string? _lastSuccessfulSave;
        private string? _lastFailure;

        private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

        /// <summary>
        /// Initializes a new <see cref="AutoSaveService"/>.
        /// </summary>
        /// <param name="stateManager">Manager for atomic state persistence.</param>
        /// <param name="stateProvider">Provider that captures the live application state.</param>
        /// <param name="logging">Structured logging service.</param>
        /// <param name="appDataPath">Base application data directory.</param>
        /// <param name="intervalSeconds">Autosave interval in seconds (default 30).</param>
        /// <param name="compressionEnabled">Whether GZip compression is applied (default false).</param>
        public AutoSaveService(
            SessionStateManager stateManager,
            ISessionStateProvider stateProvider,
            LoggingService logging,
            string appDataPath,
            int intervalSeconds = 30,
            bool compressionEnabled = false)
        {
            _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
            _stateProvider = stateProvider ?? throw new ArgumentNullException(nameof(stateProvider));
            _logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _diagnosticsDirectory = Path.Combine(appDataPath, "Diagnostics");
            _intervalSeconds = intervalSeconds > 0 ? intervalSeconds : 30;
            _compressionEnabled = compressionEnabled;

            Directory.CreateDirectory(_diagnosticsDirectory);
        }

        /// <summary>Gets whether the session is currently marked dirty.</summary>
        public bool IsDirty => _isDirty;

        /// <summary>Gets the configured save interval in seconds.</summary>
        public int IntervalSeconds => _intervalSeconds;

        /// <summary>Gets the UTC timestamp of the last successful save.</summary>
        public string? LastSuccessfulSaveTime => _lastSuccessfulSave;

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Marks the session state as dirty, signalling the next autosave to proceed.</summary>
        public void MarkDirty() => _isDirty = true;

        /// <summary>Clears the dirty flag after a successful save.</summary>
        private void ClearDirty() => _isDirty = false;

        /// <summary>Sets the autosave interval.</summary>
        /// <param name="seconds">Interval in seconds (minimum 5).</param>
        public void SetInterval(int seconds)
        {
            _intervalSeconds = seconds >= 5 ? seconds : 5;
            _logging.Information($"AutoSaveService: Interval updated to {_intervalSeconds}s.", "AutoSaveService");
        }

        /// <summary>Sets whether GZip compression is applied to saved state files.</summary>
        public void SetCompression(bool enabled) => _compressionEnabled = enabled;

        /// <summary>Starts the background autosave loop.</summary>
        public async Task StartAsync(CancellationToken externalToken = default)
        {
            await _stateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_saveLoop != null && !_saveLoop.IsCompleted)
                {
                    _logging.Warning("AutoSaveService: Already running.", "AutoSaveService");
                    return;
                }

                _cts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
                _saveLoop = Task.Run(() => RunSaveLoopAsync(_cts.Token), _cts.Token);
                _logging.Information($"AutoSaveService: Started (interval={_intervalSeconds}s).", "AutoSaveService");
            }
            finally
            {
                _stateLock.Release();
            }
        }

        /// <summary>Stops the background autosave loop.</summary>
        public async Task StopAsync()
        {
            await _stateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_cts == null) return;

                _cts.Cancel();
                if (_saveLoop != null)
                {
                    try { await _saveLoop.ConfigureAwait(false); }
                    catch (OperationCanceledException) { }
                    catch (Exception ex)
                    {
                        _logging.Warning($"AutoSaveService: StopAsync exception: {ex.Message}", "AutoSaveService");
                    }
                    _saveLoop = null;
                }

                _cts.Dispose();
                _cts = null;

                await GenerateReportAsync().ConfigureAwait(false);
                _logging.Information("AutoSaveService: Stopped.", "AutoSaveService");
            }
            finally
            {
                _stateLock.Release();
            }
        }

        /// <summary>Immediately saves the current session state regardless of the dirty flag.</summary>
        public async Task SaveNowAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _logging.Information("AutoSaveService: Manual save triggered.", "AutoSaveService");
            await ExecuteSaveWithRetryAsync(force: true, cancellationToken).ConfigureAwait(false);
        }

        // ── Background Loop ───────────────────────────────────────────────────

        private async Task RunSaveLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(_intervalSeconds), ct).ConfigureAwait(false);
                    await ExecuteSaveWithRetryAsync(force: false, ct).ConfigureAwait(false);
                    await GenerateReportAsync().ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logging.Error("AutoSaveService: Unhandled error in save loop.", ex, "AutoSaveService");
                }
            }
        }

        private async Task ExecuteSaveWithRetryAsync(bool force, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (!force && !_isDirty)
            {
                Interlocked.Increment(ref _skippedSaves);
                _logging.Debug("AutoSaveService: Skipped — state not dirty.", "AutoSaveService");
                return;
            }

            var sw = Stopwatch.StartNew();
            int attempt = 0;
            bool success = false;

            while (attempt <= MaxRetries)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var state = await _stateProvider.CaptureCurrentStateAsync().ConfigureAwait(false);
                    await _stateManager.SaveStateAsync(state, null, ct).ConfigureAwait(false);

                    // Also write a diagnostics autosave copy for recovery fallback
                    var autosavePath = Path.Combine(_diagnosticsDirectory, "session_state_autosave.json");
                    await _stateManager.SaveStateAsync(state, autosavePath, ct).ConfigureAwait(false);

                    ClearDirty();
                    sw.Stop();

                    Interlocked.Increment(ref _saveCount);
                    Interlocked.Add(ref _totalSaveDurationMs, sw.ElapsedMilliseconds);
                    _lastSuccessfulSave = DateTime.UtcNow.ToString("O");

                    _logging.Information(
                        $"AutoSaveService: Save #{_saveCount} completed in {sw.ElapsedMilliseconds}ms.",
                        "AutoSaveService");
                    success = true;
                    break;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    attempt++;
                    Interlocked.Increment(ref _retryCount);
                    _lastFailure = DateTime.UtcNow.ToString("O");

                    _logging.Warning(
                        $"AutoSaveService: Save attempt {attempt} failed: {ex.Message}. " +
                        $"Retrying in {BaseBackoffMs * attempt}ms…",
                        "AutoSaveService");

                    if (attempt <= MaxRetries)
                    {
                        await Task.Delay(BaseBackoffMs * attempt, ct).ConfigureAwait(false);
                    }
                }
            }

            if (!success)
            {
                Interlocked.Increment(ref _failedSaves);
                _logging.Error(
                    $"AutoSaveService: Save failed after {MaxRetries} retries.",
                    null, "AutoSaveService");
            }
        }

        // ── Report ────────────────────────────────────────────────────────────

        private async Task GenerateReportAsync()
        {
            try
            {
                var report = new AutoSaveReport
                {
                    Timestamp = DateTime.UtcNow.ToString("O"),
                    SaveCount = _saveCount,
                    SkippedSaves = _skippedSaves,
                    FailedSaves = _failedSaves,
                    RetryCount = _retryCount,
                    AverageSaveDurationMs = _saveCount > 0
                        ? (double)_totalSaveDurationMs / _saveCount
                        : 0,
                    LastSuccessfulSave = _lastSuccessfulSave,
                    LastFailure = _lastFailure,
                    IsDirty = _isDirty,
                    IntervalSeconds = _intervalSeconds,
                    CompressionEnabled = _compressionEnabled
                };

                var reportPath = Path.Combine(_diagnosticsDirectory, "autosave_report.json");
                var json = JsonSerializer.Serialize(report, JsonOpts);
                await File.WriteAllTextAsync(reportPath, json).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logging.Warning($"AutoSaveService: Failed to write autosave_report.json: {ex.Message}", "AutoSaveService");
            }
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            await StopAsync().ConfigureAwait(false);
            _stateLock.Dispose();
        }
    }
}
