using System;
using System.Collections.Generic;

namespace AirGestureAI.Extensions
{
    /// <summary>
    /// Describes the contents, authorship, and version requirements of an
    /// <c>.airgesture-extension</c> package.
    /// </summary>
    public sealed class ExtensionManifest
    {
        /// <summary>Gets or sets the extension unique identifier (reverse-DNS style).</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>Gets or sets the human-readable display name.</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>Gets or sets the extension description.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Gets or sets the author name.</summary>
        public string Author { get; set; } = string.Empty;

        /// <summary>Gets or sets the extension version string (SemVer).</summary>
        public string Version { get; set; } = "1.0.0";

        /// <summary>Gets or sets the minimum SDK version required (SemVer).</summary>
        public string MinSdkVersion { get; set; } = "1.0.0";

        /// <summary>Gets or sets the entry-point DLL filename (relative to package root).</summary>
        public string EntryAssembly { get; set; } = string.Empty;

        /// <summary>Gets or sets the icon filename within the package.</summary>
        public string Icon { get; set; } = "icon.png";

        /// <summary>Gets or sets the list of dependency extension IDs required.</summary>
        public List<string> Dependencies { get; set; } = new();

        /// <summary>Gets or sets a dictionary of arbitrary metadata key/value pairs.</summary>
        public Dictionary<string, string> Metadata { get; set; } = new();

        /// <summary>Gets or sets the SHA-256 hex checksum of the package archive.</summary>
        public string PackageChecksum { get; set; } = string.Empty;

        /// <summary>Gets or sets the digital signature of the manifest JSON bytes.</summary>
        public string DigitalSignature { get; set; } = string.Empty;

        /// <summary>Gets or sets the UTC publication timestamp.</summary>
        public DateTime PublishedAtUtc { get; set; } = DateTime.UtcNow;

        /// <inheritdoc/>
        public override string ToString() => $"{DisplayName} v{Version} by {Author} (requires SDK ≥ {MinSdkVersion})";
    }
}
