# AirGesture AI v4.1.0 — API Documentation Reference

This document highlights the core class signatures, methods, and events that form the public API contract of the AirGesture AI v4.1.0 desktop platform.

---

## 1. Hand Tracking Pipeline (`IHandTracker`)

The interface responsible for processing raw camera matrices and dispatching landmark coordinate updates.

### Definition
- **Namespace**: `AirGestureAI.HandTracking`
- **Assembly**: `AirGestureAI.dll`

### API Signature
```csharp
public interface IHandTracker : IDisposable
{
    /// <summary>
    /// Occurs when hand landmarks have been successfully updated.
    /// </summary>
    event EventHandler<HandTrackedEventArgs>? HandTracked;

    /// <summary>
    /// Occurs when the tracker encounters a fatal process or parsing error.
    /// </summary>
    event EventHandler<string>? TrackerError;

    /// <summary>
    /// Gets a value indicating whether the tracking process is active and running.
    /// </summary>
    bool IsTracking { get; }

    /// <summary>
    /// Starts the underlying tracking process or remote session.
    /// </summary>
    Task StartAsync();

    /// <summary>
    /// Stops active tracking.
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Submits a raw video frame for processing.
    /// </summary>
    Task ProcessFrameAsync(Mat frame, DateTime captureTimestamp);
}
```

---

## 2. IPC Communications (`IpcClient` & `IpcServer`)

The modernized infrastructure enabling secure, concurrent, and non-blocking multi-process Named Pipe messages.

### Definition
- **Namespace**: `AirGestureAI.IPC`

### API Signature
```csharp
public sealed class IpcClient
{
    public IpcClient(string pipeName);

    /// <summary>
    /// Gets or sets the request processing timeout in milliseconds.
    /// </summary>
    public int RequestTimeoutMs { get; set; }

    /// <summary>
    /// Sends an IPC message asynchronously with cancellation and timeouts.
    /// </summary>
    public Task<IpcMessage> SendAsync(IpcMessage msg, CancellationToken ct = default);
}

public sealed class IpcServer
{
    public IpcServer(string pipeName, IpcRouter router);

    /// <summary>
    /// Starts the asynchronous named pipe server connection loop.
    /// </summary>
    public void Start();

    /// <summary>
    /// Stops the server and cancels active connections.
    /// </summary>
    public void Stop();
}

public sealed class IpcRouter
{
    /// <summary>
    /// Registers a synchronous backward-compatible handler.
    /// </summary>
    public void Register(string method, Func<IpcMessage, string> handler);

    /// <summary>
    /// Registers an asynchronous handler.
    /// </summary>
    public void RegisterAsync(string method, Func<IpcMessage, CancellationToken, Task<string>> handler);

    /// <summary>
    /// Adds a middleware delegate to the pipeline.
    /// </summary>
    public void Use(Func<IpcMessage, CancellationToken, Func<IpcMessage, CancellationToken, Task<string>>, Task<string>> middleware);

    /// <summary>
    /// Routes the request envelope asynchronously, executing the registered middlewares and targeted handler.
    /// </summary>
    public Task<string> RouteAsync(IpcMessage request, CancellationToken cancellationToken);
}

public class IpcMessage
{
    public string Method { get; set; }
    public string Payload { get; set; }
    public string Token { get; set; }
    public string CorrelationId { get; set; }
}

public sealed class IpcResponse
{
    public bool Success { get; set; }
    public string Payload { get; set; }
    public string ErrorMessage { get; set; }
    public string CorrelationId { get; set; }
}
```

---

## 3. Secure Vault Credentials Storage (`SecureVault`)

Encrypts at-rest configuration keys using Windows DPAPI.

### Definition
- **Namespace**: `AirGestureAI.Security`

### API Signature
```csharp
public sealed class SecureVault
{
    public SecureVault(string vaultDirectory = "SecureVault");

    /// <summary>
    /// Encrypts and writes a credential or key to disk using User Scope DPAPI.
    /// </summary>
    public void Store(string entryKey, string value);

    /// <summary>
    /// Reads and decrypts a credential or key. Returns null if not found.
    /// </summary>
    public string? Retrieve(string entryKey);
}
```

---

## 4. Agent Orchestrator (`AgentOrchestrator`)

Decomposes and schedules concurrent DAG agent execution trees.

### Definition
- **Namespace**: `AirGestureAI.AgentRuntime`

### API Signature
```csharp
public sealed class AgentOrchestrator
{
    public AgentRegistry Registry { get; }
    public AgentScheduler Scheduler { get; }
    public AgentMessageBus MessageBus { get; }

    public event Action<string, string>? GestureDispatched;

    /// <summary>
    /// Decomposes a multi-step user goal text into dependencies and runs them.
    /// </summary>
    public Task<List<AgentTask>> SubmitGoalAsync(string goal, CancellationToken ct = default);

    /// <summary>
    /// Routes a gesture event through semantic parsing and DAG execution.
    /// </summary>
    public Task DispatchGestureAsync(string gestureLabel, string semanticExplanation, CancellationToken ct = default);
}
```

---

## 5. Structured Logging Service (`LoggingService`)

The enterprise logging pipeline providing async channel buffering, rolling files, and subscription capabilities.

### Definition
- **Namespace**: `AirGestureAI.Services`
- **Assembly**: `AirGestureAI.dll`

### API Signature
```csharp
public sealed class LoggingService : IAsyncDisposable
{
    public LoggingService(string baseDirectory);

    public long MaxFileSizeBytes { get; set; }
    public int RetentionDays { get; set; }

    public void AddSubscriber(ILogSubscriber subscriber);
    public void Log(LogLevel level, string message, string subsystem = "", string correlationId = "", Exception? exception = null);

    public void Trace(string message, string subsystem = "", string correlationId = "");
    public void Debug(string message, string subsystem = "", string correlationId = "");
    public void Information(string message, string subsystem = "", string correlationId = "");
    public void Warning(string message, string subsystem = "", string correlationId = "");
    public void Error(string message, Exception? exception = null, string subsystem = "", string correlationId = "");
    public void Critical(string message, Exception? exception = null, string subsystem = "", string correlationId = "");

    public Task<IReadOnlyList<LogEntry>> SearchLogsAsync(string? keyword, LogLevel? minLevel, DateTime? from, DateTime? to, CancellationToken cancellationToken = default);
    public Task ExportToCsvAsync(string outputPath, string? keyword, LogLevel? minLevel, DateTime? from, DateTime? to, CancellationToken cancellationToken = default);
    public Task ExportToJsonAsync(string outputPath, string? keyword, LogLevel? minLevel, DateTime? from, DateTime? to, CancellationToken cancellationToken = default);

    public ValueTask DisposeAsync();
}

public interface ILogSubscriber
{
    LogLevel MinimumLevel { get; }
    void OnLogEntry(LogEntry entry);
}

public sealed record LogEntry
{
    public DateTime TimestampUtc { get; init; }
    public LogLevel Level { get; init; }
    public string Subsystem { get; init; }
    public string CorrelationId { get; init; }
    public string Message { get; init; }
    public string? ExceptionInfo { get; init; }
}

public enum LogLevel
{
    Trace,
    Debug,
    Information,
    Warning,
    Error,
    Critical
}
```

---

## 6. Startup Profiler (`StartupProfiler`)

Collects execution latencies during application startup phases and generates `startup_report.json`.

### Definition
- **Namespace**: `AirGestureAI.Services`

### API Signature
```csharp
public sealed class StartupProfiler
{
    public StartupProfiler(string baseDirectory);

    public void RecordDiBuild(long ms);
    public void RecordMainWindowCreation(long ms);
    public void RecordUiShown(long ms);
    public void RecordAiLoad(long ms);
    public void RecordPluginLoad(long ms);
    public void RecordCameraInitialization(long ms);

    public void FinalizeProfile();
}
```

---

## 7. Performance Optimizer (`PerformanceOptimizer`)

Coordinates background task execution, ThreadPool sizing, memory monitoring, and GC tuning.

### Definition
- **Namespace**: `AirGestureAI.Optimization`

### API Signature
```csharp
public sealed class PerformanceOptimizer : IAsyncDisposable
{
    public PerformanceOptimizer();

    public double MaxAllowedMemoryMb { get; set; }

    public void Apply();
    public void ScheduleBackgroundTask(Func<CancellationToken, Task> task, string taskLabel);
    public void RegisterCacheTrimming(Action callback);
    public void TrimCaches();

    public ValueTask DisposeAsync();
}
```

---

## 8. Security Audit Service (`SecurityAuditService`)

Runs 8 structured security checks and generates `security_report.json` in the Diagnostics directory.

### Definition
- **Namespace**: `AirGestureAI.Security`

### API Signature
```csharp
public sealed class SecurityAuditService
{
    public SecurityAuditService(LoggingService loggingService, string appBaseDirectory);

    /// <summary>
    /// Runs all security checks asynchronously and returns the audit report.
    /// Also writes security_report.json to %LocalAppData%\AirGestureAI\Diagnostics\.
    /// </summary>
    public Task<SecurityAuditReport> RunAuditAsync(CancellationToken cancellationToken = default);
}

public sealed class SecurityAuditReport
{
    public string AuditTimestamp { get; set; }
    public string Grade { get; set; }          // A through F
    public int CriticalCount { get; set; }
    public int WarningCount { get; set; }
    public List<string> Checks { get; set; }
    public List<AuditFinding> Findings { get; set; }
}

public sealed class AuditFinding
{
    public string Check { get; set; }
    public AuditFindingSeverity Severity { get; set; }
    public string Description { get; set; }
    public string Remediation { get; set; }
    public bool Passed { get; set; }
}

public enum AuditFindingSeverity { Info, Warning, Critical }
```

---

## 9. SecureVault (Hardened)

AES-256-GCM encrypted credential store with DPAPI-protected master key, key rotation, and backup recovery.

### Definition
- **Namespace**: `AirGestureAI.Security`

### API Signature
```csharp
public sealed class SecureVault
{
    public SecureVault(string vaultDirectory = "SecureVault");

    public void Store(string entryKey, string value);
    public string? Retrieve(string entryKey);

    /// <summary>Rotates master key and re-encrypts all stored entries.</summary>
    public void RotateKey();

    /// <summary>Exports all vault files to a backup directory.</summary>
    public Task<string> BackupAsync();

    /// <summary>Validates all vault entries are valid AES-GCM blobs.</summary>
    public bool VerifyIntegrity();
}
```

---

## 10. Path Security (`PathSecurity`)

Prevents path traversal and root boundary escapes.

### Definition
- **Namespace**: `AirGestureAI.Security`

### API Signature
```csharp
public static class PathSecurity
{
    /// <summary>
    /// Returns true only if inputPath resolves to a location strictly within rootDirectory.
    /// </summary>
    public static bool IsPathSafe(string inputPath, string rootDirectory);
}
```

---

## 11. Input Security (`InputSecurity`)

Rejects payloads containing shell injection characters.

### Definition
- **Namespace**: `AirGestureAI.Security`

### API Signature
```csharp
public static class InputSecurity
{
    // Default blacklist: ; & | > < $ ` ' " \
    public static InputValidationResult Validate(string input, IEnumerable<char>? allowedTokens = null);
}

public sealed class InputValidationResult
{
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; }
    public string InvalidToken { get; set; }
}
```

---

## 12. Binary Signer (`BinarySigner`)

SHA-256 hash and Authenticode signature verification for application binaries.

### Definition
- **Namespace**: `AirGestureAI.SDK`

### API Signature
```csharp
public static class BinarySigner
{
    public static string ComputeSha256(string filePath);
    public static string ReadExpectedHash(string sha256FilePath);
    public static BinaryVerificationResult VerifySha256(string binaryPath, string hashFilePath);
    public static BinaryVerificationResult ReadAuthenticodeSignature(string filePath);
    public static void GenerateIntegrityFile(string binaryPath, string outputHashFilePath);
}

public sealed class BinaryVerificationResult
{
    public bool IsValid { get; set; }
    public string ComputedHash { get; set; }
    public string ExpectedHash { get; set; }
    public bool HasAuthenticodeSignature { get; set; }
    public string SignerSubject { get; set; }
    public string ErrorMessage { get; set; }
}
```

---

## 13. Runtime Protection Service (`RuntimeProtectionService`)

Background binary tamper and vault corruption monitor with IPC auth failure tracking.

### Definition
- **Namespace**: `AirGestureAI.Security`

### API Signature
```csharp
public sealed class RuntimeProtectionService : IAsyncDisposable
{
    public RuntimeProtectionService(LoggingService loggingService, string appBaseDirectory);

    /// <summary>Call when an IPC authentication fails.</summary>
    public void RecordIpcAuthFailure();

    /// <summary>Call when an IPC authentication succeeds (resets failure counter).</summary>
    public void RecordIpcAuthSuccess();

    /// <summary>Call when a plugin fails to load.</summary>
    public void RecordPluginLoadFailure(string pluginName);

    public ValueTask DisposeAsync();
}
```

---

## 14. Performance Profiler Service (`PerformanceProfilerService`)

Gathers application performance metrics and generates `performance_report.json`.

### Definition
- **Namespace**: `AirGestureAI.Services`

### API Signature
```csharp
public sealed class PerformanceProfilerService : IAsyncDisposable
{
    public PerformanceProfilerService(string baseDirectory);

    public void RecordIpcLatency(double ms);
    public void RecordFrameLatency(double ms);
    public void RecordGestureLatency(double ms);
    public void RecordWorkflowExecution(double ms);
    public void RecordPluginLoad(double ms);
    public void RecordAiResponse(double ms);

    public void GenerateReport();
    public ValueTask DisposeAsync();
}
```

---

## 15. Memory Diagnostics Service (`MemoryDiagnosticsService`)

Monitors resource allocations and tracks leaks using `WeakReference`.

### Definition
- **Namespace**: `AirGestureAI.Services`

### API Signature
```csharp
public sealed class MemoryDiagnosticsService
{
    public MemoryDiagnosticsService(string baseDirectory);

    public void Track(object obj, string tag);
    public void Untrack(object obj);
    public void GenerateReport();
}
```

---

## 16. Accessibility Verifier Service (`AccessibilityVerifierService`)

Inspects WPF Visual Tree for accessibility compliance.

### Definition
- **Namespace**: `AirGestureAI.Services`

### API Signature
```csharp
public sealed class AccessibilityVerifierService
{
    public AccessibilityVerifierService(string baseDirectory);

    public void VerifyAccessibility();
}
```

---

## 17. System Diagnostics Service (`SystemDiagnosticsService`)

Gathers hardware/software configuration and generates `system_report.json`.

### Definition
- **Namespace**: `AirGestureAI.Services`

### API Signature
```csharp
public sealed class SystemDiagnosticsService
{
    public SystemDiagnosticsService(string baseDirectory);

    public void GenerateReport();
}
```

---

## 18. Plugin Diagnostics Service (`PluginDiagnosticsService`)

Collects status, load metrics, and compatibility issues for loaded plugins.

### Definition
- **Namespace**: `AirGestureAI.Services`

### API Signature
```csharp
public sealed class PluginDiagnosticsService
{
    public PluginDiagnosticsService(string baseDirectory, PluginManager? pluginManager = null);

    public void GenerateReport();
}
```

---

## 19. Diagnostics Manager (`DiagnosticsManager`)

Central coordinator scheduling and running all diagnostics.

### Definition
- **Namespace**: `AirGestureAI.Services`

### API Signature
```csharp
public sealed class DiagnosticsManager : IDisposable
{
    public DiagnosticsManager(
        PerformanceProfilerService profiler,
        MemoryDiagnosticsService memoryDiag,
        AccessibilityVerifierService accessibilityVerifier,
        SystemDiagnosticsService systemDiag,
        PluginDiagnosticsService pluginDiag);

    public void RunAll();
    public void Dispose();
}
```

---

## Phase 5 — Crash Recovery & Session Restoration API

### `SessionStateManager` (`AirGestureAI.Services`)

Manages atomic session persistence, SHA-256 integrity, backup chain, checkpoints, and v4.0→v4.1 migration.

```csharp
public sealed class SessionStateManager
{
    public string StateFilePath { get; }
    public string BackupFilePath { get; }

    public SessionStateManager(string appDataPath, LoggingService logging);

    /// <summary>Atomically saves state. Previous file becomes .bak.</summary>
    public Task SaveStateAsync(
        SessionState state,
        string? targetPath = null,
        CancellationToken cancellationToken = default);

    /// <summary>Restores state via priority chain: primary → backup → autosave → null.</summary>
    public Task<SessionState?> RestoreStateAsync();

    /// <summary>Loads and validates a specific file (checksum, schema version, migration).</summary>
    public Task<SessionState?> TryLoadAndValidateFileAsync(string filePath);

    /// <summary>Creates a named checkpoint of the current state.</summary>
    public Task CreateCheckpointAsync(string tag, CancellationToken cancellationToken = default);

    /// <summary>Loads a named checkpoint.</summary>
    public Task<SessionState?> LoadCheckpointAsync(string tag);

    /// <summary>Lists all checkpoint tags.</summary>
    public Task<List<string>> ListCheckpointsAsync();

    /// <summary>Deletes a named checkpoint.</summary>
    public Task DeleteCheckpointAsync(string tag);
}
```

---

### `AutoSaveService` (`AirGestureAI.Services`)

Background autosave loop with dirty-flag tracking, retry with exponential backoff, and diagnostics reporting.

```csharp
public sealed class AutoSaveService : IAsyncDisposable
{
    public bool IsDirty { get; }
    public int IntervalSeconds { get; }

    public AutoSaveService(
        SessionStateManager stateManager,
        ISessionStateProvider stateProvider,
        LoggingService logging,
        string appDataPath,
        int intervalSeconds = 30,
        bool compressionEnabled = false);

    public void MarkDirty();
    public void SetInterval(int seconds);
    public void SetCompression(bool enabled);

    public Task StartAsync(CancellationToken externalToken = default);
    public Task StopAsync();
    public Task SaveNowAsync(CancellationToken cancellationToken = default);
}

/// <summary>Implement to provide live application state for autosave capture.</summary>
public interface ISessionStateProvider
{
    Task<SessionState> CaptureCurrentStateAsync();
}
```

---

### `CrashRecoveryService` (`AirGestureAI.Services`)

Detects abnormal shutdowns, manages lock file, tracks failure count, and activates Safe Mode.

```csharp
public sealed class CrashRecoveryService : IAsyncDisposable
{
    public bool IsSafeMode { get; }
    public bool AbnormalShutdownDetected { get; }
    public RecoveryReport? LastRecoveryReport { get; }

    public CrashRecoveryService(
        SessionStateManager stateManager,
        LoggingService logging,
        string appDataPath);

    /// <summary>Runs startup detection, restore chain, lock file creation, and report generation.</summary>
    public Task<SessionState?> RunStartupRecoveryAsync(CancellationToken cancellationToken = default);

    /// <summary>Resets failure counter after confirmed stable startup.</summary>
    public Task MarkStartupStableAsync();

    /// <summary>Removes lock file on clean exit.</summary>
    public Task PerformCleanShutdownAsync();
}
```

---

### `RecoveryAgent` (`AirGestureAI.Services`)

Orchestrates the full recovery sequence: detection, validation, repair, notification, and reporting.

```csharp
public sealed class RecoveryAgent
{
    public SessionValidationReport? LastValidationReport { get; }
    public SessionState? RestoredState { get; }

    public RecoveryAgent(
        SessionStateManager stateManager,
        CrashRecoveryService crashRecovery,
        LoggingService logging,
        string appDataPath,
        IRecoveryNotificationService? notificationService = null);

    /// <summary>
    /// Executes the full recovery sequence and returns a validated SessionState.
    /// Never returns null — defaults are applied when no valid state exists.
    /// </summary>
    public Task<SessionState> ExecuteRecoveryAsync(
        bool silentMode = false,
        CancellationToken cancellationToken = default);
}

/// <summary>Implement to display recovery notifications in the UI layer.</summary>
public interface IRecoveryNotificationService
{
    Task ShowRecoveryNotificationAsync(RecoveryReport report, SessionValidationReport validation);
}
```

---

### Session State Models

| Type | Purpose |
|------|--------|
| `SessionState` | Root session document (v4.1) |
| `SessionEnvelope` | Checksum wrapper written to disk |
| `LayoutState` | Window geometry, zoom, dock panels |
| `CalibrationState` | Camera calibration parameters |
| `WorkflowState` | Node graph, connections, triggers, execution state |
| `WorkflowNodeState` | Individual workflow node (id, label, type, position) |
| `WorkflowConnectionState` | Directed connection between two nodes |
| `RecoveryReport` | Written to `Diagnostics/recovery_report.json` |
| `SessionValidationReport` | Written to `Diagnostics/validation_report.json` |
| `AutoSaveReport` | Written to `Diagnostics/autosave_report.json` |

