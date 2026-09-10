using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AirGestureAI.Configuration;
using AirGestureAI.Models;
using AirGestureAI.Plugins;
using AirGestureAI.SDK;
using AirGestureAI.Services;
using AirGestureAI.Utilities;
using AirGestureAI.ViewModels;

namespace AirGestureAI
{
    /// <summary>
    /// Executes unit tests and verification suites for the AirGesture AI v4.1 Sprint 1 features.
    /// </summary>
    public static class Sprint1Tests
    {
        /// <summary>
        /// Runs all Sprint 1 tests and returns true if all passed.
        /// </summary>
        public static async Task<bool> RunAllTestsAsync()
        {
            Logger.Info("[Sprint1Tests] Initiating Sprint 1 Verification Suite…");
            bool allPassed = true;

            try
            {
                bool test1 = TestSetupWizardService();
                Logger.Info($"[Sprint1Tests] TestSetupWizardService: {(test1 ? "PASSED" : "FAILED")}");
                allPassed &= test1;

                bool test2 = TestSetupWizardViewModel();
                Logger.Info($"[Sprint1Tests] TestSetupWizardViewModel: {(test2 ? "PASSED" : "FAILED")}");
                allPassed &= test2;

                bool test3 = TestGestureProfileService();
                Logger.Info($"[Sprint1Tests] TestGestureProfileService: {(test3 ? "PASSED" : "FAILED")}");
                allPassed &= test3;

                bool test4 = await TestGestureProfileServiceCrudAsync();
                Logger.Info($"[Sprint1Tests] TestGestureProfileServiceCrudAsync: {(test4 ? "PASSED" : "FAILED")}");
                allPassed &= test4;

                bool test5 = TestGestureManagerViewModel();
                Logger.Info($"[Sprint1Tests] TestGestureManagerViewModel: {(test5 ? "PASSED" : "FAILED")}");
                allPassed &= test5;

                bool test6 = TestWorkflowEditorViewModel();
                Logger.Info($"[Sprint1Tests] TestWorkflowEditorViewModel: {(test6 ? "PASSED" : "FAILED")}");
                allPassed &= test6;

                bool test7 = TestPerformanceDashboardViewModel();
                Logger.Info($"[Sprint1Tests] TestPerformanceDashboardViewModel: {(test7 ? "PASSED" : "FAILED")}");
                allPassed &= test7;

                bool test8 = TestPluginBrowserViewModel();
                Logger.Info($"[Sprint1Tests] TestPluginBrowserViewModel: {(test8 ? "PASSED" : "FAILED")}");
                allPassed &= test8;
            }
            catch (Exception ex)
            {
                Logger.Error("[Sprint1Tests] Critical test failure during execution.", ex);
                allPassed = false;
            }

            Logger.Info($"[Sprint1Tests] Suite concluded. Global result: {(allPassed ? "SUCCESS" : "FAILURE")}");
            return allPassed;
        }

        private static bool TestSetupWizardService()
        {
            var config = new AppConfig { IsFirstRunComplete = false };
            var service = new SetupWizardService(config);

            if (!service.IsFirstRun) return false;

            service.MarkComplete();

            return config.IsFirstRunComplete && !service.IsFirstRun;
        }

        private static bool TestSetupWizardViewModel()
        {
            var config = new AppConfig { IsFirstRunComplete = false };
            var service = new SetupWizardService(config);
            var vm = new SetupWizardViewModel(service);

            if (vm.CurrentStepIndex != 0) return false;
            if (!vm.CanGoNext) return false;
            if (vm.IsLastStep) return false;

            vm.NextCommand.Execute(null);
            if (vm.CurrentStepIndex != 1) return false;

            vm.BackCommand.Execute(null);
            return vm.CurrentStepIndex == 0;
        }

        private static bool TestGestureProfileService()
        {
            var service = new GestureProfileService();
            var profile = service.BuildDefaultProfile();

            if (profile == null) return false;
            if (profile.Name != "Default") return false;
            if (profile.Bindings.Count != 3) return false;

            var openPalm = profile.Bindings.FirstOrDefault(b => b.Gesture == GestureType.OpenPalm);
            if (openPalm == null) return false;
            if (!openPalm.IsEnabled) return false;
            return Math.Abs(openPalm.Sensitivity - 1.0) < 0.01;
        }

        private static async Task<bool> TestGestureProfileServiceCrudAsync()
        {
            var service = new GestureProfileService();
            var profile = service.BuildDefaultProfile();
            profile.Name = "TestCRUDProfile";

            await service.SaveProfileAsync(profile);

            var all = await service.LoadAllProfilesAsync();
            var loaded = all.FirstOrDefault(p => p.Id == profile.Id);
            if (loaded == null) return false;
            if (loaded.Name != "TestCRUDProfile") return false;

            service.DeleteProfile(profile.Id);
            all = await service.LoadAllProfilesAsync();
            return all.FirstOrDefault(p => p.Id == profile.Id) == null;
        }

        private static bool TestGestureManagerViewModel()
        {
            var service = new GestureProfileService();
            var vm = new GestureManagerViewModel(service);

            if (vm.SelectedProfile == null) return false;
            if (vm.Bindings.Count == 0) return false;

            var firstBinding = vm.Bindings[0];
            firstBinding.Sensitivity = 1.8;
            firstBinding.IsEnabled = false;

            return Math.Abs(firstBinding.Sensitivity - 1.8) < 0.01 && !firstBinding.IsEnabled;
        }

        private static bool TestWorkflowEditorViewModel()
        {
            var vm = new WorkflowEditorViewModel();

            if (vm.Nodes.Count != 0) return false;
            if (vm.Connections.Count != 0) return false;

            vm.AddActionCommand.Execute(null);
            if (vm.Nodes.Count != 1) return false;
            if (vm.Nodes[0].NodeType != WorkflowNodeType.Action) return false;

            vm.AddDelayCommand.Execute(null);
            if (vm.Nodes.Count != 2) return false;
            if (vm.Nodes[1].NodeType != WorkflowNodeType.Delay) return false;

            if (vm.Connections.Count != 1) return false;
            if (vm.Connections[0].SourceId != vm.Nodes[0].Id) return false;
            return vm.Connections[0].TargetId == vm.Nodes[1].Id;
        }

        private static bool TestPerformanceDashboardViewModel()
        {
            using var vm = new PerformanceDashboardViewModel();

            if (!vm.IsMonitoring) return false;
            if (string.IsNullOrEmpty(vm.FpsDisplay)) return false;
            if (string.IsNullOrEmpty(vm.CpuDisplay)) return false;
            if (string.IsNullOrEmpty(vm.MemDisplay)) return false;

            vm.StopCommand.Execute(null);
            return !vm.IsMonitoring;
        }

        private static bool TestPluginBrowserViewModel()
        {
            var registry = new PluginRegistry();
            var context = new MockApplicationContextEngine();
            var sdkHost = new MockSdkHost();
            var manager = new PluginManager(registry, context, sdkHost);
            var config = new AppConfig();

            var vm = new PluginBrowserViewModel(manager, config);

            if (vm.Plugins.Count != 0) return false;

            var mockPlugin = new MockPlugin();
            registry.Register(mockPlugin);

            vm.RefreshCommand.Execute(null);

            if (vm.Plugins.Count != 1) return false;
            var pluginInfo = vm.Plugins[0];
            if (pluginInfo.Name != "MockPlugin") return false;
            if (!pluginInfo.IsEnabled) return false;

            pluginInfo.IsEnabled = false;
            return config.DisabledPlugins.Contains("MockPlugin") && !pluginInfo.IsEnabled;
        }
    }

    // ── Mocks ────────────────────────────────────────────────────────────────

    internal class MockApplicationContextEngine : IApplicationContextEngine
    {
#pragma warning disable CS0067
        public event EventHandler<ApplicationChangedEventArgs>? ApplicationChanged;
#pragma warning restore CS0067
        public ApplicationContext CurrentContext => ApplicationContext.Empty;
        public void Start() { }
        public void Stop() { }
        public void Dispose() { }
    }

    internal class MockSdkHost : ISdkHost
    {
        public SdkVersion Version => new SdkVersion(1, 0, 0);
        public SdkMetadata Metadata => new SdkMetadata();
        public AppConfig Configuration => new AppConfig();
        public bool IsCompatible(string minimumVersion) => true;
        public bool IsFeatureAvailable(string featureKey) => true;
#pragma warning disable CS0067
        public event EventHandler? HostChanged;
#pragma warning restore CS0067
    }

    internal class MockPlugin : IPlugin
    {
        public PluginDescriptor Descriptor => new PluginDescriptor
        {
            Name = "MockPlugin",
            Version = "1.0.0",
            Author = "UnitTest",
            Description = "Mocking plugin for testing purposes."
        };
        public bool Initialize() => true;
        public void Start() { }
        public void Stop() { }
        public void Dispose() { }
    }
}
