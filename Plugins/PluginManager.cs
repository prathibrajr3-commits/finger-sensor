using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AirGestureAI.Compatibility;
using AirGestureAI.GestureRecognition;
using AirGestureAI.Models;
using AirGestureAI.SDK;
using AirGestureAI.Services;
using AirGestureAI.Utilities;

namespace AirGestureAI.Plugins
{
    /// <summary>
    /// Discovers, loads, initializes, and coordinates the lifecycle of all plugins.
    /// Isolates plugin failures — a crashing plugin never terminates the host application.
    /// </summary>
    public sealed class PluginManager : IDisposable
    {
        // ── Core version checked against plugin MinCoreVersion requirements ──
        private static readonly Version CoreVersion = new Version(1, 1, 0);

        private readonly PluginRegistry _registry;
        private readonly IApplicationContextEngine _contextEngine;
        private readonly ISdkHost _sdkHost;
        private readonly CompatibilityChecker _compatibilityChecker;
        private readonly List<PluginLoadResult> _loadResults = new();
        private readonly object _lifecycleLock = new object();

        private bool _isStarted;
        private string _pluginsDirectory = string.Empty;

        // ── Events ────────────────────────────────────────────────────────────

        /// <summary>Raised when plugin load results change (for diagnostics UI).</summary>
        public event Action? PluginsChanged;

        // ── Public Properties ─────────────────────────────────────────────────

        /// <summary>Gets the plugin registry.</summary>
        public PluginRegistry Registry => _registry;

        /// <summary>Gets a snapshot of load results for diagnostics display.</summary>
        public IReadOnlyList<PluginLoadResult> LoadResults
        {
            get { lock (_lifecycleLock) { return _loadResults.ToArray(); } }
        }

        /// <summary>
        /// Initializes the <see cref="PluginManager"/> with its required dependencies.
        /// </summary>
        public PluginManager(PluginRegistry registry, IApplicationContextEngine contextEngine, ISdkHost sdkHost)
        {
            _registry             = registry      ?? throw new ArgumentNullException(nameof(registry));
            _contextEngine        = contextEngine ?? throw new ArgumentNullException(nameof(contextEngine));
            _sdkHost              = sdkHost       ?? throw new ArgumentNullException(nameof(sdkHost));
            _compatibilityChecker = new CompatibilityChecker(sdkHost);

            // Subscribe to context changes so we can route them to adapters
            _contextEngine.ApplicationChanged += OnApplicationChanged;
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        /// <summary>
        /// Discovers and loads plugins from the given directory path,
        /// then starts the context engine and all loaded adapters.
        /// </summary>
        public void Start(string pluginsDirectory)
        {
            lock (_lifecycleLock)
            {
                if (_isStarted) return;
                _isStarted      = true;
                _pluginsDirectory = pluginsDirectory;
            }

            Logger.Info($"PluginManager starting. Plugins directory: '{pluginsDirectory}'");

            // Discover and load plugins
            DiscoverPlugins(pluginsDirectory);

            // Start all successfully loaded plugins
            foreach (var result in _loadResults.Where(r => r.Success && r.Plugin != null))
            {
                SafePluginCall(result.Plugin!, p => p.Start(), "Start");
            }

            // Start the context engine polling
            _contextEngine.Start();

            Logger.Info($"PluginManager started. {_registry.Count} plugin(s) active.");
            PluginsChanged?.Invoke();
        }

        /// <summary>Stops the context engine and all plugins cleanly.</summary>
        public void Stop()
        {
            lock (_lifecycleLock)
            {
                if (!_isStarted) return;
                _isStarted = false;
            }

            Logger.Info("PluginManager stopping...");

            _contextEngine.Stop();

            foreach (var result in _loadResults.Where(r => r.Success && r.Plugin != null))
            {
                SafePluginCall(result.Plugin!, p => p.Stop(), "Stop");
            }

            Logger.Info("PluginManager stopped.");
        }

        public void Dispose()
        {
            Stop();
            _contextEngine.ApplicationChanged -= OnApplicationChanged;

            foreach (var result in _loadResults.Where(r => r.Success && r.Plugin != null))
            {
                SafePluginCall(result.Plugin!, p => p.Dispose(), "Dispose");
            }

            _loadResults.Clear();
        }

        // ── Plugin Discovery & Loading ─────────────────────────────────────────

        private void DiscoverPlugins(string directory)
        {
            if (!Directory.Exists(directory))
            {
                Logger.Info($"PluginManager: Plugins directory does not exist — skipping discovery: '{directory}'");
                return;
            }

            var dllFiles = Directory.GetFiles(directory, "*.dll", SearchOption.TopDirectoryOnly);
            Logger.Info($"PluginManager: Found {dllFiles.Length} DLL file(s) in plugins directory.");

            foreach (var dll in dllFiles)
            {
                var result = LoadPlugin(dll);
                lock (_lifecycleLock) { _loadResults.Add(result); }

                if (result.Success && result.Plugin != null)
                {
                    _registry.Register(result.Plugin);
                }
            }
        }

        private PluginLoadResult LoadPlugin(string assemblyPath)
        {
            Logger.Info($"PluginManager: Loading plugin from '{Path.GetFileName(assemblyPath)}'...");

            try
            {
                var assembly = Assembly.LoadFrom(assemblyPath);

                // ── Phase 24: SDK compatibility check ─────────────────────────
                var compatReport = _compatibilityChecker.Check(assembly, _sdkHost.Version.ToString());
                if (!compatReport.IsCompatible)
                {
                    Logger.Warn($"PluginManager: Compatibility issues for '{Path.GetFileName(assemblyPath)}': {string.Join("; ", compatReport.Errors)}");
                }
                foreach (var warning in compatReport.Warnings)
                    Logger.Warn($"PluginManager (SDK): {warning}");
                foreach (var dep in compatReport.DeprecatedApis)
                    Logger.Warn($"PluginManager (Deprecated): {dep}");
                // ─────────────────────────────────────────────────────────────

                // Find all public types implementing IPlugin
                var pluginTypes = assembly.GetExportedTypes()
                    .Where(t => !t.IsAbstract && !t.IsInterface && typeof(IPlugin).IsAssignableFrom(t))
                    .ToList();

                if (pluginTypes.Count == 0)
                {
                    return PluginLoadResult.Failed(assemblyPath, "Assembly contains no IPlugin implementations.");
                }

                // Instantiate first plugin type found
                var type   = pluginTypes[0];
                var plugin = (IPlugin)Activator.CreateInstance(type)!;

                // Validate version compatibility
                if (!IsCompatible(plugin.Descriptor.MinCoreVersion))
                {
                    return PluginLoadResult.Failed(assemblyPath,
                        $"Plugin requires AirGesture AI v{plugin.Descriptor.MinCoreVersion} but core is v{CoreVersion}.",
                        plugin.Descriptor);
                }

                // Initialize
                bool ok = false;
                try { ok = plugin.Initialize(); }
                catch (Exception ex)
                {
                    Logger.Error($"PluginManager: Plugin '{plugin.Descriptor.Name}' threw during Initialize()", ex);
                    return PluginLoadResult.Failed(assemblyPath, $"Initialize() threw: {ex.Message}", plugin.Descriptor);
                }

                if (!ok)
                {
                    return PluginLoadResult.Failed(assemblyPath, "Plugin.Initialize() returned false.", plugin.Descriptor);
                }

                Logger.Info($"PluginManager: Successfully loaded '{plugin.Descriptor.Name}' v{plugin.Descriptor.Version}.");
                return PluginLoadResult.Succeeded(plugin, assemblyPath);
            }
            catch (Exception ex)
            {
                Logger.Error($"PluginManager: Failed to load assembly '{Path.GetFileName(assemblyPath)}'", ex);
                return PluginLoadResult.Failed(assemblyPath, ex.Message);
            }
        }

        private static bool IsCompatible(string minVersionStr)
        {
            if (!Version.TryParse(minVersionStr, out var minVersion)) return true; // Assume compatible if unparseable
            return CoreVersion >= minVersion;
        }

        // ── Gesture Routing ──────────────────────────────────────────────────

        /// <summary>
        /// Routes a recognized gesture through relevant adapters for the current context.
        /// Returns true if any adapter suppressed the default action.
        /// </summary>
        public bool RouteGesture(GestureType gesture, ApplicationContext context)
        {
            var adapters = _registry.FindAdaptersForApp(context.AppType);
            bool anyOverride = false;

            foreach (var adapter in adapters)
            {
                try
                {
                    var result = adapter.ProcessGesture(gesture, context, out bool overrideDefault);
                    if (overrideDefault) anyOverride = true;
                    if (result != null)
                    {
                        Logger.Info($"PluginManager: Adapter '{adapter.Descriptor.Name}' handled gesture {gesture}: {result}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"PluginManager: Adapter '{adapter.Descriptor.Name}' threw during gesture routing", ex);
                    // Isolated — continue to next adapter
                }
            }

            return anyOverride;
        }

        // ── Context Event Routing ─────────────────────────────────────────────

        private void OnApplicationChanged(object? sender, ApplicationChangedEventArgs e)
        {
            var adapters = _registry.FindAdaptersForApp(e.NewContext.AppType);
            foreach (var adapter in adapters)
            {
                SafePluginCall(adapter, a => a.OnApplicationChanged(e.NewContext), "OnApplicationChanged");
            }
        }

        // ── Safety Helpers ────────────────────────────────────────────────────

        private static void SafePluginCall<T>(T plugin, Action<T> action, string methodName) where T : IPlugin
        {
            try
            {
                action(plugin);
            }
            catch (Exception ex)
            {
                Logger.Error($"PluginManager: Plugin '{plugin.Descriptor.Name}'.{methodName}() threw an exception (isolated)", ex);
            }
        }
    }
}
