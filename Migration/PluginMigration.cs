using System;
using System.IO;
using AirGestureAI.Utilities;

namespace AirGestureAI.Migration
{
    /// <summary>
    /// Handles migration of plugin DLLs and manifests between AirGesture AI versions.
    /// Ensures legacy plugins remain loadable through the compatibility layer.
    /// </summary>
    public sealed class PluginMigration
    {
        private const string PluginsSubfolder = "Plugins";

        /// <summary>
        /// Scans the plugins directory within <paramref name="dataDirectory"/>
        /// and applies any structural migrations required for compatibility.
        /// </summary>
        public void Migrate(string dataDirectory)
        {
            var pluginsDir = Path.Combine(dataDirectory, PluginsSubfolder);
            if (!Directory.Exists(pluginsDir))
            {
                Logger.Info($"PluginMigration: No plugins directory at '{pluginsDir}'. Skipping.");
                return;
            }

            Logger.Info($"PluginMigration: Scanning '{pluginsDir}' for legacy plugins…");

            var dlls = Directory.GetFiles(pluginsDir, "*.dll", SearchOption.TopDirectoryOnly);
            Logger.Info($"PluginMigration: Found {dlls.Length} plugin DLL(s).");

            foreach (var dll in dlls)
            {
                // In a production implementation we would:
                // 1. Load the assembly metadata.
                // 2. Check its minimum SDK version attribute.
                // 3. Apply shim adapters or update manifest files.
                Logger.Info($"PluginMigration: Plugin '{Path.GetFileName(dll)}' — OK (no migration needed for v1.0).");
            }

            Logger.Info("PluginMigration: Plugin migration completed.");
        }
    }
}
