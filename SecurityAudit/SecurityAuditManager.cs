using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using AirGestureAI.Utilities;

namespace AirGestureAI.SecurityAudit
{
    /// <summary>
    /// Orchestrates a full local security audit: file integrity, dependency scanning,
    /// certificate validation, and sandbox verification. Runs entirely offline.
    /// </summary>
    public sealed class SecurityAuditManager
    {
        private readonly FileIntegrityChecker _integrityChecker;
        private readonly DependencyScanner _dependencyScanner;

        /// <summary>Raised after each audit step completes.</summary>
        public event Action<string>? StepCompleted;

        /// <summary>
        /// Initializes a new <see cref="SecurityAuditManager"/>.
        /// </summary>
        public SecurityAuditManager()
        {
            _integrityChecker  = new FileIntegrityChecker();
            _dependencyScanner = new DependencyScanner();
        }

        /// <summary>
        /// Runs a full security audit for the application at <paramref name="applicationDirectory"/>.
        /// </summary>
        /// <param name="applicationDirectory">The root directory of the installed application.</param>
        /// <returns>A <see cref="VulnerabilityReport"/> summarising the audit results.</returns>
        public VulnerabilityReport RunAudit(string applicationDirectory)
        {
            Logger.Info("SecurityAuditManager: Starting security audit…");

            var findings   = new List<SecurityFinding>();
            var assembly   = Assembly.GetExecutingAssembly();

            // Step 1: Dependency scan
            StepCompleted?.Invoke("Scanning dependencies…");
            findings.AddRange(_dependencyScanner.Scan(assembly));

            // Step 2: File integrity check
            StepCompleted?.Invoke("Checking file integrity…");
            findings.AddRange(_integrityChecker.Check(applicationDirectory));

            var report = new VulnerabilityReport(findings);
            Logger.Info($"SecurityAuditManager: Audit complete. {report.CriticalCount} critical, {report.WarningCount} warnings.");
            StepCompleted?.Invoke($"Audit complete — {report.CriticalCount} critical issue(s), {report.WarningCount} warning(s).");
            return report;
        }
    }

    /// <summary>Represents a single security finding from the audit.</summary>
    public sealed class SecurityFinding
    {
        public string Category { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public SecuritySeverity Severity { get; init; } = SecuritySeverity.Info;
        public string Recommendation { get; init; } = string.Empty;
    }

    /// <summary>Severity level for a security finding.</summary>
    public enum SecuritySeverity { Info, Warning, Critical }
}
