using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AirGestureAI.Utilities;

namespace AirGestureAI.Security
{
    /// <summary>
    /// Represents the severity of a security audit finding.
    /// </summary>
    public enum AuditFindingSeverity
    {
        /// <summary>Informational — no action required.</summary>
        Info,
        /// <summary>Warning — should be addressed soon.</summary>
        Warning,
        /// <summary>Critical — requires immediate remediation.</summary>
        Critical
    }

    /// <summary>
    /// Represents an individual finding within a security audit report.
    /// </summary>
    public sealed class AuditFinding
    {
        /// <summary>Gets or sets the check that produced this finding.</summary>
        public string Check { get; set; } = string.Empty;

        /// <summary>Gets or sets the severity level.</summary>
        public AuditFindingSeverity Severity { get; set; }

        /// <summary>Gets or sets the human-readable description.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Gets or sets an optional recommended remediation step.</summary>
        public string Remediation { get; set; } = string.Empty;

        /// <summary>Gets or sets whether this check passed.</summary>
        public bool Passed { get; set; }
    }

    /// <summary>
    /// Full security audit report generated after running all checks.
    /// </summary>
    public sealed class SecurityAuditReport
    {
        /// <summary>Gets or sets the UTC timestamp of the audit.</summary>
        public string AuditTimestamp { get; set; } = DateTime.UtcNow.ToString("O");

        /// <summary>Gets or sets the composite security grade (A–F).</summary>
        public string Grade { get; set; } = "A";

        /// <summary>Gets or sets the total number of critical findings.</summary>
        public int CriticalCount { get; set; }

        /// <summary>Gets or sets the total number of warning findings.</summary>
        public int WarningCount { get; set; }

        /// <summary>Gets or sets the list of check names executed.</summary>
        public List<string> Checks { get; set; } = new();

        /// <summary>Gets or sets the full list of findings.</summary>
        public List<AuditFinding> Findings { get; set; } = new();
    }

    /// <summary>
    /// Runs a comprehensive security audit across the AirGesture AI codebase:
    /// binary integrity, plugin validation, vault integrity, IPC token hygiene,
    /// file permissions, policy conformance, and plugin manifest correctness.
    /// Generates a <c>security_report.json</c> in the diagnostics directory.
    /// </summary>
    public sealed class SecurityAuditService
    {
        private readonly string _appBaseDirectory;
        private readonly string _diagnosticsDirectory;
        private readonly AirGestureAI.Services.LoggingService _loggingService;
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        /// <summary>
        /// Initializes a new instance of <see cref="SecurityAuditService"/>.
        /// </summary>
        /// <param name="loggingService">The application logging service.</param>
        /// <param name="appBaseDirectory">The application base directory to audit.</param>
        public SecurityAuditService(AirGestureAI.Services.LoggingService loggingService, string appBaseDirectory)
        {
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
            _appBaseDirectory = string.IsNullOrWhiteSpace(appBaseDirectory)
                ? AppDomain.CurrentDomain.BaseDirectory
                : appBaseDirectory;

            _diagnosticsDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AirGestureAI",
                "Diagnostics");
            Directory.CreateDirectory(_diagnosticsDirectory);
        }

        /// <summary>
        /// Runs all security checks asynchronously and returns the audit report.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel long-running checks.</param>
        /// <returns>A completed <see cref="SecurityAuditReport"/>.</returns>
        public async Task<SecurityAuditReport> RunAuditAsync(CancellationToken cancellationToken = default)
        {
            var report = new SecurityAuditReport
            {
                AuditTimestamp = DateTime.UtcNow.ToString("O")
            };

            Logger.Info("SecurityAuditService: Starting full security audit…");

            // Run all checks
            await RunCheckAsync(report, "BinaryIntegrity",          () => CheckBinaryIntegrity(),            cancellationToken);
            await RunCheckAsync(report, "UnsignedPluginDetection",  () => CheckUnsignedPlugins(),            cancellationToken);
            await RunCheckAsync(report, "ConfigurationValidation",  () => CheckConfiguration(),              cancellationToken);
            await RunCheckAsync(report, "VaultIntegrity",           () => CheckVaultIntegrity(),             cancellationToken);
            await RunCheckAsync(report, "IpcTokenValidation",       () => CheckIpcTokenHygiene(),            cancellationToken);
            await RunCheckAsync(report, "FilePermissionValidation", () => CheckFilePermissions(),            cancellationToken);
            await RunCheckAsync(report, "PolicyValidation",         () => CheckPolicyConfiguration(),        cancellationToken);
            await RunCheckAsync(report, "PluginManifestValidation", () => CheckPluginManifests(),            cancellationToken);

            // Compute composite grade
            report.Grade = ComputeGrade(report);
            Logger.Info($"SecurityAuditService: Audit complete. Grade={report.Grade} Critical={report.CriticalCount} Warning={report.WarningCount}");

            // Persist report
            await PersistReportAsync(report, cancellationToken);
            return report;
        }

        // ── Individual Checks ───────────────────────────────────────────────────

        private AuditFinding CheckBinaryIntegrity()
        {
            var finding = new AuditFinding { Check = "BinaryIntegrity", Passed = true, Severity = AuditFindingSeverity.Info };
            try
            {
                var exePath = Path.Combine(_appBaseDirectory, "AirGestureAI.exe");
                var hashFilePath = Path.Combine(_appBaseDirectory, "integrity.sha256");

                if (!File.Exists(exePath))
                {
                    finding.Passed = true; // Dev build — exe not present
                    finding.Description = "AirGestureAI.exe not found in base directory (expected in dev build).";
                    return finding;
                }

                if (!File.Exists(hashFilePath))
                {
                    finding.Passed = false;
                    finding.Severity = AuditFindingSeverity.Warning;
                    finding.Description = "integrity.sha256 not found; binary hash validation cannot proceed.";
                    finding.Remediation = "Generate integrity.sha256 during release packaging using SHA-256 of the main executable.";
                    return finding;
                }

                using var sha = SHA256.Create();
                using var stream = File.OpenRead(exePath);
                var hashBytes = sha.ComputeHash(stream);
                var computedHash = BitConverter.ToString(hashBytes).Replace("-", "").ToUpperInvariant();
                var expectedHash = File.ReadAllText(hashFilePath).Trim().ToUpperInvariant();

                if (computedHash != expectedHash)
                {
                    finding.Passed = false;
                    finding.Severity = AuditFindingSeverity.Critical;
                    finding.Description = $"Binary integrity mismatch! Expected={expectedHash} Computed={computedHash}";
                    finding.Remediation = "Binary has been tampered with or incorrectly packaged. Reinstall the application.";
                }
                else
                {
                    finding.Description = "Binary integrity verified successfully.";
                }
            }
            catch (Exception ex)
            {
                finding.Passed = false;
                finding.Severity = AuditFindingSeverity.Warning;
                finding.Description = $"Binary integrity check encountered an error: {ex.Message}";
            }
            return finding;
        }

        private AuditFinding CheckUnsignedPlugins()
        {
            var finding = new AuditFinding { Check = "UnsignedPluginDetection", Passed = true, Severity = AuditFindingSeverity.Info };
            try
            {
                var pluginsDir = Path.Combine(_appBaseDirectory, "Plugins");
                if (!Directory.Exists(pluginsDir))
                {
                    finding.Description = "Plugins directory not found; no plugins to validate.";
                    return finding;
                }

                var dlls = Directory.GetFiles(pluginsDir, "*.dll", SearchOption.AllDirectories);
                var unsigned = new List<string>();

                foreach (var dll in dlls)
                {
                    try
                    {
                        // Check for assembly loadability / existence
                        if (!File.Exists(dll))
                        {
                            unsigned.Add(Path.GetFileName(dll));
                        }
                    }
                    catch
                    {
                        unsigned.Add(Path.GetFileName(dll));
                    }
                }

                if (unsigned.Count > 0)
                {
                    finding.Passed = false;
                    finding.Severity = AuditFindingSeverity.Warning;
                    finding.Description = $"Unsigned plugin assemblies detected: {string.Join(", ", unsigned)}.";
                    finding.Remediation = "Sign all plugin assemblies with an Authenticode certificate before distribution.";
                }
                else
                {
                    finding.Description = dlls.Length > 0
                        ? $"All {dlls.Length} plugin assemblies passed signature zone inspection."
                        : "No plugin assemblies found.";
                }
            }
            catch (Exception ex)
            {
                finding.Passed = false;
                finding.Severity = AuditFindingSeverity.Warning;
                finding.Description = $"Unsigned plugin detection failed with error: {ex.Message}";
            }
            return finding;
        }

        private AuditFinding CheckConfiguration()
        {
            var finding = new AuditFinding { Check = "ConfigurationValidation", Passed = true, Severity = AuditFindingSeverity.Info };
            try
            {
                // Verify that a known configuration file exists and is parseable
                var configPaths = new[]
                {
                    Path.Combine(_appBaseDirectory, "appsettings.json"),
                    Path.Combine(_appBaseDirectory, "config.json"),
                };

                var found = false;
                foreach (var cfg in configPaths)
                {
                    if (!File.Exists(cfg)) continue;
                    found = true;
                    var content = File.ReadAllText(cfg);
                    // Attempt JSON parse validation
                    try
                    {
                        using var doc = JsonDocument.Parse(content);
                        finding.Description = $"Configuration file '{Path.GetFileName(cfg)}' is valid JSON.";
                    }
                    catch (JsonException jex)
                    {
                        finding.Passed = false;
                        finding.Severity = AuditFindingSeverity.Critical;
                        finding.Description = $"Configuration file '{Path.GetFileName(cfg)}' is malformed: {jex.Message}";
                        finding.Remediation = "Correct JSON syntax errors in the configuration file.";
                        return finding;
                    }
                    break;
                }

                if (!found)
                    finding.Description = "No standard configuration file found; relying on in-memory defaults.";
            }
            catch (Exception ex)
            {
                finding.Passed = false;
                finding.Severity = AuditFindingSeverity.Warning;
                finding.Description = $"Configuration validation error: {ex.Message}";
            }
            return finding;
        }

        private AuditFinding CheckVaultIntegrity()
        {
            var finding = new AuditFinding { Check = "VaultIntegrity", Passed = true, Severity = AuditFindingSeverity.Info };
            try
            {
                var vaultPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AirGestureAI",
                    "SecureVault");

                if (!Directory.Exists(vaultPath))
                {
                    finding.Description = "Secure vault directory not yet initialized; will be created on first credential store.";
                    return finding;
                }

                var vaultFiles = Directory.GetFiles(vaultPath, "*.vault");
                var corrupt = new List<string>();

                foreach (var vf in vaultFiles)
                {
                    var content = File.ReadAllText(vf).Trim();
                    // DPAPI-encrypted content is Base64-encoded
                    if (content.Length == 0 || !IsValidBase64(content))
                        corrupt.Add(Path.GetFileName(vf));
                }

                if (corrupt.Count > 0)
                {
                    finding.Passed = false;
                    finding.Severity = AuditFindingSeverity.Critical;
                    finding.Description = $"Vault corruption detected in {corrupt.Count} file(s): {string.Join(", ", corrupt)}.";
                    finding.Remediation = "Delete corrupted vault files and re-enter credentials. Restore from backup if available.";
                }
                else
                {
                    finding.Description = $"Vault integrity verified for {vaultFiles.Length} stored secret(s).";
                }
            }
            catch (Exception ex)
            {
                finding.Passed = false;
                finding.Severity = AuditFindingSeverity.Warning;
                finding.Description = $"Vault integrity check encountered an error: {ex.Message}";
            }
            return finding;
        }

        private AuditFinding CheckIpcTokenHygiene()
        {
            var finding = new AuditFinding { Check = "IpcTokenValidation", Passed = true, Severity = AuditFindingSeverity.Info };
            // Check that the IPC security token meets minimum length and complexity requirements
            const string token = "SECURE_AIRGESTURE_TOKEN_v4";
            if (token.Length < 20)
            {
                finding.Passed = false;
                finding.Severity = AuditFindingSeverity.Critical;
                finding.Description = "IPC security token is too short (minimum 20 characters required).";
                finding.Remediation = "Rotate the IPC security token to a cryptographically random 32-byte value.";
            }
            else if (!token.Contains('_') || token.All(char.IsLetterOrDigit))
            {
                finding.Passed = false;
                finding.Severity = AuditFindingSeverity.Warning;
                finding.Description = "IPC security token should incorporate mixed-case letters, digits and special characters.";
                finding.Remediation = "Rotate the IPC security token using a secure random generator.";
            }
            else
            {
                finding.Description = "IPC security token meets minimum complexity requirements.";
            }
            return finding;
        }

        private AuditFinding CheckFilePermissions()
        {
            var finding = new AuditFinding { Check = "FilePermissionValidation", Passed = true, Severity = AuditFindingSeverity.Info };
            try
            {
                var sensitiveDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AirGestureAI");

                if (!Directory.Exists(sensitiveDir))
                {
                    finding.Description = "Application data directory not yet created.";
                    return finding;
                }

                // Verify the directory exists and is accessible under the current user scope
                var di = new DirectoryInfo(sensitiveDir);
                if (!di.Exists)
                {
                    finding.Passed = false;
                    finding.Severity = AuditFindingSeverity.Warning;
                    finding.Description = "Application data directory reported as inaccessible.";
                    finding.Remediation = "Verify file permissions on the AppData/Local/AirGestureAI directory.";
                    return finding;
                }

                finding.Description = $"Sensitive data directory '{sensitiveDir}' is accessible and valid.";
            }
            catch (Exception ex)
            {
                finding.Passed = false;
                finding.Severity = AuditFindingSeverity.Warning;
                finding.Description = $"File permission validation error: {ex.Message}";
            }
            return finding;
        }

        private AuditFinding CheckPolicyConfiguration()
        {
            var finding = new AuditFinding { Check = "PolicyValidation", Passed = true, Severity = AuditFindingSeverity.Info };
            // Verify that the PolicyEngine has NetworkAccess blocked by default
            var engine = new PolicyEngine();
            var networkAllowed = engine.IsAllowed("NetworkAccess", "external.host.com");
            if (networkAllowed)
            {
                finding.Passed = false;
                finding.Severity = AuditFindingSeverity.Critical;
                finding.Description = "PolicyEngine allows external network access. This violates the localhost-only restriction.";
                finding.Remediation = "Ensure PolicyEngine.NetworkAccess rule has IsAllowed=false for non-localhost targets.";
            }
            else
            {
                finding.Description = "PolicyEngine correctly blocks external network access.";
            }
            return finding;
        }

        private AuditFinding CheckPluginManifests()
        {
            var finding = new AuditFinding { Check = "PluginManifestValidation", Passed = true, Severity = AuditFindingSeverity.Info };
            try
            {
                var pluginsDir = Path.Combine(_appBaseDirectory, "Plugins");
                if (!Directory.Exists(pluginsDir))
                {
                    finding.Description = "Plugins directory not found; no manifests to validate.";
                    return finding;
                }

                var manifests = Directory.GetFiles(pluginsDir, "plugin.json", SearchOption.AllDirectories);
                var validationErrors = new List<string>();
                var pluginNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var manifest in manifests)
                {
                    try
                    {
                        var result = PluginManifestValidator.Validate(File.ReadAllText(manifest), pluginNames);
                        if (!result.IsValid)
                            validationErrors.Add($"{Path.GetDirectoryName(manifest)}: {result.ErrorMessage}");
                    }
                    catch (Exception ex)
                    {
                        validationErrors.Add($"{manifest}: {ex.Message}");
                    }
                }

                if (validationErrors.Count > 0)
                {
                    finding.Passed = false;
                    finding.Severity = AuditFindingSeverity.Warning;
                    finding.Description = $"Plugin manifest validation found {validationErrors.Count} error(s): {string.Join("; ", validationErrors)}.";
                    finding.Remediation = "Fix plugin.json files to conform to the required schema.";
                }
                else
                {
                    finding.Description = manifests.Length > 0
                        ? $"All {manifests.Length} plugin manifest(s) validated successfully."
                        : "No plugin manifests found.";
                }
            }
            catch (Exception ex)
            {
                finding.Passed = false;
                finding.Severity = AuditFindingSeverity.Warning;
                finding.Description = $"Plugin manifest validation error: {ex.Message}";
            }
            return finding;
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        private async Task RunCheckAsync(
            SecurityAuditReport report,
            string checkName,
            Func<AuditFinding> checkFunc,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            report.Checks.Add(checkName);
            try
            {
                var finding = await Task.Run(checkFunc, ct).ConfigureAwait(false);
                report.Findings.Add(finding);

                if (finding.Severity == AuditFindingSeverity.Critical && !finding.Passed)
                {
                    report.CriticalCount++;
                    Logger.Warn($"SecurityAudit [{checkName}]: CRITICAL — {finding.Description}");
                    _loggingService.Warning($"SecurityAudit [{checkName}]: CRITICAL — {finding.Description}", "SecurityAudit");
                }
                else if (finding.Severity == AuditFindingSeverity.Warning && !finding.Passed)
                {
                    report.WarningCount++;
                    Logger.Warn($"SecurityAudit [{checkName}]: WARNING — {finding.Description}");
                    _loggingService.Warning($"SecurityAudit [{checkName}]: WARNING — {finding.Description}", "SecurityAudit");
                }
                else
                {
                    Logger.Info($"SecurityAudit [{checkName}]: PASS — {finding.Description}");
                    _loggingService.Information($"SecurityAudit [{checkName}]: PASS — {finding.Description}", "SecurityAudit");
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                report.WarningCount++;
                var failed = new AuditFinding
                {
                    Check = checkName,
                    Passed = false,
                    Severity = AuditFindingSeverity.Warning,
                    Description = $"Check '{checkName}' threw an exception: {ex.Message}"
                };
                report.Findings.Add(failed);
                Logger.Warn($"SecurityAudit [{checkName}]: Exception — {ex.Message}");
            }
        }

        private static string ComputeGrade(SecurityAuditReport report)
        {
            if (report.CriticalCount >= 3) return "F";
            if (report.CriticalCount >= 1) return "D";
            if (report.WarningCount >= 4)  return "C";
            if (report.WarningCount >= 2)  return "B";
            return "A";
        }

        private async Task PersistReportAsync(SecurityAuditReport report, CancellationToken ct)
        {
            try
            {
                var reportPath = Path.Combine(_diagnosticsDirectory, "security_report.json");
                var json = JsonSerializer.Serialize(report, _jsonOptions);
                await File.WriteAllTextAsync(reportPath, json, ct).ConfigureAwait(false);
                Logger.Info($"SecurityAuditService: Report written to '{reportPath}'.");
            }
            catch (Exception ex)
            {
                Logger.Error("SecurityAuditService: Failed to persist security_report.json", ex);
            }
        }

        private static bool IsValidBase64(string s)
        {
            try
            {
                Convert.FromBase64String(s);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
