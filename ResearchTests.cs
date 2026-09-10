using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AirGestureAI.AIProviders;
using AirGestureAI.Analytics;
using AirGestureAI.Experiments;
using AirGestureAI.Federated;
using AirGestureAI.Learning;
using AirGestureAI.ModelZoo;
using AirGestureAI.Research;
using AirGestureAI.Utilities;

namespace AirGestureAI
{
    /// <summary>
    /// Executes unit tests and verification suites for the AirGesture AI v2.0 Research Platform subsystems.
    /// </summary>
    public static class ResearchTests
    {
        /// <summary>
        /// Runs all automated test cases and returns true if all pass.
        /// </summary>
        public static async Task<bool> RunAllTestsAsync()
        {
            Logger.Info("[ResearchTests] Initiating Phase 27 Verification Suite...");
            bool allPassed = true;

            try
            {
                // Test 1: AdaptiveLearningEngine
                bool test1 = await TestAdaptiveLearningAsync();
                Logger.Info($"[ResearchTests] TestAdaptiveLearning: {(test1 ? "PASSED" : "FAILED")}");
                allPassed &= test1;

                // Test 2: AnalyticsEngine
                bool test2 = TestAnalyticsEngine();
                Logger.Info($"[ResearchTests] TestAnalyticsEngine: {(test2 ? "PASSED" : "FAILED")}");
                allPassed &= test2;

                // Test 3: ProviderManager
                bool test3 = await TestProviderManagerAsync();
                Logger.Info($"[ResearchTests] TestProviderManager: {(test3 ? "PASSED" : "FAILED")}");
                allPassed &= test3;

                // Test 4: ModelValidator
                bool test4 = TestModelValidator();
                Logger.Info($"[ResearchTests] TestModelValidator: {(test4 ? "PASSED" : "FAILED")}");
                allPassed &= test4;

                // Test 5: ExperimentManager
                bool test5 = TestExperimentManager();
                Logger.Info($"[ResearchTests] TestExperimentManager: {(test5 ? "PASSED" : "FAILED")}");
                allPassed &= test5;

                // Test 6: FederatedCoordinator
                bool test6 = await TestFederatedCoordinatorAsync();
                Logger.Info($"[ResearchTests] TestFederatedCoordinator: {(test6 ? "PASSED" : "FAILED")}");
                allPassed &= test6;

                // Test 7: v4.0 Architectural Verification Suite
                bool test7 = await ArchitectureTests.RunAllTestsAsync();
                Logger.Info($"[ResearchTests] ArchitectureTests: {(test7 ? "PASSED" : "FAILED")}");
                allPassed &= test7;

                // Test 8: v4.1 Sprint 1 Functional Tests
                bool test8 = await Sprint1Tests.RunAllTestsAsync();
                Logger.Info($"[ResearchTests] Sprint1Tests: {(test8 ? "PASSED" : "FAILED")}");
                allPassed &= test8;
            }
            catch (Exception ex)
            {
                Logger.Error("[ResearchTests] Critical test failure during execution.", ex);
                allPassed = false;
            }

            Logger.Info($"[ResearchTests] Suite concluded. Global result: {(allPassed ? "SUCCESS" : "FAILURE")}");
            return allPassed;
        }

        private static async Task<bool> TestAdaptiveLearningAsync()
        {
            var profile = new LearningProfile();
            var trainer = new GestureTrainer();
            var evaluator = new ModelEvaluator();
            var engine = new AdaptiveLearningEngine(new PersonalizationManager(), trainer, evaluator);

            double baseline = engine.TrackingAccuracy;
            engine.ProcessGesture(Models.GestureType.ScrollUp);
            double trained = await engine.RetrainModelAsync(CancellationToken.None);

            // Trained accuracy should reflect evaluation metrics (e.g. 0.85 * 1.05 = ~0.8925)
            return trained > baseline;
        }

        private static bool TestAnalyticsEngine()
        {
            var engine = new AnalyticsEngine();
            engine.Usage.RegisterSessionStart();
            engine.CollectGesture(Models.GestureType.OpenPalm);
            engine.CollectUsage(new Models.HandData { IsDetected = true });

            var tempDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestAnalytics");
            string dashboardPath = engine.ExportReports(tempDir);

            bool filesExist = File.Exists(dashboardPath) && 
                              File.Exists(Path.Combine(tempDir, "analytics_data.json")) &&
                              File.Exists(Path.Combine(tempDir, "analytics_history.csv"));

            // Clean up
            try { Directory.Delete(tempDir, true); } catch { }

            return filesExist && engine.Gestures.Counts.ContainsKey("OpenPalm");
        }

        private static async Task<bool> TestProviderManagerAsync()
        {
            var manager = new ProviderManager("Local");
            if (manager.ActiveProviderName != "Local") return false;

            // Switch to ONNX
            manager.SetProvider("ONNX");
            if (manager.ActiveProviderName != "ONNX") return false;

            // Execute (ONNX prompt should run fine)
            string onnxRes = await manager.ExecuteAsync("Test ONNX");
            if (!onnxRes.Contains("[ONNX Provider]")) return false;

            // Switch to Ollama (Ollama fails and falls back to Local)
            manager.SetProvider("Ollama");
            string fallbackRes = await manager.ExecuteAsync("Trigger fallback");
            return fallbackRes.Contains("[Local Provider]");
        }

        private static bool TestModelValidator()
        {
            var validator = new ModelValidator();
            var model = new ModelMetadata
            {
                DisplayName = "Test Model",
                FilePath = "test_model.onnx"
            };

            var tempDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestModelZoo");
            Directory.CreateDirectory(tempDir);
            var filePath = Path.Combine(tempDir, "test_model.onnx");
            File.WriteAllText(filePath, "Mock binary contents");

            bool isValid = validator.Validate(model, tempDir);

            // Clean up
            try { Directory.Delete(tempDir, true); } catch { }

            return isValid && model.IsValidated;
        }

        private static bool TestExperimentManager()
        {
            var manager = new ExperimentManager();
            bool hasFlag = manager.Flags.ContainsKey("EnhancedSmoothing");
            bool isEnabled = manager.IsFeatureEnabled("EnhancedSmoothing");

            var control = new ResearchMetrics { Accuracy = 0.82, Precision = 0.80, Recall = 0.81, AverageLatencyMs = 5.0 };
            var treatment = new ResearchMetrics { Accuracy = 0.86, Precision = 0.84, Recall = 0.85, AverageLatencyMs = 4.2 };
            var result = manager.EvaluateExperiment(0, control, treatment);

            return hasFlag && isEnabled && result.WinnerVariant == "Treatment" && result.AccuracyLift > 0.03;
        }

        private static async Task<bool> TestFederatedCoordinatorAsync()
        {
            var privacy = new PrivacyManager();
            var synchronizer = new ModelSynchronizer();
            var aggregator = new AggregationEngine();
            var coordinator = new FederatedCoordinator(privacy, synchronizer, aggregator);

            string initialVersion = coordinator.CurrentModel.ModelVersion;
            string syncedVersion = await coordinator.RunSyncCycleAsync(CancellationToken.None);

            return initialVersion != syncedVersion && coordinator.CurrentModel.CompletedRounds == 1;
        }
    }
}
