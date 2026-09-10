using System;

namespace AirGestureAI.Plugins
{
    /// <summary>
    /// Captures the outcome of a plugin load attempt.
    /// </summary>
    public sealed class PluginLoadResult
    {
        /// <summary>Gets the loaded plugin instance, or null on failure.</summary>
        public IPlugin? Plugin { get; }

        /// <summary>Gets the plugin descriptor (may be populated even on failure if metadata was readable).</summary>
        public PluginDescriptor? Descriptor { get; }

        /// <summary>Gets whether the plugin loaded and initialized successfully.</summary>
        public bool Success { get; }

        /// <summary>Gets the error message if loading failed, or null on success.</summary>
        public string? ErrorMessage { get; }

        /// <summary>Gets the source assembly path that was loaded.</summary>
        public string AssemblyPath { get; }

        private PluginLoadResult(IPlugin? plugin, PluginDescriptor? descriptor, bool success, string? errorMessage, string assemblyPath)
        {
            Plugin        = plugin;
            Descriptor    = descriptor;
            Success       = success;
            ErrorMessage  = errorMessage;
            AssemblyPath  = assemblyPath;
        }

        /// <summary>Creates a successful load result.</summary>
        public static PluginLoadResult Succeeded(IPlugin plugin, string assemblyPath) =>
            new(plugin, plugin.Descriptor, true, null, assemblyPath);

        /// <summary>Creates a failed load result.</summary>
        public static PluginLoadResult Failed(string assemblyPath, string errorMessage, PluginDescriptor? descriptor = null) =>
            new(null, descriptor, false, errorMessage, assemblyPath);

        /// <inheritdoc/>
        public override string ToString() =>
            Success
                ? $"[PluginLoadResult OK: {Descriptor?.Name ?? "Unknown"} from {System.IO.Path.GetFileName(AssemblyPath)}]"
                : $"[PluginLoadResult FAILED: {System.IO.Path.GetFileName(AssemblyPath)} — {ErrorMessage}]";
    }
}
