using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using AirGestureAI.Models;
using AirGestureAI.Utilities;

namespace AirGestureAI.Plugins
{
    /// <summary>
    /// Thread-safe registry for loaded plugins and application adapters.
    /// Supports registration, lookup by name, and lookup by ApplicationType.
    /// </summary>
    public sealed class PluginRegistry
    {
        // Primary plugin store indexed by plugin name (case-insensitive)
        private readonly ConcurrentDictionary<string, IPlugin> _pluginsByName =
            new(StringComparer.OrdinalIgnoreCase);

        // Adapter lookup indexed by ApplicationType for O(1) dispatch
        private readonly ConcurrentDictionary<ApplicationType, List<IApplicationAdapter>> _adaptersByAppType = new();

        // ── Registration ─────────────────────────────────────────────────────

        /// <summary>
        /// Registers a plugin. Ignores duplicates (same Name) without throwing.
        /// Returns false if the plugin was already registered.
        /// </summary>
        public bool Register(IPlugin plugin)
        {
            if (plugin == null) throw new ArgumentNullException(nameof(plugin));

            string name = plugin.Descriptor.Name;
            if (_pluginsByName.ContainsKey(name))
            {
                Logger.Warn($"PluginRegistry: Duplicate registration skipped for '{name}'.");
                return false;
            }

            _pluginsByName[name] = plugin;
            Logger.Info($"PluginRegistry: Registered plugin '{name}' v{plugin.Descriptor.Version}.");

            // If this is an adapter, also index by each supported ApplicationType
            if (plugin is IApplicationAdapter adapter)
            {
                foreach (var appType in adapter.Descriptor.SupportedApplications)
                {
                    _adaptersByAppType.AddOrUpdate(
                        appType,
                        _ => new List<IApplicationAdapter> { adapter },
                        (_, existing) =>
                        {
                            lock (existing) { existing.Add(adapter); }
                            return existing;
                        });
                }
            }

            return true;
        }

        /// <summary>
        /// Unregisters a plugin by name. No-ops if not found.
        /// </summary>
        public void Unregister(string name)
        {
            if (!_pluginsByName.TryRemove(name, out var plugin))
            {
                Logger.Warn($"PluginRegistry: Attempted to unregister unknown plugin '{name}'.");
                return;
            }

            // Remove from adapter index as well
            if (plugin is IApplicationAdapter adapter)
            {
                foreach (var appType in adapter.Descriptor.SupportedApplications)
                {
                    if (_adaptersByAppType.TryGetValue(appType, out var list))
                    {
                        lock (list) { list.Remove(adapter); }
                    }
                }
            }

            Logger.Info($"PluginRegistry: Unregistered plugin '{name}'.");
        }

        // ── Lookups ───────────────────────────────────────────────────────────

        /// <summary>Returns the plugin with the given name, or null.</summary>
        public IPlugin? FindByName(string name) =>
            _pluginsByName.TryGetValue(name, out var p) ? p : null;

        /// <summary>
        /// Returns all registered adapters that support the specified ApplicationType.
        /// </summary>
        public IReadOnlyList<IApplicationAdapter> FindAdaptersForApp(ApplicationType appType)
        {
            if (_adaptersByAppType.TryGetValue(appType, out var list))
            {
                lock (list) { return list.ToArray(); }
            }
            return Array.Empty<IApplicationAdapter>();
        }

        /// <summary>Returns a snapshot of all registered plugins.</summary>
        public IReadOnlyList<IPlugin> AllPlugins => _pluginsByName.Values.ToArray();

        /// <summary>Returns a snapshot of all registered adapters.</summary>
        public IReadOnlyList<IApplicationAdapter> AllAdapters =>
            _pluginsByName.Values.OfType<IApplicationAdapter>().ToArray();

        /// <summary>Total registered plugin count.</summary>
        public int Count => _pluginsByName.Count;
    }
}
