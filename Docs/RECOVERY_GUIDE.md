# AirGesture AI v4.1 — Recovery Guide

## Overview

AirGesture AI v4.1 includes a **production-grade crash recovery platform** that automatically detects abnormal shutdowns, restores prior session state, validates each recovered component, and presents the user with a clear notification when recovery occurs.

---

## Architecture

```
App startup
    │
    ▼
CrashRecoveryService.RunStartupRecoveryAsync()
    ├── Detects lock file (abnormal shutdown flag)
    ├── Reads consecutive failure counter
    ├── Activates Safe Mode if failures ≥ 3
    └── Runs restoration priority chain
            │
            ├── 1. session_state.json   (primary)
            ├── 2. session_state.json.bak  (backup)
            ├── 3. Diagnostics/session_state_autosave.json
            └── 4. Default state (no prior session)
    │
    ▼
RecoveryAgent.ExecuteRecoveryAsync()
    ├── ValidateLayout          → repairs out-of-range bounds
    ├── ValidateCalibration     → resets implausible focal length
    ├── ValidateWorkflow        → removes orphan connections, resets Running → Stopped
    ├── ValidatePlugins         → strips blank plugin IDs
    ├── ValidateAiHistory       → truncates to last 500 entries
    └── ValidateUserPreferences → null-guard
    │
    ▼
UI Notification (if abnormal shutdown detected, non-silent mode)
    │
    ▼
Session state returned to App.xaml.cs / AppCoordinator
```

---

## State Files

| File | Location | Purpose |
|------|----------|---------|
| `session_state.json` | `%AppData%/AirGestureAI/` | Primary session state |
| `session_state.json.bak` | `%AppData%/AirGestureAI/` | Backup (previous save) |
| `Diagnostics/session_state_autosave.json` | `%AppData%/AirGestureAI/` | AutoSave diagnostics copy |
| `active_session.lock` | `%AppData%/AirGestureAI/` | Session lock file containing PID, timestamp, and machine ID |
| `recovery_state.json` | `%AppData%/AirGestureAI/` | Structured recovery metadata (failures, timestamps, safe mode) |
| `recovery_failure_count.txt` | `%AppData%/AirGestureAI/` | Legacy consecutive failure counter (synced for backward compatibility) |
| `Checkpoints/checkpoint_<tag>.json` | `%AppData%/AirGestureAI/` | Named checkpoints |

All state files are **versioned JSON** wrapped in a `SessionEnvelope` with SHA-256 checksum.

---

## File Format

```json
{
  "Version": "4.1",
  "Checksum": "3b4c2a...",
  "PayloadJson": "{\"Version\":\"4.1\",\"Theme\":\"Dark\",...}"
}
```

The `Checksum` field is the hex-encoded SHA-256 hash of the raw `PayloadJson` string.

---

## Safe Mode

If the application crashes **3 or more times consecutively**, it activates **Safe Mode** on the next startup:

- Session state restoration is **skipped** (no potentially-corrupted state is loaded).
- The application starts with a **default state**.
- The user is notified via the recovery notification UI.
- Safe Mode is cleared automatically once the application reaches a **stable startup** (i.e., `MarkStartupStableAsync()` is called by the coordinator).

---

## AutoSave Behaviour

`AutoSaveService` runs a background loop using dynamic `Task.Delay` (no blocking).

| Setting | Default | Range |
|---------|---------|-------|
| Interval | 30 seconds | ≥ 5 seconds |
| Max retries | 3 | — |
| Backoff | 500 ms × attempt | — |

- A **dirty flag** prevents unnecessary saves when state hasn't changed.
- On every successful save, a copy is written to `Diagnostics/session_state_autosave.json` as a recovery fallback.
- Metrics are written to `Diagnostics/autosave_report.json`.
- Dynamically adapts to interval changes on the next loop tick.

---

## Checkpoints

Named checkpoints allow the user (or the application) to save a labelled snapshot of state at any time.

```csharp
// Create a checkpoint
await sessionStateManager.CreateCheckpointAsync("before_plugin_install");

// Restore a checkpoint
var state = await sessionStateManager.LoadCheckpointAsync("before_plugin_install");

// List checkpoints
var tags = await sessionStateManager.ListCheckpointsAsync();

// Delete a checkpoint
await sessionStateManager.DeleteCheckpointAsync("before_plugin_install");
```

**Tag validation:** Tags must match `^[a-zA-Z0-9_\-]{1,64}$`. Path traversal (e.g., `../../`) is rejected.

---

## v4.0 → v4.1 Session Migration

v4.0 sessions used a flat `Steps` list (from `WorkflowEngine.Workflow`). v4.1 uses a node-graph model.

| v4.0 Field | v4.1 Equivalent |
|------------|----------------|
| `Workflow.Steps[n].Action` | `WorkflowNodeState.Label` |
| `Workflow.Steps[n].Parameters` | `WorkflowNodeState.Parameter` |
| Steps are sequential | Nodes connected left-to-right by `WorkflowConnectionState` |
| *(none)* | `ExecutionState` = `"Stopped"` (safety default) |

Migration is automatic — the application detects the `"4.0"` envelope version and upgrades in-memory, then persists the migrated state as v4.1 on the next save.

---

## Recovery Reports

Three JSON reports are generated during and after recovery:

### `Diagnostics/recovery_report.json`

```json
{
  "Timestamp": "2025-...",
  "AbnormalShutdownDetected": true,
  "RecoverySource": "Backup",
  "IntegrityStatus": "Backup file valid.",
  "RestorationSuccess": true,
  "CorruptedFiles": ["session_state.json"],
  "SafeModeActive": false,
  "RecoveryDurationMs": 42.3,
  "ConsecutiveFailureCount": 1
}
```

### `Diagnostics/validation_report.json`

```json
{
  "Timestamp": "2025-...",
  "OverallSeverity": "Recoverable",
  "ComponentResults": [
    { "ComponentName": "WorkflowGraph", "Severity": "Recoverable", "Message": "Workflow execution state reset from 'Running' to 'Stopped' after crash.", "Repaired": true },
    { "ComponentName": "WindowLayout", "Severity": "Healthy", "Message": "Layout valid.", "Repaired": false }
  ]
}
```

### `Diagnostics/autosave_report.json`

```json
{
  "SaveCount": 12,
  "SkippedSaves": 3,
  "FailedSaves": 0,
  "AverageSaveDurationMs": 11.4,
  "LastSuccessfulSave": "2025-...",
  "IsDirty": false,
  "IntervalSeconds": 30
}
```

---

## Dependency Injection Registration

Register all Phase 5 services in `App.xaml.cs` → `ConfigureServices`:

```csharp
// Register state manager
services.AddSingleton<SessionStateManager>(sp =>
{
    var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    var dir = Path.Combine(appData, "AirGestureAI");
    var logging = sp.GetRequiredService<LoggingService>();
    return new SessionStateManager(dir, logging);
});

// Register crash recovery
services.AddSingleton<CrashRecoveryService>(sp =>
{
    var mgr = sp.GetRequiredService<SessionStateManager>();
    var log = sp.GetRequiredService<LoggingService>();
    var dir = Path.GetDirectoryName(mgr.StateFilePath)!;
    return new CrashRecoveryService(mgr, log, dir);
});

// Register recovery agent
services.AddSingleton<RecoveryAgent>(sp =>
    new RecoveryAgent(
        sp.GetRequiredService<SessionStateManager>(),
        sp.GetRequiredService<CrashRecoveryService>(),
        sp.GetRequiredService<LoggingService>(),
        Path.GetDirectoryName(sp.GetRequiredService<SessionStateManager>().StateFilePath)!));

// Register autosave service
services.AddSingleton<AutoSaveService>(sp =>
    new AutoSaveService(
        sp.GetRequiredService<SessionStateManager>(),
        sp.GetRequiredService<ISessionStateProvider>(),
        sp.GetRequiredService<LoggingService>(),
        Path.GetDirectoryName(sp.GetRequiredService<SessionStateManager>().StateFilePath)!,
        intervalSeconds: 30));
```

---

## Application Startup Integration

```csharp
// In App.xaml.cs OnStartup or AppCoordinator.InitialiseAsync:

var agent = serviceProvider.GetRequiredService<RecoveryAgent>();
var restoredState = await agent.ExecuteRecoveryAsync(silentMode: false);

// Apply restored state to the application
applyStateToApplication(restoredState);

// Start autosave
var autoSave = serviceProvider.GetRequiredService<AutoSaveService>();
await autoSave.StartAsync(applicationCancellationToken);
```

```csharp
// On clean application exit (App.xaml.cs OnExit or AppCoordinator.ShutdownAsync):

await autoSave.StopAsync();
await crashRecovery.PerformCleanShutdownAsync();
```

---

## Testing

Run the Phase 5 test suite:

```bash
dotnet test Tests/AirGestureAI.Tests.csproj --filter "CrashRecoveryTests" --logger "console;verbosity=detailed"
```

33 tests covering:
- Atomic save/restore roundtrip
- Backup, autosave, and default-state chain
- Checksum mismatch and corrupted JSON handling
- v4.0 → v4.1 migration (steps → nodes)
- AutoSave dirty flag and diagnostics copy
- Lock-file crash detection
- Safe Mode activation
- Recovery report generation
- Workflow execution state safety (never auto-resume after crash)
- Orphan connection removal
- Invalid layout dimension repair
- Atomic save cancellation cleanup
- Crash counter reset after stable startup
- Absence of sensitive information in reports
- Concurrent autosaves validation

---

## Security Notes

- State files contain **no secrets, API keys, or credentials**.
- Checkpoint tags are validated against `^[a-zA-Z0-9_\-]{1,64}$` — path traversal is impossible.
- All file writes are **atomic** (write temp → move) to prevent corruption from interrupted writes.
- SHA-256 checksums detect tampering or partial writes before the payload is deserialized.

---

## Phase 6 — Application Lifecycle Integration

Phase 6 connects the Phase 5 subsystem to the real WPF startup/shutdown lifecycle.

### How to call AutoSaveService.MarkDirty()

Call `MarkDirty()` on the `AutoSaveService` singleton whenever the user changes recoverable state:

```csharp
// e.g. in a ViewModel after the user changes theme:
var autoSave = _serviceProvider.GetRequiredService<AutoSaveService>();
autoSave.MarkDirty();
```

The background loop saves every 30 seconds (configurable). The final save also fires at shutdown.

### Reading Recovery Status in the UI

```csharp
var status = App.Current.RecoveryStatus;

if (status.IsSafeMode)
    ShowSafeModeWarning();

if (status.AbnormalShutdownDetected)
    ShowRecoveryBanner($"Recovered from {status.RecoverySource}");
```

### Integration Test Coverage

`Tests/CrashRecoveryIntegrationTests.cs` contains **14 end-to-end tests**:

| Test | What it verifies |
|------|-----------------|
| INT_01 | Clean start — default state, no crash detected |
| INT_02 | Crash lock present, no saved state → default state used |
| INT_03 | Crash with saved state → state fully restored |
| INT_04 | Graceful shutdown → no crash detected on next run |
| INT_05 | 3 consecutive crashes → Safe Mode activates |
| INT_06 | Workflow `Running` state reset to `Stopped` after crash |
| INT_07 | `validation_report.json` written to `Diagnostics/` |
| INT_08 | AutoSave write → crash → state recovered |
| INT_09 | Corrupted primary → backup chain used, no exception |
| INT_10 | `MarkStartupStable` resets failure counter to 0 |
| INT_11 | AutoSaveService `SaveNowAsync()` round-trip persists state |
| INT_12 | `RecoveryStatus` fields populated correctly |
| INT_13 | 8 concurrent saves — no corruption |
| INT_14 | Safe Mode does not persist across a subsequent clean restart |

---

## Changelog

| Version | Date | Change |
|---------|------|--------|
| 4.1 | 2025-08 | Initial Phase 5 — Crash Recovery & Session Restoration |
| 4.1 | 2026-08 | Phase 6 — Production lifecycle integration, SessionStateProvider, RecoveryStatus, WpfRecoveryNotificationService, 14 integration tests |
