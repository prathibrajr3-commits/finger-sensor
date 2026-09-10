using System;
using System.IO;

namespace AirGestureAI.Extensions
{
    /// <summary>
    /// Represents a loaded or inspected <c>.airgesture-extension</c> archive.
    /// The archive is a ZIP file renamed with the <c>.airgesture-extension</c> extension.
    /// </summary>
    public sealed class ExtensionPackage
    {
        /// <summary>Gets the absolute path to the package file on disk.</summary>
        public string FilePath { get; }

        /// <summary>Gets the filename (basename) of the package.</summary>
        public string FileName => Path.GetFileName(FilePath);

        /// <summary>Gets the parsed manifest describing this package.</summary>
        public ExtensionManifest Manifest { get; }

        /// <summary>Gets the directory where this package was extracted.</summary>
        public string? ExtractedDirectory { get; internal set; }

        /// <summary>Gets whether the package has been extracted and is ready to load.</summary>
        public bool IsExtracted => ExtractedDirectory is not null && Directory.Exists(ExtractedDirectory);

        /// <summary>Gets the UTC time when this package object was created.</summary>
        public DateTime LoadedAtUtc { get; } = DateTime.UtcNow;

        /// <summary>
        /// Initializes a new <see cref="ExtensionPackage"/>.
        /// </summary>
        public ExtensionPackage(string filePath, ExtensionManifest manifest)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path must not be empty.", nameof(filePath));

            FilePath = filePath;
            Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        }

        /// <inheritdoc/>
        public override string ToString() => $"{Manifest.DisplayName} v{Manifest.Version} [{FileName}]";
    }
}
