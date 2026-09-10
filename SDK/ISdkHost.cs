using System;
using AirGestureAI.Configuration;

namespace AirGestureAI.SDK
{
    /// <summary>
    /// Defines the host environment exposed to extensions and plugins through the public SDK.
    /// Provides access to the core SDK version, feature gates, and host services.
    /// </summary>
    public interface ISdkHost
    {
        /// <summary>Gets the current SDK version.</summary>
        SdkVersion Version { get; }

        /// <summary>Gets metadata describing the host application build.</summary>
        SdkMetadata Metadata { get; }

        /// <summary>Gets the active application configuration.</summary>
        AppConfig Configuration { get; }

        /// <summary>
        /// Determines whether the host is compatible with the specified minimum SDK version.
        /// </summary>
        /// <param name="minimumVersion">The minimum required version string (SemVer).</param>
        /// <returns>True if the host satisfies the version requirement; otherwise false.</returns>
        bool IsCompatible(string minimumVersion);

        /// <summary>
        /// Determines whether a specific named feature is available in the current host.
        /// </summary>
        /// <param name="featureKey">The feature identifier (e.g. "VoiceCommands", "Vision").</param>
        bool IsFeatureAvailable(string featureKey);

        /// <summary>Raised when the host version or feature set changes at runtime.</summary>
        event EventHandler? HostChanged;
    }
}
