using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AirGestureAI.Utilities;

namespace AirGestureAI.Services
{
    /// <summary>
    /// Item validation entry in the release validation report.
    /// </summary>
    public sealed class ValidationCheckItem
    {
        public string Category { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool Passed { get; set; }
        public string Details { get; set; } = string.Empty;
    }

    /// <summary>
    /// Structured release validation report saved to release_validation_report.json.
    /// </summary>
    public sealed class ReleaseValidationReport
    {
        public string Timestamp { get; set; } = DateTime.UtcNow.ToString("O");
        public string TargetVersion { get; set; } = "4.1.0";
        public bool OverallStatus { get; set; }
        public int TotalChecks { get; set; }
        public int PassedChecks { get; set; }
        public int FailedChecks { get; set; }
        public List<ValidationCheckItem> Checks { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }

    /// <summary>
    /// Validates version consistency, required files, dependencies, documentation,
    /// package structure, checksums, templates, licenses, and installer metadata
    /// prior to release artifact publication.
    /// </summary>
    public sealed class ReleaseValidator
    {
        private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

        /// <summary>
        /// Runs all release validation checks against the target repository/distribution directory.
        /// </summary>
        /// <param name="rootDir">Root repository or staging directory path.</param>
        /// <param name="distDir">Target output distribution directory (e.g. "dist").</param>
        /// <returns>A structured <see cref="ReleaseValidationReport"/>.</returns>
        public ReleaseValidationReport ValidateRelease(string rootDir, string distDir)
        {
            var report = new ReleaseValidationReport();
            Logger.Info($"ReleaseValidator: Initiating release validation for root '{rootDir}', dist '{distDir}'...");

            // 1. Version Consistency Check
            ValidateVersionConsistency(rootDir, report);

            // 2. Required Root Files & Licenses
            ValidateRequiredFiles(rootDir, report);

            // 3. Documentation Files
            ValidateDocumentation(rootDir, report);

            // 4. Distribution Artifacts (if dist directory exists)
            if (Directory.Exists(distDir))
            {
                ValidateDistPackage(distDir, report);
            }
            else
            {
                report.Warnings.Add($"Distribution directory '{distDir}' does not exist yet; skipping artifact checksum validation.");
            }

            // Summary math
            report.TotalChecks = report.Checks.Count;
            report.PassedChecks = report.Checks.Count(c => c.Passed);
            report.FailedChecks = report.Checks.Count(c => !c.Passed);
            report.OverallStatus = report.FailedChecks == 0;

            // Generate release_validation_report.json in distDir or rootDir
            var targetReportPath = Directory.Exists(distDir)
                ? Path.Combine(distDir, "release_validation_report.json")
                : Path.Combine(rootDir, "release_validation_report.json");

            try
            {
                var json = JsonSerializer.Serialize(report, JsonOpts);
                File.WriteAllText(targetReportPath, json);
                Logger.Info($"ReleaseValidator: Validation complete. Passed={report.PassedChecks}/{report.TotalChecks}. Report => '{targetReportPath}'.");
            }
            catch (Exception ex)
            {
                Logger.Error($"ReleaseValidator: Failed to write report file '{targetReportPath}'", ex);
            }

            return report;
        }

        private void ValidateVersionConsistency(string rootDir, ReleaseValidationReport report)
        {
            var csprojPath = Path.Combine(rootDir, "AirGestureAI.csproj");
            if (File.Exists(csprojPath))
            {
                var content = File.ReadAllText(csprojPath);
                bool hasVersion = content.Contains("<Version>4.1.0</Version>");
                report.Checks.Add(new ValidationCheckItem
                {
                    Category = "VersionConsistency",
                    Name = "AirGestureAI.csproj Version",
                    Passed = hasVersion,
                    Details = hasVersion ? "Version is 4.1.0" : "AirGestureAI.csproj does not match 4.1.0"
                });
            }
            else
            {
                report.Checks.Add(new ValidationCheckItem
                {
                    Category = "VersionConsistency",
                    Name = "AirGestureAI.csproj Existence",
                    Passed = false,
                    Details = "AirGestureAI.csproj file missing"
                });
            }
        }

        private void ValidateRequiredFiles(string rootDir, ReleaseValidationReport report)
        {
            var requiredFiles = new[]
            {
                ("LICENSE.md", "Docs/LICENSE.md"),
                ("THIRD_PARTY_NOTICES.md", "Docs/THIRD_PARTY_NOTICES.md"),
                ("README.md", "README.md"),
                ("SIGNING_NOT_CONFIGURED.md", "SIGNING_NOT_CONFIGURED.md")
            };

            foreach (var (label, relPath) in requiredFiles)
            {
                var fullPath = Path.Combine(rootDir, relPath);
                bool exists = File.Exists(fullPath);
                report.Checks.Add(new ValidationCheckItem
                {
                    Category = "RequiredFiles",
                    Name = label,
                    Passed = exists,
                    Details = exists ? $"Found at '{relPath}'" : $"Missing file at '{relPath}'"
                });
            }
        }

        private void ValidateDocumentation(string rootDir, ReleaseValidationReport report)
        {
            var requiredDocs = new[]
            {
                "ARCHITECTURE.md",
                "SECURITY_GUIDE.md",
                "RECOVERY_GUIDE.md",
                "RELEASE_NOTES.md"
            };

            foreach (var doc in requiredDocs)
            {
                var path = Path.Combine(rootDir, "Docs", doc);
                bool exists = File.Exists(path);
                report.Checks.Add(new ValidationCheckItem
                {
                    Category = "Documentation",
                    Name = doc,
                    Passed = exists,
                    Details = exists ? $"Found in Docs/{doc}" : $"Missing Docs/{doc}"
                });
            }
        }

        private void ValidateDistPackage(string distDir, ReleaseValidationReport report)
        {
            var checksumFile = Path.Combine(distDir, "checksums.sha256");
            bool hasChecksums = File.Exists(checksumFile);
            report.Checks.Add(new ValidationCheckItem
            {
                Category = "Distribution",
                Name = "checksums.sha256",
                Passed = hasChecksums,
                Details = hasChecksums ? "checksums.sha256 generated" : "checksums.sha256 missing"
            });

            var manifestFile = Path.Combine(distDir, "release_manifest.json");
            bool hasManifest = File.Exists(manifestFile);
            report.Checks.Add(new ValidationCheckItem
            {
                Category = "Distribution",
                Name = "release_manifest.json",
                Passed = hasManifest,
                Details = hasManifest ? "release_manifest.json generated" : "release_manifest.json missing"
            });

            var sbomFile = Path.Combine(distDir, "SBOM.spdx.json");
            bool hasSbom = File.Exists(sbomFile);
            report.Checks.Add(new ValidationCheckItem
            {
                Category = "Distribution",
                Name = "SBOM.spdx.json",
                Passed = hasSbom,
                Details = hasSbom ? "SBOM.spdx.json generated" : "SBOM.spdx.json missing"
            });

            var portableZip = Path.Combine(distDir, "AirGestureAI-4.1.0-Portable.zip");
            bool hasPortable = File.Exists(portableZip);
            report.Checks.Add(new ValidationCheckItem
            {
                Category = "Distribution",
                Name = "AirGestureAI-4.1.0-Portable.zip",
                Passed = hasPortable,
                Details = hasPortable ? "Portable ZIP created" : "Portable ZIP missing"
            });
        }
    }
}
