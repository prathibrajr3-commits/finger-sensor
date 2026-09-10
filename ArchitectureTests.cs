using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AirGestureAI.AgentRuntime;
using AirGestureAI.AIWorker;
using AirGestureAI.Cognitive;
using AirGestureAI.DesktopAutomation;
using AirGestureAI.DiagnosticsSubsystem;
using AirGestureAI.FederatedLearning;
using AirGestureAI.GestureAI;
using AirGestureAI.IPC;
using AirGestureAI.Memory;
using AirGestureAI.OnnxEngine;
using AirGestureAI.Planning;
using AirGestureAI.PluginRuntime;
using AirGestureAI.Production;
using AirGestureAI.Security;
using AirGestureAI.Semantic;
using AirGestureAI.ServicesHost;
using AirGestureAI.SpatialComputing;
using AirGestureAI.Tools;
using AirGestureAI.TrackerHost;
using AirGestureAI.Utilities;
using AirGestureAI.WorkflowEngine;

namespace AirGestureAI
{
    /// <summary>
    /// Test suite verifying the v4.0 multi-process, security, agentic, and operations architecture.
    /// </summary>
    public static class ArchitectureTests
    {
        /// <summary>
        /// Executes all automated architectural tests and returns true if all passed.
        /// </summary>
        public static async Task<bool> RunAllTestsAsync()
        {
            Logger.Info("[ArchitectureTests] Initiating Architectural Verification Suite...");
            bool allPassed = true;

            try
            {
                // Test 1: ServiceHost & Process Manager Lifecycle
                bool test1 = TestServiceHost();
                Logger.Info($"[ArchitectureTests] TestServiceHost: {(test1 ? "PASSED" : "FAILED")}");
                allPassed &= test1;

                // Test 2: Secure Named Pipe IPC Loop
                bool test2 = await TestIpcLoopAsync();
                Logger.Info($"[ArchitectureTests] TestIpcLoop: {(test2 ? "PASSED" : "FAILED")}");
                allPassed &= test2;

                // Test 3: Plugin Sandboxing & Wasm Stub Validation
                bool test3 = TestPluginSandbox();
                Logger.Info($"[ArchitectureTests] TestPluginSandbox: {(test3 ? "PASSED" : "FAILED")}");
                allPassed &= test3;

                // Test 4: Semantic Intent Recognition
                bool test4 = await TestSemanticIntentAsync();
                Logger.Info($"[ArchitectureTests] TestSemanticIntent: {(test4 ? "PASSED" : "FAILED")}");
                allPassed &= test4;

                // Test 5: Local Federated Averaging & DP Noise
                bool test5 = await TestFederatedLearningAsync();
                Logger.Info($"[ArchitectureTests] TestFederatedLearning: {(test5 ? "PASSED" : "FAILED")}");
                allPassed &= test5;

                // Test 6: Spatial Computing & Depth Mapping
                bool test6 = await TestSpatialComputingAsync();
                Logger.Info($"[ArchitectureTests] TestSpatialComputing: {(test6 ? "PASSED" : "FAILED")}");
                allPassed &= test6;

                // Test 7: Secure Vault & AES Encryption
                bool test7 = TestSecureVault();
                Logger.Info($"[ArchitectureTests] TestSecureVault: {(test7 ? "PASSED" : "FAILED")}");
                allPassed &= test7;

                // Test 8: Agent Orchestrator & Task Decomposition
                bool test8 = await TestAgentOrchestrationAsync();
                Logger.Info($"[ArchitectureTests] TestAgentOrchestration: {(test8 ? "PASSED" : "FAILED")}");
                allPassed &= test8;

                // Test 9: Memory manager (Working & Long-Term)
                bool test9 = TestMemoryManager();
                Logger.Info($"[ArchitectureTests] TestMemoryManager: {(test9 ? "PASSED" : "FAILED")}");
                allPassed &= test9;

                // Test 10: Workflow Recorder & Replay
                bool test10 = await TestWorkflowsAsync();
                Logger.Info($"[ArchitectureTests] TestWorkflows: {(test10 ? "PASSED" : "FAILED")}");
                allPassed &= test10;

                // Test 11: Production Diagnostics & Performance Profiling
                bool test11 = TestDiagnosticsAndProfiling();
                Logger.Info($"[ArchitectureTests] TestDiagnosticsAndProfiling: {(test11 ? "PASSED" : "FAILED")}");
                allPassed &= test11;
            }
            catch (Exception ex)
            {
                Logger.Error("[ArchitectureTests] Critical error during tests execution", ex);
                allPassed = false;
            }

            Logger.Info($"[ArchitectureTests] Suite concluded. Global result: {(allPassed ? "SUCCESS" : "FAILURE")}");
            return allPassed;
        }

        private static bool TestServiceHost()
        {
            var manager = new ServiceProcessManager();
            var host = new ServiceHost(manager);

            host.Start();
            bool processesStarted = manager.IsAlive("TrackerHost") && manager.IsAlive("AIWorker");
            host.Stop();

            return processesStarted && !manager.IsAlive("TrackerHost") && !manager.IsAlive("AIWorker");
        }

        private static async Task<bool> TestIpcLoopAsync()
        {
            var router = new IpcRouter();
            router.Register("Ping", req => "Processed 'Ping'");
            var server = new IpcServer("AirGestureAI_Test_Pipe", router);
            server.Start();

            var client = new IpcClient("AirGestureAI_Test_Pipe");
            var response = await client.SendAsync(new IpcMessage { Method = "Ping", Payload = "test_data" });
            server.Stop();

            return response.Method == "Ping_Response" && response.Payload.Contains("Processed 'Ping'");
        }

        private static bool TestPluginSandbox()
        {
            var manager = new RuntimeManager();
            manager.Initialize();
            
            var validator = new AirGestureAI.PluginRuntime.PluginManifestValidator();
            var sandbox = manager.Sandbox;

            bool isManifestOk = validator.Validate("Plugins/manifest.json"); // Should log warn/fail gracefully
            return sandbox.IsActive && !isManifestOk; // Sandbox active, manifest should fail if non-existent
        }

        private static async Task<bool> TestSemanticIntentAsync()
        {
            var engine = new SemanticIntentEngine();
            var result = await engine.ClassifyAsync("open the files manager");

            return result.Tag == "OpenApplication" && result.Confidence > 0.8 && result.Entities.Contains("WorkspaceAgent");
        }

        private static async Task<bool> TestFederatedLearningAsync()
        {
            var coordinator = new FederatedLearningCoordinator();
            coordinator.RegisterParticipant("Node_A");
            coordinator.RegisterParticipant("Node_B");

            var globalModel = await coordinator.RunRoundAsync();
            return globalModel.Round == 1 && globalModel.Accuracy > 0.6 && coordinator.CurrentRound == 1;
        }

        private static async Task<bool> TestSpatialComputingAsync()
        {
            var center = new SpatialComputingCenter();
            center.Initialize();

            var depth = center.GetDepthFrame();
            var handFrame = await center.GetHand3DAsync();

            return depth.Width == 640 && handFrame.Landmarks.Count == 21 && handFrame.Confidence > 0.8;
        }

        private static bool TestSecureVault()
        {
            var center = new EnterpriseSecurityCenter();
            center.Initialize();

            var vault = center.Vault;
            vault.Store("api_secret_key", "SuperSecretAES256Text");

            var decrypted = vault.Retrieve("api_secret_key");
            return decrypted == "SuperSecretAES256Text" && center.Policy.IsAllowed("PluginInstall");
        }

        private static async Task<bool> TestAgentOrchestrationAsync()
        {
            var orchestrator = new AgentOrchestrator();
            orchestrator.Start();

            var tasks = await orchestrator.SubmitGoalAsync("open, launch, search");
            return tasks.Count == 3 && tasks.Any(t => t.Name == "open");
        }

        private static bool TestMemoryManager()
        {
            var memory = new MemoryManager("TestMemoryDb");
            memory.Store("Goal1", "Summarize article", "nlp", "test");

            var search = memory.Search("Summarize");
            bool ok = search.Count == 1 && search[0].Key == "Goal1";

            // Cleanup
            try { Directory.Delete("TestMemoryDb", true); } catch {}
            return ok;
        }

        private static async Task<bool> TestWorkflowsAsync()
        {
            var service = new WorkflowEngineService();
            service.StartRecording("OpenIDE");
            service.Recorder.CaptureStep("Launch", "VSCode");
            service.Recorder.CaptureStep("Keys", "Ctrl+K");
            var wf = service.StopRecording();

            if (wf == null || wf.Steps.Count != 2) return false;

            await service.ReplayAsync("OpenIDE");
            return service.History.Records.Count == 1 && service.History.Records[0].Item2 == "OpenIDE";
        }

        private static bool TestDiagnosticsAndProfiling()
        {
            var manager = new DiagnosticsManager("TestDiagnosticsLogs");
            manager.RecordHealth("TrackerService", true);
            manager.RecordHealth("AISubsystem", true);

            var path = manager.ExportDiagnostics("TestDiagnosticsLogs");
            bool fileExists = File.Exists(path);

            // Cleanup
            try { Directory.Delete("TestDiagnosticsLogs", true); } catch {}

            return fileExists && manager.HealthRecords.Count == 2;
        }
    }
}
