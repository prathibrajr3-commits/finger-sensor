using System;
using System.Collections.Generic;

namespace AirGestureAI.SDK
{
    /// <summary>
    /// Describes the build metadata of the AirGesture AI SDK host.
    /// </summary>
    public sealed class SdkMetadata
    {
        /// <summary>Gets the application product name.</summary>
        public string ProductName { get; init; } = "AirGesture AI";

        /// <summary>Gets the publisher name.</summary>
        public string Publisher { get; init; } = "AirGesture AI Project";

        /// <summary>Gets the build date/time in UTC.</summary>
        public DateTime BuildDateUtc { get; init; } = DateTime.UtcNow;

        /// <summary>Gets the target platform (e.g. "win-x64").</summary>
        public string TargetPlatform { get; init; } = "win-x64";

        /// <summary>Gets the target .NET runtime moniker (e.g. "net8.0-windows").</summary>
        public string TargetFramework { get; init; } = "net8.0-windows";

        /// <summary>Gets the VCS commit hash at the time of the build.</summary>
        public string CommitHash { get; init; } = string.Empty;

        /// <summary>Gets whether this is a release build (false = debug).</summary>
        public bool IsReleaseBuild { get; init; } = false;

        /// <summary>Gets the complete list of feature flags enabled in this build.</summary>
        public IReadOnlyList<string> EnabledFeatures { get; init; } = Array.Empty<string>();

        /// <summary>Returns a human-readable summary of the metadata.</summary>
        public override string ToString() =>
            $"{ProductName} | Platform: {TargetPlatform} | Framework: {TargetFramework} | Build: {(IsReleaseBuild ? "Release" : "Debug")} | Date: {BuildDateUtc:u}";
    }
}
