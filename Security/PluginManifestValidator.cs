using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using AirGestureAI.Security;

namespace AirGestureAI.Security
{
    /// <summary>
    /// Models a deserialized plugin manifest (plugin.json).
    /// </summary>
    internal sealed class PluginManifest
    {
        public string Name    { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Author  { get; set; } = string.Empty;
        public string EntryAssembly { get; set; } = string.Empty;
        public string[] Dependencies { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// Validates plugin.json manifests against naming, versioning, author, and dependency rules.
    /// </summary>
    public static class PluginManifestValidator
    {
        // name: letters, digits, hyphens, underscores — 2 to 64 chars
        private static readonly Regex NameRegex    = new(@"^[a-zA-Z0-9_\-]{2,64}$", RegexOptions.Compiled);
        // semver: major.minor.patch (strict)
        private static readonly Regex VersionRegex = new(@"^\d+\.\d+\.\d+$", RegexOptions.Compiled);

        private static readonly JsonSerializerOptions _options = new()
        {
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Validates the raw JSON content of a plugin.json manifest.
        /// </summary>
        /// <param name="rawJson">The full JSON content of plugin.json.</param>
        /// <param name="existingNames">Set of already-seen plugin names, used for duplicate detection.</param>
        /// <returns>An <see cref="InputValidationResult"/> capturing pass/fail status.</returns>
        public static InputValidationResult Validate(string rawJson, System.Collections.Generic.HashSet<string>? existingNames = null)
        {
            PluginManifest? manifest;
            try
            {
                manifest = JsonSerializer.Deserialize<PluginManifest>(rawJson, _options);
            }
            catch (JsonException ex)
            {
                return Fail($"Malformed JSON: {ex.Message}");
            }

            if (manifest is null)
                return Fail("Manifest deserialized to null.");

            // Validate name
            if (string.IsNullOrWhiteSpace(manifest.Name))
                return Fail("Field 'name' is required.");
            if (!NameRegex.IsMatch(manifest.Name))
                return Fail($"Field 'name' value '{manifest.Name}' is invalid. Must match ^[a-zA-Z0-9_-]{{2,64}}$.");

            // Validate version
            if (string.IsNullOrWhiteSpace(manifest.Version))
                return Fail("Field 'version' is required.");
            if (!VersionRegex.IsMatch(manifest.Version))
                return Fail($"Field 'version' value '{manifest.Version}' is invalid. Must follow semantic versioning (major.minor.patch).");

            // Validate author
            if (string.IsNullOrWhiteSpace(manifest.Author))
                return Fail("Field 'author' is required.");

            // Validate entry assembly
            if (string.IsNullOrWhiteSpace(manifest.EntryAssembly))
                return Fail("Field 'entryAssembly' is required.");
            if (!manifest.EntryAssembly.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                return Fail($"Field 'entryAssembly' must reference a .dll file. Got: '{manifest.EntryAssembly}'.");

            // Validate dependency names (each must satisfy the same name pattern)
            foreach (var dep in manifest.Dependencies)
            {
                if (string.IsNullOrWhiteSpace(dep) || !NameRegex.IsMatch(dep))
                    return Fail($"Dependency '{dep}' has an invalid name format.");
            }

            // Detect duplicate plugin names
            if (existingNames is not null)
            {
                if (!existingNames.Add(manifest.Name))
                    return Fail($"Duplicate plugin detected: A plugin named '{manifest.Name}' is already registered.");
            }

            return new InputValidationResult { IsValid = true };
        }

        private static InputValidationResult Fail(string reason) =>
            new() { IsValid = false, ErrorMessage = reason };
    }
}
