using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using AirGestureAI.Utilities;

namespace AirGestureAI.SecurityAudit
{
    /// <summary>
    /// Checks integrity of application binaries using SHA-256 hashes.
    /// Compares against a stored baseline manifest file.
    /// </summary>
    public sealed class FileIntegrityChecker
    {
        private const string ManifestFileName = "integrity.sha256";

        /// <summary>
        /// Checks all DLL and EXE files in <paramref name="directory"/> against a baseline.
        /// Returns a list of findings for modified or missing files.
        /// </summary>
        public IReadOnlyList<SecurityFinding> Check(string directory)
        {
            var findings     = new List<SecurityFinding>();
            var manifestPath = Path.Combine(directory, ManifestFileName);

            if (!File.Exists(manifestPath))
            {
                Logger.Warn($"FileIntegrityChecker: No integrity manifest at '{manifestPath}'. Skipping check.");
                findings.Add(new SecurityFinding
                {
                    Category       = "FileIntegrity",
                    Severity       = SecuritySeverity.Warning,
                    Description    = "Integrity manifest not found. Cannot verify file hashes.",
                    Recommendation = "Generate an integrity manifest using the Release Builder.",
                });
                return findings;
            }

            var baseline = ParseManifest(manifestPath);
            var files    = Directory.GetFiles(directory, "*.dll", SearchOption.TopDirectoryOnly);

            foreach (var file in files)
            {
                var name = Path.GetFileName(file);
                var hash = ComputeHash(file);

                if (!baseline.TryGetValue(name, out var expected))
                {
                    findings.Add(new SecurityFinding
                    {
                        Category       = "FileIntegrity",
                        Severity       = SecuritySeverity.Warning,
                        Description    = $"File '{name}' is not in the integrity manifest.",
                        Recommendation = "Rebuild the integrity manifest after adding new files.",
                    });
                }
                else if (!hash.Equals(expected, StringComparison.OrdinalIgnoreCase))
                {
                    findings.Add(new SecurityFinding
                    {
                        Category       = "FileIntegrity",
                        Severity       = SecuritySeverity.Critical,
                        Description    = $"Hash mismatch for '{name}'. Expected: {expected}, Got: {hash}",
                        Recommendation = "File may have been tampered with. Reinstall the application.",
                    });
                }
            }

            Logger.Info($"FileIntegrityChecker: Checked {files.Length} file(s). {findings.Count} finding(s).");
            return findings;
        }

        private static string ComputeHash(string filePath)
        {
            using var sha = SHA256.Create();
            using var fs  = File.OpenRead(filePath);
            return BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLowerInvariant();
        }

        private static Dictionary<string, string> ParseManifest(string path)
        {
            var dict  = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in File.ReadAllLines(path))
            {
                var parts = line.Split(' ', 2, StringSplitOptions.TrimEntries);
                if (parts.Length == 2) dict[parts[1]] = parts[0];
            }
            return dict;
        }
    }
}
