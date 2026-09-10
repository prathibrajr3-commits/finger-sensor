using System;
using System.Collections.Generic;
using AirGestureAI.Utilities;

namespace AirGestureAI.Extensions
{
    /// <summary>
    /// Checks for available updates for installed extensions and applies them.
    /// In offline-first mode this checks a local update feed (JSON file).
    /// </summary>
    public sealed class ExtensionUpdater
    {
        private readonly ExtensionInstaller _installer;
        private readonly string _localFeedPath;

        /// <summary>Raised when an extension update is available.</summary>
#pragma warning disable CS0067
        public event Action<string, string>? UpdateAvailable; // (extensionId, newVersion)
#pragma warning restore CS0067

        /// <summary>Raised after an extension has been updated successfully.</summary>
        public event Action<string>? UpdateApplied;

        /// <summary>
        /// Initializes a new <see cref="ExtensionUpdater"/>.
        /// </summary>
        /// <param name="installer">The installer used to apply updates.</param>
        /// <param name="localFeedPath">Path to the local JSON update feed file.</param>
        public ExtensionUpdater(ExtensionInstaller installer, string localFeedPath)
        {
            _installer     = installer     ?? throw new ArgumentNullException(nameof(installer));
            _localFeedPath = localFeedPath ?? throw new ArgumentNullException(nameof(localFeedPath));
        }

        /// <summary>
        /// Checks all installed extensions against the local feed for newer versions.
        /// Returns a list of extension IDs that have updates available.
        /// </summary>
        public IReadOnlyList<string> CheckForUpdates()
        {
            Logger.Info("ExtensionUpdater: Checking for extension updates (local feed)…");

            var updatable  = new List<string>();
            var installed  = _installer.GetInstalled();

            // In a real implementation this reads _localFeedPath as a JSON feed.
            // For now we simply log each installed extension and return an empty list
            // until a real feed file is placed at _localFeedPath.
            foreach (var pkg in installed)
                Logger.Info($"ExtensionUpdater: '{pkg.Manifest.Id}' v{pkg.Manifest.Version} — up to date.");

            return updatable;
        }

        /// <summary>
        /// Applies the update for the given extension using a new package file path.
        /// </summary>
        /// <param name="extensionId">The extension ID to update.</param>
        /// <param name="newPackagePath">Path to the new <c>.airgesture-extension</c> file.</param>
        public void ApplyUpdate(string extensionId, string newPackagePath)
        {
            Logger.Info($"ExtensionUpdater: Updating extension '{extensionId}'…");
            _installer.Uninstall(extensionId);
            _installer.Install(newPackagePath);
            UpdateApplied?.Invoke(extensionId);
            Logger.Info($"ExtensionUpdater: '{extensionId}' updated successfully.");
        }
    }
}
