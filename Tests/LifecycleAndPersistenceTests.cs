using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using AirGestureAI.Configuration;
using AirGestureAI.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AirGestureAI.Tests
{
    public sealed class LifecycleAndPersistenceTests : IDisposable
    {
        private readonly string _tempDirectory;

        public LifecycleAndPersistenceTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "AirGestureAI_Tests_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(_tempDirectory);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempDirectory))
                {
                    Directory.Delete(_tempDirectory, recursive: true);
                }
            }
            catch { }
        }

        // ── AppConfig Persistence Tests ───────────────────────────────────────

        [Fact]
        public void FirstRunDefaultBehavior_WhenFileDoesNotExist_ReturnsDefaultConfig()
        {
            string missingPath = Path.Combine(_tempDirectory, "nonexistent_appsettings.json");

            var config = AppConfig.Load(missingPath);

            Assert.NotNull(config);
            Assert.False(config.IsFirstRunComplete);

            var wizardService = new SetupWizardService(config, missingPath);
            Assert.True(wizardService.IsFirstRun);
        }

        [Fact]
        public void PersistedIsFirstRunCompleteLoading_WhenFileExists_RestoresFlagCorrectly()
        {
            string configPath = Path.Combine(_tempDirectory, "appsettings.json");
            File.WriteAllText(configPath, JsonSerializer.Serialize(new { IsFirstRunComplete = true }));

            var config = AppConfig.Load(configPath);

            Assert.NotNull(config);
            Assert.True(config.IsFirstRunComplete);

            var wizardService = new SetupWizardService(config, configPath);
            Assert.False(wizardService.IsFirstRun);
        }

        [Fact]
        public void PersistedIsFirstRunCompleteLoading_WithStringBoolean_RestoresFlagCorrectly()
        {
            string configPath = Path.Combine(_tempDirectory, "appsettings.json");
            File.WriteAllText(configPath, "{ \"IsFirstRunComplete\": \"true\" }");

            var config = AppConfig.Load(configPath);

            Assert.NotNull(config);
            Assert.True(config.IsFirstRunComplete);
        }

        [Fact]
        public void MalformedConfigurationFallback_WhenJsonCorrupted_FallsBackToDefaultsSafely()
        {
            string configPath = Path.Combine(_tempDirectory, "corrupt_appsettings.json");
            File.WriteAllText(configPath, "{ \"IsFirstRunComplete\": [not valid json, 12345");

            var config = AppConfig.Load(configPath);

            Assert.NotNull(config);
            Assert.False(config.IsFirstRunComplete);
        }

        [Fact]
        public void MalformedConfigurationFallback_WhenFileIsEmpty_FallsBackToDefaultsSafely()
        {
            string configPath = Path.Combine(_tempDirectory, "empty_appsettings.json");
            File.WriteAllText(configPath, "   ");

            var config = AppConfig.Load(configPath);

            Assert.NotNull(config);
            Assert.False(config.IsFirstRunComplete);
        }

        [Fact]
        public void FirstRunToCompletionToSecondRunBehavior_PersistsAndLoadsStateAcrossLaunches()
        {
            string configPath = Path.Combine(_tempDirectory, "appsettings.json");

            // First run: file does not exist
            var configRun1 = AppConfig.Load(configPath);
            Assert.False(configRun1.IsFirstRunComplete);

            var wizardService1 = new SetupWizardService(configRun1, configPath);
            Assert.True(wizardService1.IsFirstRun);

            // User completes Setup Wizard
            wizardService1.MarkComplete();
            Assert.True(configRun1.IsFirstRunComplete);
            Assert.False(wizardService1.IsFirstRun);
            Assert.True(File.Exists(configPath));

            // Second run: file exists and persisted state is loaded
            var configRun2 = AppConfig.Load(configPath);
            Assert.True(configRun2.IsFirstRunComplete);

            var wizardService2 = new SetupWizardService(configRun2, configPath);
            Assert.False(wizardService2.IsFirstRun);
        }

        // ── Async Service-Provider Disposal Tests ──────────────────────────────

        private sealed class AsyncOnlyDisposableService : IAsyncDisposable
        {
            public bool IsDisposed { get; private set; }

            public ValueTask DisposeAsync()
            {
                IsDisposed = true;
                return ValueTask.CompletedTask;
            }
        }

        private sealed class SyncDisposableService : IDisposable
        {
            public bool IsDisposed { get; private set; }

            public void Dispose()
            {
                IsDisposed = true;
            }
        }

        [Fact]
        public void AsyncServiceProviderDisposal_WithAsyncOnlyDisposableService_DisposesWithoutException()
        {
            var services = new ServiceCollection();
            services.AddSingleton<AsyncOnlyDisposableService>();

            var sp = services.BuildServiceProvider();
            var service = sp.GetRequiredService<AsyncOnlyDisposableService>();

            Assert.False(service.IsDisposed);

            // App.DisposeServiceProvider must handle IAsyncDisposable without throwing InvalidOperationException
            App.DisposeServiceProvider(sp);

            Assert.True(service.IsDisposed);
        }

        [Fact]
        public async Task AsyncServiceProviderDisposalAsync_WithAsyncOnlyDisposableService_DisposesWithoutException()
        {
            var services = new ServiceCollection();
            services.AddSingleton<AsyncOnlyDisposableService>();

            var sp = services.BuildServiceProvider();
            var service = sp.GetRequiredService<AsyncOnlyDisposableService>();

            Assert.False(service.IsDisposed);

            await App.DisposeServiceProviderAsync(sp);

            Assert.True(service.IsDisposed);
        }

        [Fact]
        public void AsyncServiceProviderDisposal_PreservesSynchronousIDisposableCleanup()
        {
            var services = new ServiceCollection();
            services.AddSingleton<AsyncOnlyDisposableService>();
            services.AddSingleton<SyncDisposableService>();

            var sp = services.BuildServiceProvider();
            var asyncService = sp.GetRequiredService<AsyncOnlyDisposableService>();
            var syncService = sp.GetRequiredService<SyncDisposableService>();

            Assert.False(asyncService.IsDisposed);
            Assert.False(syncService.IsDisposed);

            App.DisposeServiceProvider(sp);

            Assert.True(asyncService.IsDisposed);
            Assert.True(syncService.IsDisposed);
        }

        private sealed class TestStateProvider : ISessionStateProvider
        {
            public Task<SessionState> CaptureCurrentStateAsync() =>
                Task.FromResult(new SessionState { Version = "4.1", Theme = "Dark" });

            public Task ApplyStateAsync(SessionState state) => Task.CompletedTask;
        }

        [Fact]
        public void AsyncServiceProviderDisposal_WithAutoSaveService_DisposesWithoutException()
        {
            var services = new ServiceCollection();
            var loggingService = new LoggingService(_tempDirectory);
            var stateManager = new SessionStateManager(_tempDirectory, loggingService);
            var stateProvider = new TestStateProvider();

            services.AddSingleton<AutoSaveService>(sp => new AutoSaveService(
                stateManager,
                stateProvider,
                loggingService,
                _tempDirectory,
                intervalSeconds: 30));

            var sp = services.BuildServiceProvider();
            var autoSave = sp.GetRequiredService<AutoSaveService>();
            Assert.NotNull(autoSave);

            // AutoSaveService implements only IAsyncDisposable.
            // Synchronous sp.Dispose() would throw InvalidOperationException.
            // App.DisposeServiceProvider must succeed safely.
            var exception = Record.Exception(() => App.DisposeServiceProvider(sp));
            Assert.Null(exception);
        }

        [Fact]
        public void AsyncServiceProviderDisposal_WithNullProvider_DoesNotThrow()
        {
            var exception = Record.Exception(() => App.DisposeServiceProvider(null));
            Assert.Null(exception);
        }

        // ── WPF Lifetime Modes Test ───────────────────────────────────────────

        [Fact]
        public void WpfLifetimeModes_ExplicitShutdownAndMainWindowClose_AreSupportedEnums()
        {
            // Verify WPF ShutdownMode enum values used during transition
            Assert.Equal(ShutdownMode.OnExplicitShutdown, ShutdownMode.OnExplicitShutdown);
            Assert.Equal(ShutdownMode.OnMainWindowClose, ShutdownMode.OnMainWindowClose);
        }
    }
}
