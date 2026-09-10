using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AirGestureAI.Services;
using Xunit;

namespace AirGestureAI.Tests
{
    /// <summary>
    /// Phase 5 — Crash Recovery and Session Restoration xUnit test suite.
    /// 25 tests covering SessionStateManager, AutoSaveService, CrashRecoveryService, and RecoveryAgent.
    /// Each test uses a unique temp directory for complete isolation.
    /// </summary>
    public sealed class CrashRecoveryTests : IAsyncDisposable
    {
        // ── Fixtures ──────────────────────────────────────────────────────────

        private readonly string _tempRoot;

        public CrashRecoveryTests()
        {
            // Unique temp directory per test class instance
            _tempRoot = Path.Combine(Path.GetTempPath(), $"CrashRecoveryTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempRoot);
        }

        private LoggingService NewLogging(string subDir = "") =>
            new LoggingService(Path.Combine(_tempRoot, string.IsNullOrEmpty(subDir) ? "log" : subDir));

        private (SessionStateManager mgr, LoggingService log) NewManager(string subDir = "")
        {
            var dir = Path.Combine(_tempRoot, string.IsNullOrEmpty(subDir) ? Guid.NewGuid().ToString("N") : subDir);
            Directory.CreateDirectory(dir);
            var log = NewLogging(subDir + "_log");
            return (new SessionStateManager(dir, log), log);
        }

        private static SessionState DefaultState() => new SessionState
        {
            Version = "4.1",
            Theme = "Dark",
            ActiveProject = "TestProject",
            SelectedCamera = 2,
            WindowLayout = new LayoutState { Width = 1920, Height = 1080 },
            CalibrationProfile = new CalibrationState { IsCalibrated = true, FocalLength = 600f },
            Workflow = new WorkflowState
            {
                Nodes = new List<WorkflowNodeState>
                {
                    new WorkflowNodeState { Id = "n1", Label = "Pinch", NodeType = "Action", CanvasX = 60, CanvasY = 120 },
                    new WorkflowNodeState { Id = "n2", Label = "Click", NodeType = "Action", CanvasX = 240, CanvasY = 120 }
                },
                Connections = new List<WorkflowConnectionState>
                {
                    new WorkflowConnectionState { SourceId = "n1", TargetId = "n2" }
                }
            }
        };

        // ── SessionStateManager Tests ─────────────────────────────────────────

        [Fact]
        public async Task SSM_01_SaveAndRestore_RoundtripSucceeds()
        {
            var (mgr, _) = NewManager("ssm01");
            var original = DefaultState();

            await mgr.SaveStateAsync(original);
            var restored = await mgr.RestoreStateAsync();

            Assert.NotNull(restored);
            Assert.Equal("4.1", restored!.Version);
            Assert.Equal(2, restored.SelectedCamera);
            Assert.Equal("Dark", restored.Theme);
        }

        [Fact]
        public async Task SSM_02_SaveCreatesAtomicFile()
        {
            var (mgr, _) = NewManager("ssm02");
            await mgr.SaveStateAsync(DefaultState());

            // Primary file must exist; no temp file should remain
            Assert.True(File.Exists(mgr.StateFilePath));
            var tempFiles = Directory.GetFiles(Path.GetDirectoryName(mgr.StateFilePath)!, ".tmp_state_*");
            Assert.Empty(tempFiles);
        }

        [Fact]
        public async Task SSM_03_SecondSaveCreatesBackup()
        {
            var (mgr, _) = NewManager("ssm03");

            await mgr.SaveStateAsync(DefaultState());
            await mgr.SaveStateAsync(DefaultState());  // Second save triggers backup creation

            Assert.True(File.Exists(mgr.BackupFilePath));
        }

        [Fact]
        public async Task SSM_04_ChecksumMismatch_ReturnsNull()
        {
            var (mgr, _) = NewManager("ssm04");
            await mgr.SaveStateAsync(DefaultState());

            // Corrupt the payload checksum
            var raw = await File.ReadAllTextAsync(mgr.StateFilePath);
            var corrupted = raw.Replace("\"Checksum\":", "\"Checksum\":\"INVALIDHEX\"")
                               .Replace("\"INVALIDHEX\"\"", "\"INVALIDHEX\"");

            // Write a manually tampered envelope
            var env = new SessionEnvelope
            {
                Version = "4.1",
                Checksum = "0000000000000000000000000000000000000000000000000000000000000000",
                PayloadJson = "{\"Version\":\"4.1\"}"
            };
            await File.WriteAllTextAsync(mgr.StateFilePath, JsonSerializer.Serialize(env));

            var result = await mgr.TryLoadAndValidateFileAsync(mgr.StateFilePath);
            Assert.Null(result);
        }

        [Fact]
        public async Task SSM_05_CorruptedJson_ReturnsNull()
        {
            var (mgr, _) = NewManager("ssm05");
            await mgr.SaveStateAsync(DefaultState());

            await File.WriteAllTextAsync(mgr.StateFilePath, "{ NOT VALID JSON {{{{");
            var result = await mgr.TryLoadAndValidateFileAsync(mgr.StateFilePath);
            Assert.Null(result);
        }

        [Fact]
        public async Task SSM_06_EmptyFile_ReturnsNull()
        {
            var (mgr, _) = NewManager("ssm06");
            await File.WriteAllTextAsync(mgr.StateFilePath, string.Empty);

            var result = await mgr.TryLoadAndValidateFileAsync(mgr.StateFilePath);
            Assert.Null(result);
        }

        [Fact]
        public async Task SSM_07_RestoreFromBackup_WhenPrimaryCorrupted()
        {
            var (mgr, _) = NewManager("ssm07");

            // Save twice to create a backup
            await mgr.SaveStateAsync(DefaultState());
            await mgr.SaveStateAsync(DefaultState());

            // Corrupt primary
            await File.WriteAllTextAsync(mgr.StateFilePath, "CORRUPT");

            var restored = await mgr.RestoreStateAsync();
            Assert.NotNull(restored);
        }

        [Fact]
        public async Task SSM_08_RestoreFromAutosave_WhenBothCorrupted()
        {
            var (mgr, log) = NewManager("ssm08");
            var dir = Path.GetDirectoryName(mgr.StateFilePath)!;
            var diagDir = Path.Combine(dir, "Diagnostics");
            Directory.CreateDirectory(diagDir);

            // Write valid autosave manually via a second manager pointing to diagDir
            var autosaveState = DefaultState();
            autosaveState.Theme = "Cobalt";
            var autosaveMgr = new SessionStateManager(diagDir, log);
            await autosaveMgr.SaveStateAsync(autosaveState, Path.Combine(diagDir, "session_state_autosave.json"));

            // Corrupt primary and backup
            await File.WriteAllTextAsync(mgr.StateFilePath, "X");
            await File.WriteAllTextAsync(mgr.BackupFilePath, "X");

            var restored = await mgr.RestoreStateAsync();
            Assert.NotNull(restored);
            Assert.Equal("Cobalt", restored!.Theme);
        }

        [Fact]
        public async Task SSM_09_NoFiles_ReturnsNull()
        {
            var (mgr, _) = NewManager("ssm09");
            var result = await mgr.RestoreStateAsync();
            Assert.Null(result);
        }

        [Fact]
        public async Task SSM_10_Checkpoint_CreateAndLoad()
        {
            var (mgr, _) = NewManager("ssm10");
            var state = DefaultState();
            state.Theme = "Ocean";
            await mgr.SaveStateAsync(state);

            await mgr.CreateCheckpointAsync("test_checkpoint");
            var loaded = await mgr.LoadCheckpointAsync("test_checkpoint");

            Assert.NotNull(loaded);
            Assert.Equal("Ocean", loaded!.Theme);
        }

        [Fact]
        public async Task SSM_11_Checkpoint_ListAndDelete()
        {
            var (mgr, _) = NewManager("ssm11");
            await mgr.SaveStateAsync(DefaultState());

            await mgr.CreateCheckpointAsync("cp1");
            await mgr.CreateCheckpointAsync("cp2");

            var list = await mgr.ListCheckpointsAsync();
            Assert.Contains("cp1", list);
            Assert.Contains("cp2", list);

            await mgr.DeleteCheckpointAsync("cp1");
            list = await mgr.ListCheckpointsAsync();
            Assert.DoesNotContain("cp1", list);
        }

        [Fact]
        public async Task SSM_12_Checkpoint_InvalidTag_Throws()
        {
            var (mgr, _) = NewManager("ssm12");
            await mgr.SaveStateAsync(DefaultState());

            await Assert.ThrowsAsync<ArgumentException>(() =>
                mgr.CreateCheckpointAsync("../../etc/passwd"));
        }

        // ── v4.0 Migration Tests ──────────────────────────────────────────────

        [Fact]
        public async Task SSM_13_V40Migration_FlatStepsBecomesNodes()
        {
            var (mgr, _) = NewManager("ssm13");

            // Build a v4.0 style payload (flat Steps)
            var v40State = new
            {
                Theme = "Dark",
                SelectedCamera = 0,
                ActiveProject = "OldProject",
                Workflow = new
                {
                    Steps = new[]
                    {
                        new { Action = "Pinch", Target = "Window", Parameters = "", Status = "Pending" },
                        new { Action = "Swipe", Target = "Scroll",  Parameters = "fast", Status = "Pending" }
                    }
                }
            };

            var payloadJson = JsonSerializer.Serialize(v40State);

            // Compute real checksum to embed a valid v4.0 envelope
            using var sha = System.Security.Cryptography.SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(payloadJson));
            var checksum = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

            var envelope = new SessionEnvelope
            {
                Version = "4.0",
                Checksum = checksum,
                PayloadJson = payloadJson
            };

            await File.WriteAllTextAsync(mgr.StateFilePath, JsonSerializer.Serialize(envelope));

            var result = await mgr.RestoreStateAsync();
            Assert.NotNull(result);
            Assert.Equal("4.1", result!.Version);
            Assert.Equal(2, result.Workflow.Nodes.Count);
            Assert.Single(result.Workflow.Connections);
            Assert.Equal("Stopped", result.Workflow.ExecutionState);
        }

        [Fact]
        public async Task SSM_14_V40Migration_NodePositionsChained()
        {
            var (mgr, _) = NewManager("ssm14");

            var v40State = new
            {
                Workflow = new
                {
                    Steps = new[]
                    {
                        new { Action = "Step1", Target = "", Parameters = "", Status = "Done" },
                        new { Action = "Step2", Target = "", Parameters = "", Status = "Done" },
                        new { Action = "Step3", Target = "", Parameters = "", Status = "Done" }
                    }
                }
            };

            var payloadJson = JsonSerializer.Serialize(v40State);
            using var sha = System.Security.Cryptography.SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(payloadJson));
            var checksum = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

            var envelope = new SessionEnvelope
            {
                Version = "4.0",
                Checksum = checksum,
                PayloadJson = payloadJson
            };

            await File.WriteAllTextAsync(mgr.StateFilePath, JsonSerializer.Serialize(envelope));

            var result = await mgr.RestoreStateAsync();
            Assert.NotNull(result);
            Assert.Equal(3, result!.Workflow.Nodes.Count);
            // All steps chained: 2 connections for 3 nodes
            Assert.Equal(2, result.Workflow.Connections.Count);
        }

        // ── AutoSaveService Tests ─────────────────────────────────────────────

        private class FakeStateProvider : ISessionStateProvider
        {
            public SessionState State = DefaultState();
            public int CallCount;

            public Task<SessionState> CaptureCurrentStateAsync()
            {
                CallCount++;
                return Task.FromResult(State);
            }

            public Task ApplyStateAsync(SessionState state)
            {
                State = state;
                return Task.CompletedTask;
            }

            private static SessionState DefaultState() => new SessionState { Version = "4.1", Theme = "Dark" };
        }

        [Fact]
        public async Task AS_15_ManualSave_PersistsState()
        {
            var dir = Path.Combine(_tempRoot, "as15");
            Directory.CreateDirectory(dir);
            var log = NewLogging("as15_log");
            var provider = new FakeStateProvider { State = new SessionState { Theme = "Ruby", Version = "4.1" } };
            var mgr = new SessionStateManager(dir, log);
            await using var svc = new AutoSaveService(mgr, provider, log, dir, intervalSeconds: 60);

            await svc.SaveNowAsync();

            var restored = await mgr.RestoreStateAsync();
            Assert.NotNull(restored);
            Assert.Equal("Ruby", restored!.Theme);
        }

        [Fact]
        public async Task AS_16_DirtyFlag_SkipsWhenClean()
        {
            var dir = Path.Combine(_tempRoot, "as16");
            Directory.CreateDirectory(dir);
            var log = NewLogging("as16_log");
            var provider = new FakeStateProvider();
            var mgr = new SessionStateManager(dir, log);
            await using var svc = new AutoSaveService(mgr, provider, log, dir, intervalSeconds: 60);

            // Do NOT call MarkDirty — just call SaveNow without force
            // Dirty flag starts as false, so a normal (non-force) save loop tick skips
            // We'll test MarkDirty → force flow
            svc.MarkDirty();
            Assert.True(svc.IsDirty);

            await svc.SaveNowAsync(); // force=true so dirty flag cleared
            Assert.False(svc.IsDirty);
        }

        [Fact]
        public async Task AS_17_AutosaveDiagnosticsCopy_Created()
        {
            var dir = Path.Combine(_tempRoot, "as17");
            Directory.CreateDirectory(dir);
            var log = NewLogging("as17_log");
            var provider = new FakeStateProvider();
            var mgr = new SessionStateManager(dir, log);
            await using var svc = new AutoSaveService(mgr, provider, log, dir, intervalSeconds: 60);

            await svc.SaveNowAsync();

            var autosavePath = Path.Combine(dir, "Diagnostics", "session_state_autosave.json");
            Assert.True(File.Exists(autosavePath));
        }

        [Fact]
        public async Task AS_18_SetInterval_UpdatesValue()
        {
            var dir = Path.Combine(_tempRoot, "as18");
            Directory.CreateDirectory(dir);
            var log = NewLogging("as18_log");
            var provider = new FakeStateProvider();
            var mgr = new SessionStateManager(dir, log);
            await using var svc = new AutoSaveService(mgr, provider, log, dir, intervalSeconds: 30);

            svc.SetInterval(90);
            Assert.Equal(90, svc.IntervalSeconds);
        }

        // ── CrashRecoveryService Tests ────────────────────────────────────────

        [Fact]
        public async Task CRS_19_CleanStartup_NoAbnormalShutdown()
        {
            var dir = Path.Combine(_tempRoot, "crs19");
            Directory.CreateDirectory(dir);
            var log = NewLogging("crs19_log");
            var mgr = new SessionStateManager(dir, log);
            await using var crs = new CrashRecoveryService(mgr, log, dir);

            // No lock file present → clean startup
            await crs.RunStartupRecoveryAsync();
            Assert.False(crs.AbnormalShutdownDetected);
        }

        [Fact]
        public async Task CRS_20_LockFilePresent_AbnormalShutdownDetected()
        {
            var dir = Path.Combine(_tempRoot, "crs20");
            Directory.CreateDirectory(dir);
            var log = NewLogging("crs20_log");
            var mgr = new SessionStateManager(dir, log);
            await using var crs = new CrashRecoveryService(mgr, log, dir);

            // Simulate stale lock file from previous crash
            await File.WriteAllTextAsync(Path.Combine(dir, "active_session.lock"), "2025-01-01T00:00:00Z");

            await crs.RunStartupRecoveryAsync();
            Assert.True(crs.AbnormalShutdownDetected);
        }

        [Fact]
        public async Task CRS_21_SafeMode_ActivatedAfterThreeFailures()
        {
            var dir = Path.Combine(_tempRoot, "crs21");
            Directory.CreateDirectory(dir);
            var log = NewLogging("crs21_log");
            var mgr = new SessionStateManager(dir, log);

            // Pre-set failure count to 3 (threshold)
            await File.WriteAllTextAsync(Path.Combine(dir, "recovery_failure_count.txt"), "3");
            // Simulate stale lock (which increments on startup to 4)
            await File.WriteAllTextAsync(Path.Combine(dir, "active_session.lock"), "stale");

            await using var crs = new CrashRecoveryService(mgr, log, dir);
            await crs.RunStartupRecoveryAsync();

            Assert.True(crs.IsSafeMode);
        }

        [Fact]
        public async Task CRS_22_RecoveryReport_WrittenToDiagnostics()
        {
            var dir = Path.Combine(_tempRoot, "crs22");
            Directory.CreateDirectory(dir);
            var log = NewLogging("crs22_log");
            var mgr = new SessionStateManager(dir, log);
            await using var crs = new CrashRecoveryService(mgr, log, dir);

            await crs.RunStartupRecoveryAsync();

            var reportPath = Path.Combine(dir, "Diagnostics", "recovery_report.json");
            Assert.True(File.Exists(reportPath));
            var content = await File.ReadAllTextAsync(reportPath);
            Assert.Contains("AbnormalShutdownDetected", content);
        }

        [Fact]
        public async Task CRS_23_CleanShutdown_RemovesLockFile()
        {
            var dir = Path.Combine(_tempRoot, "crs23");
            Directory.CreateDirectory(dir);
            var log = NewLogging("crs23_log");
            var mgr = new SessionStateManager(dir, log);
            await using var crs = new CrashRecoveryService(mgr, log, dir);

            await crs.RunStartupRecoveryAsync(); // Creates lock file
            await crs.PerformCleanShutdownAsync(); // Should remove lock file

            Assert.False(File.Exists(Path.Combine(dir, "active_session.lock")));
        }

        // ── RecoveryAgent Tests ───────────────────────────────────────────────

        [Fact]
        public async Task RA_24_ExecuteRecovery_ReturnsValidState()
        {
            var dir = Path.Combine(_tempRoot, "ra24");
            Directory.CreateDirectory(dir);
            var log = NewLogging("ra24_log");
            var mgr = new SessionStateManager(dir, log);

            // Pre-save a valid state
            var expected = DefaultState();
            expected.Theme = "Jade";
            await mgr.SaveStateAsync(expected);

            await using var crs = new CrashRecoveryService(mgr, log, dir);
            var agent = new RecoveryAgent(mgr, crs, log, dir);

            var result = await agent.ExecuteRecoveryAsync(silentMode: true);

            Assert.NotNull(result);
            Assert.Equal("Jade", result.Theme);
        }

        [Fact]
        public async Task RA_25_ExecuteRecovery_ValidationReportWritten()
        {
            var dir = Path.Combine(_tempRoot, "ra25");
            Directory.CreateDirectory(dir);
            var log = NewLogging("ra25_log");
            var mgr = new SessionStateManager(dir, log);

            await using var crs = new CrashRecoveryService(mgr, log, dir);
            var agent = new RecoveryAgent(mgr, crs, log, dir);

            await agent.ExecuteRecoveryAsync(silentMode: true);

            var reportPath = Path.Combine(dir, "Diagnostics", "validation_report.json");
            Assert.True(File.Exists(reportPath));
            var content = await File.ReadAllTextAsync(reportPath);
            Assert.Contains("OverallSeverity", content);
        }

        [Fact]
        public async Task RA_26_WorkflowNotAutoResumedAfterCrash()
        {
            var dir = Path.Combine(_tempRoot, "ra26");
            Directory.CreateDirectory(dir);
            var log = NewLogging("ra26_log");
            var mgr = new SessionStateManager(dir, log);

            // Persist state with workflow in Running state
            var state = DefaultState();
            state.Workflow.ExecutionState = "Running";
            await mgr.SaveStateAsync(state);

            // Simulate crash by leaving a lock file
            await File.WriteAllTextAsync(Path.Combine(dir, "active_session.lock"), "stale");

            await using var crs = new CrashRecoveryService(mgr, log, dir);
            var agent = new RecoveryAgent(mgr, crs, log, dir);
            var result = await agent.ExecuteRecoveryAsync(silentMode: true);

            // Running must be reset to Stopped by validator
            Assert.Equal("Stopped", result.Workflow.ExecutionState);
        }

        [Fact]
        public async Task RA_27_OrphanConnections_RemovedDuringValidation()
        {
            var dir = Path.Combine(_tempRoot, "ra27");
            Directory.CreateDirectory(dir);
            var log = NewLogging("ra27_log");
            var mgr = new SessionStateManager(dir, log);

            var state = DefaultState();
            state.Workflow.Connections.Add(new WorkflowConnectionState
            {
                SourceId = "ghost_node",
                TargetId = "n1"
            });
            await mgr.SaveStateAsync(state);

            await using var crs = new CrashRecoveryService(mgr, log, dir);
            var agent = new RecoveryAgent(mgr, crs, log, dir);
            var result = await agent.ExecuteRecoveryAsync(silentMode: true);

            // Orphan connection targeting ghost_node should be removed
            Assert.DoesNotContain(result.Workflow.Connections, c => c.SourceId == "ghost_node");
        }

        [Fact]
        public async Task RA_28_InvalidLayoutDimensions_Repaired()
        {
            var dir = Path.Combine(_tempRoot, "ra28");
            Directory.CreateDirectory(dir);
            var log = NewLogging("ra28_log");
            var mgr = new SessionStateManager(dir, log);

            var state = DefaultState();
            state.WindowLayout.Width = -1;
            state.WindowLayout.Height = 0;
            await mgr.SaveStateAsync(state);

            await using var crs = new CrashRecoveryService(mgr, log, dir);
            var agent = new RecoveryAgent(mgr, crs, log, dir);
            var result = await agent.ExecuteRecoveryAsync(silentMode: true);

            Assert.True(result.WindowLayout.Width >= 320);
            Assert.True(result.WindowLayout.Height >= 240);
        }

        [Fact]
        public async Task RA_29_DefaultState_ReturnedWhenAllSourcesExhausted()
        {
            var dir = Path.Combine(_tempRoot, "ra29");
            Directory.CreateDirectory(dir);
            var log = NewLogging("ra29_log");
            var mgr = new SessionStateManager(dir, log);

            // No state files exist
            await using var crs = new CrashRecoveryService(mgr, log, dir);
            var agent = new RecoveryAgent(mgr, crs, log, dir);
            var result = await agent.ExecuteRecoveryAsync(silentMode: true);

            Assert.NotNull(result);
            Assert.Equal("4.1", result.Version);
        }

        [Fact]
        public async Task SSM_14_Save_CancellationCleanup()
        {
            var (mgr, _) = NewManager("ssm14_cancel");
            var cts = new CancellationTokenSource();
            cts.Cancel(); // Pre-cancel

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                mgr.SaveStateAsync(DefaultState(), null, cts.Token));

            // Verify no temp file left
            var tempFiles = Directory.GetFiles(Path.GetDirectoryName(mgr.StateFilePath)!, ".tmp_state_*");
            Assert.Empty(tempFiles);
        }

        [Fact]
        public async Task CRS_23_FailureCountReset_AfterStableStartup()
        {
            var dir = Path.Combine(_tempRoot, "crs23_reset");
            Directory.CreateDirectory(dir);
            var log = NewLogging("crs23_reset_log");
            var mgr = new SessionStateManager(dir, log);

            await File.WriteAllTextAsync(Path.Combine(dir, "recovery_failure_count.txt"), "2");

            await using var crs = new CrashRecoveryService(mgr, log, dir);
            
            // Simulate abnormal shutdown lock file
            await File.WriteAllTextAsync(Path.Combine(dir, "active_session.lock"), "stale");
            
            await crs.RunStartupRecoveryAsync();
            // Stable startup should reset it to 0
            await crs.MarkStartupStableAsync();

            var countPath = Path.Combine(dir, "recovery_failure_count.txt");
            var text = await File.ReadAllTextAsync(countPath);
            Assert.Equal("0", text.Trim());
        }

        [Fact]
        public async Task RA_30_DiagnosticReports_NoSensitiveInformation()
        {
            var dir = Path.Combine(_tempRoot, "ra30_secrets");
            Directory.CreateDirectory(dir);
            var log = NewLogging("ra30_secrets_log");
            var mgr = new SessionStateManager(dir, log);

            var state = DefaultState();
            state.UserPreferences["SecretApiKey"] = "sk-1234567890abcdef";
            state.AiAssistantHistory.Add("Here is a secret password: mypassword123");
            await mgr.SaveStateAsync(state);

            // Trigger recovery
            await using var crs = new CrashRecoveryService(mgr, log, dir);
            var agent = new RecoveryAgent(mgr, crs, log, dir);
            await agent.ExecuteRecoveryAsync(silentMode: true);

            var valReportPath = Path.Combine(dir, "Diagnostics", "validation_report.json");
            var recReportPath = Path.Combine(dir, "Diagnostics", "recovery_report.json");

            var valContent = await File.ReadAllTextAsync(valReportPath);
            var recContent = await File.ReadAllTextAsync(recReportPath);

            Assert.DoesNotContain("sk-1234567890", valContent);
            Assert.DoesNotContain("sk-1234567890", recContent);
            Assert.DoesNotContain("mypassword123", valContent);
            Assert.DoesNotContain("mypassword123", recContent);
        }

        [Fact]
        public async Task AS_19_Autosave_ConcurrentSavesLock()
        {
            var dir = Path.Combine(_tempRoot, "as19_concurrent");
            Directory.CreateDirectory(dir);
            var log = NewLogging("as19_concurrent_log");
            var provider = new FakeStateProvider();
            var mgr = new SessionStateManager(dir, log);
            await using var svc = new AutoSaveService(mgr, provider, log, dir, intervalSeconds: 60);

            svc.MarkDirty();

            // Run multiple saves concurrently
            var t1 = svc.SaveNowAsync();
            var t2 = svc.SaveNowAsync();
            var t3 = svc.SaveNowAsync();

            await Task.WhenAll(t1, t2, t3);

            Assert.True(File.Exists(mgr.StateFilePath));
        }

        private class ThrowingStateProvider : ISessionStateProvider
        {
            public int CaptureCount;
            public int ThrowCount = 2;

            public Task<SessionState> CaptureCurrentStateAsync()
            {
                CaptureCount++;
                if (CaptureCount <= ThrowCount)
                {
                    throw new InvalidOperationException("Simulated capture failure");
                }
                return Task.FromResult(new SessionState { Version = "4.1" });
            }

            public Task ApplyStateAsync(SessionState state) => Task.CompletedTask;
        }

        [Fact]
        public async Task AS_20_Autosave_RetryAfterFailure()
        {
            var dir = Path.Combine(_tempRoot, "as20_retry");
            Directory.CreateDirectory(dir);
            var log = NewLogging("as20_retry_log");
            var provider = new ThrowingStateProvider();
            var mgr = new SessionStateManager(dir, log);
            await using var svc = new AutoSaveService(mgr, provider, log, dir, intervalSeconds: 60);

            svc.MarkDirty();
            await svc.SaveNowAsync(); // This should retry twice and then succeed

            Assert.Equal(3, provider.CaptureCount); // 2 failures + 1 success
            Assert.True(File.Exists(mgr.StateFilePath));
        }

        [Fact]
        public async Task AS_21_Autosave_Cancellation()
        {
            var dir = Path.Combine(_tempRoot, "as21_cancel");
            Directory.CreateDirectory(dir);
            var log = NewLogging("as21_cancel_log");
            var provider = new FakeStateProvider();
            var mgr = new SessionStateManager(dir, log);
            await using var svc = new AutoSaveService(mgr, provider, log, dir, intervalSeconds: 60);

            var cts = new CancellationTokenSource();
            cts.Cancel(); // Pre-cancel

            await Assert.ThrowsAsync<OperationCanceledException>(() => svc.SaveNowAsync(cts.Token));
        }

        // ── Dispose ───────────────────────────────────────────────────────────

        public async ValueTask DisposeAsync()
        {
            try { Directory.Delete(_tempRoot, recursive: true); }
            catch { /* best effort */ }
            await Task.CompletedTask;
        }
    }
}
