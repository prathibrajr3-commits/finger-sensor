using System;
using System.Collections.Generic;
using AirGestureAI.Models;

namespace AirGestureAI.Plugins
{
    /// <summary>
    /// Metadata descriptor for a loaded plugin assembly.
    /// </summary>
    public sealed class PluginDescriptor
    {
        /// <summary>Display name of the plugin.</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>Semantic version string (e.g. "1.0.0").</summary>
        public string Version { get; init; } = string.Empty;

        /// <summary>Plugin author or organization.</summary>
        public string Author { get; init; } = string.Empty;

        /// <summary>Short description of what the plugin does.</summary>
        public string Description { get; init; } = string.Empty;

        /// <summary>
        /// Minimum AirGesture AI core version required by this plugin.
        /// Format: "Major.Minor.Patch"
        /// </summary>
        public string MinCoreVersion { get; init; } = "1.0.0";

        /// <summary>
        /// Application types this adapter supports.
        /// Empty for non-adapter plugins.
        /// </summary>
        public IReadOnlyList<ApplicationType> SupportedApplications { get; init; } =
            Array.Empty<ApplicationType>();

        /// <inheritdoc/>
        public override string ToString() =>
            $"[Plugin '{Name}' v{Version} by {Author}]";
    }
}
