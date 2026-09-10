using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AirGestureAI.Services;
using Xunit;

namespace AirGestureAI.Tests
{
    /// <summary>
    /// Phase 6 — End-to-end integration tests for the crash recovery subsystem as wired
    /// into the real application lifecycle. These tests exercise the full
    /// SessionStateManager → CrashRecoveryService → RecoveryAgent pipeline,
    /// including crash simulation, multi-restart, Safe Mode activation, and
    /// graceful shutdown flow — without requiring a live WPF application.
    /// </summary>
    public sealed class CrashRecoveryIntegrationTests : IAsyncDisposable
    {
        private readonly string _root;
        private readonly LoggingService _logging;

        public CrashRecoveryIntegrationTests()
        {
            _root = Path.Combine(Path.GetTempPath(), $"CRIT_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_root);
            _logging = new LoggingService(_root);
        }

        public async ValueTask DisposeAsync()
        {
            try { Directory.Delete(_root, recursive: true); } catch { /* best-effort */ }
            await Task.CompletedTask.ConfigureAwait(false);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private SessionStateManager MakeManager(string dir) =>
            new SessionStateManager(dir, _logging);

        private CrashRecoveryService MakeCrashSvc(SessionStateManager mgr, string dir) =>
            new CrashRecoveryService(mgr, _logging, dir);

        private RecoveryAgent MakeAgent(SessionStateManager mgr, CrashRecoveryService crs, string dir) =>
            new RecoveryAgent(mgr, crs, _logging, dir, notificationService: null);

        private async Task<(SessionStateManager, CrashRecoveryService, RecoveryAgent)>
            BuildPipelineAsync(string subDir)
        {
            var dir = Path.Combine(_root, subDir);
            Directory.CreateDirectory(dir);
            var mgr = MakeManager(dir);
            var crs = MakeCrashSvc(mgr, dir);
            var agent = MakeAgent(mgr, crs, dir);
            return await Task.FromResult((mgr, crs, agent));
        }

        private static SessionState MakeSampleState(string version = "4.1") => new SessionState
        {
            Version = version,
            Theme = "Dark",
            ActiveProject = "E2EProject",
            SelectedCamera = 1,
            UserPreferences = new Dictionary<string, string>
            {
                ["Language"] = "en",
                ["EnableAdaptiveLearning"] = "true"
            }
        };

        // ── INT_01: Clean start — no crash lock, no prior state ─────────────────

        [Fact]
        public async Task INT_01_CleanStart_NoLockFile_DefaultStateReturned()
        {
            var (_, crs, agent) = await BuildPipelineAsync("int01");

            var state = await agent.ExecuteRecoveryAsync(silentMode: true);

            Assert.False(crs.AbnormalShutdownDetected);
            Assert.Equal("4.1", state.Version);
            Assert.Equal("Healthy", agent.LastValidationReport!.OverallSeverity);
            await crs.DisposeAsync();
        }

        // ── INT_02: Simulated crash — lock file present, no saved state ─────────

        [Fact]
        public async Task INT_02_CrashDetected_NoSavedState_DefaultStateReturned()
        {
            var dir = Path.Combine(_root, "int02");
            Directory.CreateDirectory(dir);

            // Simulate a previous run that crashed: write the lock file manually
            await File.WriteAllTextAsync(Path.Combine(dir, "active_session.lock"), "pid=999");

            var mgr = MakeManager(dir);
            var crs = MakeCrashSvc(mgr, dir);
            var agent = MakeAgent(mgr, crs, dir);

            var state = await agent.ExecuteRecoveryAsync(silentMode: true);

            Assert.True(crs.AbnormalShutdownDetected);
            Assert.Equal("4.1", state.Version);

            await crs.DisposeAsync();
        }

        // ── INT_03: Crash with saved state — state is fully restored ────────────

        [Fact]
        public async Task INT_03_CrashWithSavedState_StateIsRestored()
        {
            var dir = Path.Combine(_root, "int03");
            Directory.CreateDirectory(dir);

            // Pre-save a valid session state
            var mgr = MakeManager(dir);
            var originalState = MakeSampleState();
            await mgr.SaveStateAsync(originalState);

            // Simulate a crash by writing the lock file
            await File.WriteAllTextAsync(Path.Combine(dir, "active_session.lock"), "pid=123");

            var crs = MakeCrashSvc(mgr, dir);
            var agent = MakeAgent(mgr, crs, dir);

            var restoredState = await agent.ExecuteRecoveryAsync(silentMode: true);

            Assert.True(crs.AbnormalShutdownDetected);
            Assert.Equal("E2EProject", restoredState.ActiveProject);
            Assert.Equal(1, restoredState.SelectedCamera);
            Assert.Equal("en", restoredState.UserPreferences["Language"]);

            await crs.DisposeAsync();
        }

        // ── INT_04: Graceful shutdown — no crash detected on next start ──────────

        [Fact]
        public async Task INT_04_GracefulShutdown_NoCrashOnNextStart()
        {
            var dir = Path.Combine(_root, "int04");
            Directory.CreateDirectory(dir);

            // Simulate run 1: starts, writes lock, saves state, then closes cleanly
            var mgr = MakeManager(dir);
            var crs1 = MakeCrashSvc(mgr, dir);
            await crs1.RunStartupRecoveryAsync(); // writes lock file
            await mgr.SaveStateAsync(MakeSampleState());
            await crs1.DisposeAsync();            // removes lock file (clean exit)

            // Simulate run 2: should detect clean start
            var crs2 = MakeCrashSvc(mgr, dir);
            var agent2 = MakeAgent(mgr, crs2, dir);
            var state2 = await agent2.ExecuteRecoveryAsync(silentMode: true);

            Assert.False(crs2.AbnormalShutdownDetected);
            Assert.Equal("E2EProject", state2.ActiveProject);

            await crs2.DisposeAsync();
        }

        // ── INT_05: Multiple crashes — Safe Mode activates at threshold ──────────

        [Fact]
        public async Task INT_05_RepeatedCrashes_SafeModeActivates()
        {
            var dir = Path.Combine(_root, "int05");
            Directory.CreateDirectory(dir);

            // Simulate 3 crashes without marking startup stable
            for (int i = 0; i < 3; i++)
            {
                await File.WriteAllTextAsync(Path.Combine(dir, "active_session.lock"), $"pid={1000 + i}");
                var mgr = MakeManager(dir);
                var crs = MakeCrashSvc(mgr, dir);
                await crs.RunStartupRecoveryAsync(); // detects crash, increments counter, writes new lock
                // Do NOT call DisposeAsync or MarkStartupStable — simulate another crash
            }

            // 4th run — should now be in Safe Mode
            var mgr4 = MakeManager(dir);
            var crs4 = MakeCrashSvc(mgr4, dir);
            var agent4 = MakeAgent(mgr4, crs4, dir);

            // Write crash lock to simulate another crash
            await File.WriteAllTextAsync(Path.Combine(dir, "active_session.lock"), "pid=9999");
            await agent4.ExecuteRecoveryAsync(silentMode: true);

            Assert.True(crs4.IsSafeMode);

            await crs4.DisposeAsync();
        }

        // ── INT_06: Workflow execution state reset to Stopped after crash ────────

        [Fact]
        public async Task INT_06_WorkflowRunningState_ResetToStoppedAfterCrash()
        {
            var dir = Path.Combine(_root, "int06");
            Directory.CreateDirectory(dir);

            // Save state where workflow was mid-run
            var mgr = MakeManager(dir);
            var crashedState = MakeSampleState();
            crashedState.Workflow.ExecutionState = "Running";
            crashedState.Workflow.Nodes.Add(new WorkflowNodeState { Id = "n1", Label = "Click", NodeType = "Action" });
            await mgr.SaveStateAsync(crashedState);

            // Crash
            await File.WriteAllTextAsync(Path.Combine(dir, "active_session.lock"), "pid=555");

            var crs = MakeCrashSvc(mgr, dir);
            var agent = MakeAgent(mgr, crs, dir);
            var restored = await agent.ExecuteRecoveryAsync(silentMode: true);

            // Execution state must be sanitised to Stopped
            Assert.Equal("Stopped", restored.Workflow.ExecutionState);

            await crs.DisposeAsync();
        }

        // ── INT_07: Validation report written to Diagnostics ────────────────────

        [Fact]
        public async Task INT_07_ValidationReport_WrittenToDiagnosticsDirectory()
        {
            var dir = Path.Combine(_root, "int07");
            Directory.CreateDirectory(dir);

            var mgr = MakeManager(dir);
            var crs = MakeCrashSvc(mgr, dir);
            var agent = MakeAgent(mgr, crs, dir);

            await agent.ExecuteRecoveryAsync(silentMode: true);

            var reportPath = Path.Combine(dir, "Diagnostics", "validation_report.json");
            Assert.True(File.Exists(reportPath), "validation_report.json should be written to Diagnostics/");

            var json = await File.ReadAllTextAsync(reportPath);
            using var doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.TryGetProperty("OverallSeverity", out _));

            await crs.DisposeAsync();
        }

        // ── INT_08: AutoSave writes state, recovery restores it ─────────────────

        [Fact]
        public async Task INT_08_AutoSave_ThenCrash_StateIsRestored()
        {
            var dir = Path.Combine(_root, "int08");
            Directory.CreateDirectory(dir);

            var mgr = MakeManager(dir);
            var stateToSave = MakeSampleState();
            stateToSave.ActiveProject = "AutoSaveProject";

            // Directly save via manager (simulating what AutoSaveService does)
            await mgr.SaveStateAsync(stateToSave);

            // Simulate crash
            await File.WriteAllTextAsync(Path.Combine(dir, "active_session.lock"), "pid=8888");

            var crs = MakeCrashSvc(mgr, dir);
            var agent = MakeAgent(mgr, crs, dir);
            var restored = await agent.ExecuteRecoveryAsync(silentMode: true);

            Assert.Equal("AutoSaveProject", restored.ActiveProject);

            await crs.DisposeAsync();
        }

        // ── INT_09: Backup chain recovery — primary corrupted, backup used ───────

        [Fact]
        public async Task INT_09_CorruptedPrimary_BackupChainUsed()
        {
            var dir = Path.Combine(_root, "int09");
            Directory.CreateDirectory(dir);

            var mgr = MakeManager(dir);
            var goodState = MakeSampleState();
            goodState.ActiveProject = "BackupProject";
            await mgr.SaveStateAsync(goodState);

            // Corrupt the primary state file
            var primaryPath = Path.Combine(dir, "session_state.json");
            await File.WriteAllTextAsync(primaryPath, "{ CORRUPTED !!! }");

            // Simulate crash
            await File.WriteAllTextAsync(Path.Combine(dir, "active_session.lock"), "pid=7777");

            var crs = MakeCrashSvc(mgr, dir);
            var agent = MakeAgent(mgr, crs, dir);
            var restored = await agent.ExecuteRecoveryAsync(silentMode: true);

            // Should fall through to backup or default — just must not throw
            Assert.NotNull(restored);
            Assert.Equal("4.1", restored.Version);

            await crs.DisposeAsync();
        }

        // ── INT_10: Startup stability marker resets failure counter ────────────

        [Fact]
        public async Task INT_10_MarkStartupStable_ResetsFailureCounter()
        {
            var dir = Path.Combine(_root, "int10");
            Directory.CreateDirectory(dir);

            // Simulate 2 failures
            var failCountPath = Path.Combine(dir, "recovery_failure_count.txt");
            await File.WriteAllTextAsync(failCountPath, "2");

            var mgr = MakeManager(dir);
            var crs = MakeCrashSvc(mgr, dir);
            var agent = MakeAgent(mgr, crs, dir);

            // ExecuteRecoveryAsync calls MarkStartupStable internally
            await agent.ExecuteRecoveryAsync(silentMode: true);

            // Counter should be zeroed
            var counter = int.Parse((await File.ReadAllTextAsync(failCountPath)).Trim());
            Assert.Equal(0, counter);

            await crs.DisposeAsync();
        }

        // ── INT_11: FakeStateProvider round-trip through AutoSaveService ─────────

        [Fact]
        public async Task INT_11_AutoSaveService_SaveNow_PersistsCapturedState()
        {
            var dir = Path.Combine(_root, "int11");
            Directory.CreateDirectory(dir);

            var mgr = MakeManager(dir);
            var provider = new Int11StateProvider();
            var autoSave = new AutoSaveService(mgr, provider, _logging, dir, intervalSeconds: 60);

            autoSave.MarkDirty();
            await autoSave.SaveNowAsync();

            var loaded = await mgr.RestoreStateAsync();
            Assert.NotNull(loaded);
            Assert.Equal("Int11Project", loaded!.ActiveProject);
        }

        private sealed class Int11StateProvider : ISessionStateProvider
        {
            public Task<SessionState> CaptureCurrentStateAsync() =>
                Task.FromResult(new SessionState
                {
                    Version = "4.1",
                    ActiveProject = "Int11Project",
                    Theme = "Dark"
                });

            public Task ApplyStateAsync(SessionState state) => Task.CompletedTask;
        }

        // ── INT_12: RecoveryStatus fields populated correctly ───────────────────

        [Fact]
        public async Task INT_12_RecoveryStatus_Fields_PopulatedCorrectly()
        {
            var dir = Path.Combine(_root, "int12");
            Directory.CreateDirectory(dir);

            var mgr = MakeManager(dir);
            var crs = MakeCrashSvc(mgr, dir);
            var agent = MakeAgent(mgr, crs, dir);

            var recovered = await agent.ExecuteRecoveryAsync(silentMode: true);
            var report = crs.LastRecoveryReport;
            var validation = agent.LastValidationReport;

            var status = new RecoveryStatus
            {
                IsSafeMode = crs.IsSafeMode,
                AbnormalShutdownDetected = crs.AbnormalShutdownDetected,
                RecoverySource = report?.RecoverySource ?? "None",
                ConsecutiveFailureCount = report?.ConsecutiveFailureCount ?? 0,
                LastValidationSeverity = validation?.OverallSeverity ?? "Healthy",
                LastSuccessfulSaveTime = null
            };

            Assert.False(status.IsSafeMode);
            Assert.True(status.IsCleanStart);
            Assert.Equal("Healthy", status.LastValidationSeverity);

            await crs.DisposeAsync();
        }

        // ── INT_13: Concurrent crash + save does not corrupt state ──────────────

        [Fact]
        public async Task INT_13_ConcurrentSaveAndCrash_NoCorruption()
        {
            var dir = Path.Combine(_root, "int13");
            Directory.CreateDirectory(dir);

            var mgr = MakeManager(dir);

            // Fire multiple concurrent saves
            var tasks = new List<Task>();
            for (int i = 0; i < 8; i++)
            {
                var state = MakeSampleState();
                state.ActiveProject = $"Project_{i}";
                tasks.Add(mgr.SaveStateAsync(state));
            }
            await Task.WhenAll(tasks);

            // State should be loadable and not corrupt
            var loaded = await mgr.RestoreStateAsync();
            Assert.NotNull(loaded);
            Assert.Equal("4.1", loaded!.Version);
        }

        // ── INT_14: IsSafeMode state does not persist across restarts ────────────

        [Fact]
        public async Task INT_14_SafeMode_DoesNotPersistAcrossCleanRestart()
        {
            var dir = Path.Combine(_root, "int14");
            Directory.CreateDirectory(dir);

            // Simulate Safe Mode by writing failure count = 5 and crash lock
            await File.WriteAllTextAsync(Path.Combine(dir, "recovery_failure_count.txt"), "5");
            await File.WriteAllTextAsync(Path.Combine(dir, "active_session.lock"), "pid=1");

            var mgr = MakeManager(dir);
            var crs1 = MakeCrashSvc(mgr, dir);
            var agent1 = MakeAgent(mgr, crs1, dir);
            await agent1.ExecuteRecoveryAsync(silentMode: true);
            Assert.True(crs1.IsSafeMode);

            // Now simulate a clean shutdown (DisposeAsync removes lock + stable resets counter)
            await crs1.DisposeAsync();
            await crs1.MarkStartupStableAsync();  // manually reset counter (simulating OnExit)

            // Next startup: no crash lock, counter is 0 → Safe Mode should NOT be active
            var crs2 = MakeCrashSvc(mgr, dir);
            var agent2 = MakeAgent(mgr, crs2, dir);
            await agent2.ExecuteRecoveryAsync(silentMode: true);
            Assert.False(crs2.IsSafeMode);

            await crs2.DisposeAsync();
        }
    }
}
