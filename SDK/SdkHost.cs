using System;
using System.Collections.Generic;
using AirGestureAI.Configuration;
using AirGestureAI.Utilities;

namespace AirGestureAI.SDK
{
    /// <summary>
    /// Concrete implementation of <see cref="ISdkHost"/>.
    /// Exposes the current SDK version, build metadata, and feature availability
    /// to plugins and extensions loaded at runtime.
    /// </summary>
    public sealed class SdkHost : ISdkHost
    {
        // ── Current SDK Version ───────────────────────────────────────────────
        private static readonly SdkVersion CurrentVersion = new(1, 0, 0, "rc1");

        private readonly AppConfig _config;
        private readonly SdkMetadata _metadata;
        private readonly HashSet<string> _availableFeatures;

        /// <inheritdoc/>
        public SdkVersion Version => CurrentVersion;

        /// <inheritdoc/>
        public SdkMetadata Metadata => _metadata;

        /// <inheritdoc/>
        public AppConfig Configuration => _config;

        /// <inheritdoc/>
#pragma warning disable CS0067
        public event EventHandler? HostChanged;
#pragma warning restore CS0067

        /// <summary>
        /// Initializes the <see cref="SdkHost"/> with application configuration.
        /// </summary>
        public SdkHost(AppConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            _metadata = new SdkMetadata
            {
                ProductName    = "AirGesture AI",
                Publisher      = "AirGesture AI Project",
                BuildDateUtc   = DateTime.UtcNow,
                TargetPlatform = "win-x64",
                TargetFramework = "net8.0-windows",
                IsReleaseBuild = false,
                EnabledFeatures = BuildFeatureList(config),
            };

            _availableFeatures = new HashSet<string>(
                _metadata.EnabledFeatures, StringComparer.OrdinalIgnoreCase);

            Logger.Info($"SdkHost initialized. SDK version: {CurrentVersion}");
        }

        /// <inheritdoc/>
        public bool IsCompatible(string minimumVersion)
        {
            if (!SdkVersion.TryParse(minimumVersion, out var required) || required is null)
            {
                Logger.Warn($"SdkHost.IsCompatible: Cannot parse version '{minimumVersion}'.");
                return false;
            }
            return CurrentVersion.IsCompatibleWith(required);
        }

        /// <inheritdoc/>
        public bool IsFeatureAvailable(string featureKey) =>
            _availableFeatures.Contains(featureKey);

        // ── Helpers ───────────────────────────────────────────────────────────

        private static IReadOnlyList<string> BuildFeatureList(AppConfig cfg)
        {
            var features = new List<string>
            {
                "Core",
                "Camera",
                "GestureRecognition",
                "CursorEngine",
                "HoverSelection",
                "Plugins",
                "ApplicationContext",
            };
            // Feature-gating based on AppConfig flags added in later phases
            return features;
        }
    }
}
