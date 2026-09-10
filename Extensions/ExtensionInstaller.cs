using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using AirGestureAI.SDK;
using AirGestureAI.Utilities;

namespace AirGestureAI.Extensions
{
    /// <summary>
    /// Installs, lists, and uninstalls <c>.airgesture-extension</c> packages.
    /// An extension package is a ZIP archive (renamed to <c>.airgesture-extension</c>)
    /// containing a <c>manifest.json</c> at its root and one or more DLL files.
    /// </summary>
    public sealed class ExtensionInstaller
    {
        private const string ManifestFileName    = "manifest.json";
        private const string ExtensionsSubfolder = "InstalledExtensions";

        private readonly string _installRoot;
        private readonly ISdkHost _sdkHost;
        private readonly ExtensionValidator _validator;
        private readonly ExtensionDependencyResolver _resolver;

        /// <summary>Raised when an extension is successfully installed.</summary>
        public event Action<ExtensionPackage>? ExtensionInstalled;

        /// <summary>Raised when an extension is uninstalled.</summary>
        public event Action<string>? ExtensionUninstalled;

        /// <summary>
        /// Initializes a new <see cref="ExtensionInstaller"/>.
        /// </summary>
        /// <param name="installRoot">Base directory under which extensions are installed.</param>
        /// <param name="sdkHost">The SDK host used for version validation.</param>
        public ExtensionInstaller(string installRoot, ISdkHost sdkHost)
        {
            _installRoot = installRoot ?? throw new ArgumentNullException(nameof(installRoot));
            _sdkHost     = sdkHost     ?? throw new ArgumentNullException(nameof(sdkHost));
            _validator   = new ExtensionValidator(sdkHost);
            _resolver    = new ExtensionDependencyResolver();

            Directory.CreateDirectory(Path.Combine(installRoot, ExtensionsSubfolder));
        }

        /// <summary>
        /// Installs an extension from the given <c>.airgesture-extension</c> file path.
        /// </summary>
        /// <returns>The installed <see cref="ExtensionPackage"/>.</returns>
        public ExtensionPackage Install(string packageFilePath)
        {
            if (!File.Exists(packageFilePath))
                throw new FileNotFoundException("Extension package not found.", packageFilePath);

            Logger.Info($"ExtensionInstaller: Installing '{Path.GetFileName(packageFilePath)}'…");

            // Parse manifest
            var manifest = ReadManifest(packageFilePath);

            // Validate
            var result = _validator.Validate(manifest);
            if (!result.IsValid)
                throw new InvalidOperationException($"Extension validation failed: {result.Reason}");

            // Resolve dependencies
            var deps = _resolver.Resolve(manifest.Dependencies, GetInstalledIds());
            if (deps.Count > 0)
                Logger.Warn($"ExtensionInstaller: Unresolved dependencies for '{manifest.Id}': {string.Join(", ", deps)}");

            // Extract to install directory
            var targetDir = Path.Combine(_installRoot, ExtensionsSubfolder, manifest.Id);
            if (Directory.Exists(targetDir)) Directory.Delete(targetDir, recursive: true);
            ZipFile.ExtractToDirectory(packageFilePath, targetDir);

            var package = new ExtensionPackage(packageFilePath, manifest)
            {
                ExtractedDirectory = targetDir,
            };

            Logger.Info($"ExtensionInstaller: Installed '{manifest.DisplayName}' v{manifest.Version} → '{targetDir}'");
            ExtensionInstalled?.Invoke(package);
            return package;
        }

        /// <summary>
        /// Uninstalls an extension by its ID.
        /// </summary>
        public void Uninstall(string extensionId)
        {
            var targetDir = Path.Combine(_installRoot, ExtensionsSubfolder, extensionId);
            if (Directory.Exists(targetDir))
            {
                Directory.Delete(targetDir, recursive: true);
                Logger.Info($"ExtensionInstaller: Uninstalled extension '{extensionId}'.");
                ExtensionUninstalled?.Invoke(extensionId);
            }
            else
            {
                Logger.Warn($"ExtensionInstaller: Extension '{extensionId}' not found for uninstall.");
            }
        }

        /// <summary>
        /// Returns a list of all currently installed extension packages.
        /// </summary>
        public IReadOnlyList<ExtensionPackage> GetInstalled()
        {
            var result = new List<ExtensionPackage>();
            var root   = Path.Combine(_installRoot, ExtensionsSubfolder);

            foreach (var dir in Directory.GetDirectories(root))
            {
                var manifestPath = Path.Combine(dir, ManifestFileName);
                if (!File.Exists(manifestPath)) continue;
                try
                {
                    var manifest = JsonSerializer.Deserialize<ExtensionManifest>(
                        File.ReadAllText(manifestPath))!;
                    result.Add(new ExtensionPackage(dir, manifest) { ExtractedDirectory = dir });
                }
                catch (Exception ex)
                {
                    Logger.Warn($"ExtensionInstaller: Failed to read manifest in '{dir}': {ex.Message}");
                }
            }
            return result;
        }

        // ── Private Helpers ───────────────────────────────────────────────────

        private static ExtensionManifest ReadManifest(string archivePath)
        {
            using var archive = ZipFile.OpenRead(archivePath);
            var entry = archive.GetEntry(ManifestFileName)
                ?? throw new InvalidDataException($"Package is missing '{ManifestFileName}'.");

            using var stream = entry.Open();
            return JsonSerializer.Deserialize<ExtensionManifest>(stream)
                ?? throw new InvalidDataException("Failed to deserialize manifest.");
        }

        private IReadOnlyList<string> GetInstalledIds()
        {
            var ids  = new List<string>();
            var root = Path.Combine(_installRoot, ExtensionsSubfolder);
            foreach (var dir in Directory.GetDirectories(root))
                ids.Add(Path.GetFileName(dir));
            return ids;
        }
    }
}
