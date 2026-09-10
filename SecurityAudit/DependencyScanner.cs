using System;
using System.Collections.Generic;
using System.Reflection;
using AirGestureAI.Utilities;

namespace AirGestureAI.SecurityAudit
{
    /// <summary>
    /// Scans loaded assemblies for known-vulnerable NuGet package versions
    /// using a locally bundled advisory list (offline-first).
    /// </summary>
    public sealed class DependencyScanner
    {
        // Simplified advisory list; replace with a full offline NVD-style feed for production
        private static readonly Dictionary<string, string> KnownVulnerableVersions =
            new(StringComparer.OrdinalIgnoreCase)
            {
                // format: "AssemblyName" → "max vulnerable version"
                // { "Newtonsoft.Json", "12.0.3" },
            };

        /// <summary>
        /// Scans all referenced assemblies of <paramref name="assembly"/> for known vulnerabilities.
        /// </summary>
        public IReadOnlyList<SecurityFinding> Scan(Assembly assembly)
        {
            Logger.Info("DependencyScanner: Scanning referenced assemblies…");
            var findings = new List<SecurityFinding>();

            foreach (var reference in assembly.GetReferencedAssemblies())
            {
                if (KnownVulnerableVersions.TryGetValue(reference.Name ?? string.Empty, out var maxVulnVersion))
                {
                    if (Version.TryParse(maxVulnVersion, out var vuln) &&
                        reference.Version is not null &&
                        reference.Version <= vuln)
                    {
                        findings.Add(new SecurityFinding
                        {
                            Category       = "Dependency",
                            Severity       = SecuritySeverity.Critical,
                            Description    = $"Dependency '{reference.Name}' v{reference.Version} is known to be vulnerable (≤ v{maxVulnVersion}).",
                            Recommendation = $"Update '{reference.Name}' to a patched version.",
                        });
                    }
                }
            }

            Logger.Info($"DependencyScanner: {findings.Count} vulnerability finding(s).");
            return findings;
        }
    }
}
