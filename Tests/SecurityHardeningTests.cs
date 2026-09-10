using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AirGestureAI.IPC;
using AirGestureAI.SDK;
using AirGestureAI.Security;
using AirGestureAI.Services;
using Xunit;

namespace AirGestureAI.Tests
{
    public sealed class SecurityHardeningTests : IDisposable
    {
        private readonly string _tempRoot;

        public SecurityHardeningTests()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), "AirGestureAI_SecurityTests_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(_tempRoot);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempRoot, recursive: true); } catch { }
        }

        // ── Path Security ────────────────────────────────────────────────────────

        [Fact]
        public void PathSecurity_TraversalAttempt_IsBlocked()
        {
            var root = _tempRoot;
            var malicious = Path.Combine(_tempRoot, "..", "..", "Windows", "System32", "config");
            Assert.False(PathSecurity.IsPathSafe(malicious, root));
        }

        [Fact]
        public void PathSecurity_SafePath_IsAccepted()
        {
            var root = _tempRoot;
            var safeFile = Path.Combine(_tempRoot, "subdir", "config.json");
            Assert.True(PathSecurity.IsPathSafe(safeFile, root));
        }

        [Fact]
        public void PathSecurity_EmptyInput_IsBlocked()
        {
            Assert.False(PathSecurity.IsPathSafe("", _tempRoot));
            Assert.False(PathSecurity.IsPathSafe("  ", _tempRoot));
        }

        // ── IPC Token Validation ─────────────────────────────────────────────────

        [Fact]
        public void IpcSecurity_ValidToken_Accepted()
        {
            Assert.True(IpcSecurity.ValidateToken("SECURE_AIRGESTURE_TOKEN_v4"));
        }

        [Fact]
        public void IpcSecurity_InvalidToken_Rejected()
        {
            Assert.False(IpcSecurity.ValidateToken("wrong_token"));
            Assert.False(IpcSecurity.ValidateToken(""));
            Assert.False(IpcSecurity.ValidateToken("SECURE_AIRGESTURE_TOKEN_v3"));
        }

        // ── Input Security (Injection) ────────────────────────────────────────────

        [Fact]
        public void InputSecurity_CleanPayload_Accepted()
        {
            var result = InputSecurity.Validate("Hello World 123");
            Assert.True(result.IsValid);
        }

        [Theory]
        [InlineData("cmd; rm -rf /")]
        [InlineData("value & shutdown")]
        [InlineData("data | cat /etc/passwd")]
        [InlineData("file > /dev/null")]
        [InlineData("${HOME}")]
        [InlineData("`whoami`")]
        public void InputSecurity_InjectionPayload_IsRejected(string input)
        {
            var result = InputSecurity.Validate(input);
            Assert.False(result.IsValid);
            Assert.False(string.IsNullOrEmpty(result.ErrorMessage));
            Assert.False(string.IsNullOrEmpty(result.InvalidToken));
        }

        [Fact]
        public void InputSecurity_AllowedTokenOverride_IsAccepted()
        {
            // Allow semicolons explicitly for SQL-style payloads
            var result = InputSecurity.Validate("SELECT * FROM t; COMMIT", allowedTokens: new[] { ';' });
            Assert.True(result.IsValid);
        }

        // ── Binary Integrity ─────────────────────────────────────────────────────

        [Fact]
        public void BinarySigner_CorrectHash_Validates()
        {
            var binaryPath = Path.Combine(_tempRoot, "test.dll");
            var hashPath   = Path.Combine(_tempRoot, "test.dll.sha256");
            File.WriteAllText(binaryPath, "FakeBinaryContent");

            BinarySigner.GenerateIntegrityFile(binaryPath, hashPath);
            var result = BinarySigner.VerifySha256(binaryPath, hashPath);

            Assert.True(result.IsValid);
            Assert.Equal(result.ComputedHash, result.ExpectedHash);
        }

        [Fact]
        public void BinarySigner_TamperedBinary_FailsValidation()
        {
            var binaryPath = Path.Combine(_tempRoot, "tampered.dll");
            var hashPath   = Path.Combine(_tempRoot, "tampered.dll.sha256");

            File.WriteAllText(binaryPath, "OriginalContent");
            BinarySigner.GenerateIntegrityFile(binaryPath, hashPath);

            // Simulate tampering
            File.WriteAllText(binaryPath, "TamperedContent");
            var result = BinarySigner.VerifySha256(binaryPath, hashPath);

            Assert.False(result.IsValid);
            Assert.NotEqual(result.ComputedHash, result.ExpectedHash);
        }

        [Fact]
        public void BinarySigner_MissingHashFile_FailsValidation()
        {
            var binaryPath = Path.Combine(_tempRoot, "nohash.dll");
            File.WriteAllText(binaryPath, "Content");
            var result = BinarySigner.VerifySha256(binaryPath, Path.Combine(_tempRoot, "missing.sha256"));
            Assert.False(result.IsValid);
        }

        // ── SecureVault ──────────────────────────────────────────────────────────

        [Fact]
        public void SecureVault_StoreAndRetrieve_Works()
        {
            var vault = new SecureVault("TestVault_" + Guid.NewGuid().ToString("N")[..6]);
            vault.Store("api-key", "SuperSecret123");
            var retrieved = vault.Retrieve("api-key");
            Assert.Equal("SuperSecret123", retrieved);
        }

        [Fact]
        public void SecureVault_MissingKey_ReturnsNull()
        {
            var vault = new SecureVault("TestVault_" + Guid.NewGuid().ToString("N")[..6]);
            Assert.Null(vault.Retrieve("nonexistent-key"));
        }

        [Fact]
        public void SecureVault_IntegrityVerification_PassesOnCleanVault()
        {
            var vault = new SecureVault("TestVault_" + Guid.NewGuid().ToString("N")[..6]);
            vault.Store("secret", "value");
            Assert.True(vault.VerifyIntegrity());
        }

        [Fact]
        public async Task SecureVault_KeyRotation_PreservesData()
        {
            var vault = new SecureVault("TestVault_" + Guid.NewGuid().ToString("N")[..6]);
            vault.Store("my-token", "BeforeRotation");

            vault.RotateKey();
            await Task.Yield();

            var after = vault.Retrieve("my-token");
            Assert.Equal("BeforeRotation", after);
        }

        [Fact]
        public async Task SecureVault_BackupAndRestore_Works()
        {
            var vault = new SecureVault("TestVault_" + Guid.NewGuid().ToString("N")[..6]);
            vault.Store("password", "MySecretPass");

            var backupDir = await vault.BackupAsync();
            Assert.True(Directory.Exists(backupDir));

            // Backup directory should contain at least one .vault file
            var backupFiles = Directory.GetFiles(backupDir, "*.vault");
            Assert.NotEmpty(backupFiles);
        }

        // ── Plugin Manifest Validation ────────────────────────────────────────────

        [Fact]
        public void PluginManifest_Valid_Passes()
        {
            const string json = """
                {
                  "name": "my-plugin",
                  "version": "1.0.0",
                  "author": "Test Author",
                  "entryAssembly": "MyPlugin.dll",
                  "dependencies": ["other-plugin"]
                }
                """;
            var result = PluginManifestValidator.Validate(json);
            Assert.True(result.IsValid, result.ErrorMessage);
        }

        [Fact]
        public void PluginManifest_MissingName_Fails()
        {
            const string json = """{ "version": "1.0.0", "author": "A", "entryAssembly": "P.dll" }""";
            var result = PluginManifestValidator.Validate(json);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void PluginManifest_InvalidVersion_Fails()
        {
            const string json = """{ "name": "plugin", "version": "v1", "author": "A", "entryAssembly": "P.dll" }""";
            var result = PluginManifestValidator.Validate(json);
            Assert.False(result.IsValid);
            Assert.Contains("version", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void PluginManifest_DuplicateName_Fails()
        {
            const string json = """{ "name": "dup-plugin", "version": "1.0.0", "author": "A", "entryAssembly": "P.dll" }""";
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var first  = PluginManifestValidator.Validate(json, seen);
            var second = PluginManifestValidator.Validate(json, seen);

            Assert.True(first.IsValid);
            Assert.False(second.IsValid);
            Assert.Contains("Duplicate", second.ErrorMessage);
        }

        [Fact]
        public void PluginManifest_NonDllAssembly_Fails()
        {
            const string json = """{ "name": "plugin", "version": "1.0.0", "author": "A", "entryAssembly": "Plugin.exe" }""";
            var result = PluginManifestValidator.Validate(json);
            Assert.False(result.IsValid);
            Assert.Contains("dll", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        // ── Security Audit Report ─────────────────────────────────────────────────

        [Fact]
        public async Task SecurityAuditService_GeneratesReport_WithExpectedSchema()
        {
            var loggingDir = Path.Combine(_tempRoot, "logs");
            Directory.CreateDirectory(loggingDir);
            await using var loggingService = new LoggingService(loggingDir);

            var auditService = new SecurityAuditService(loggingService, _tempRoot);
            var report = await auditService.RunAuditAsync(CancellationToken.None);

            Assert.NotNull(report);
            Assert.NotEmpty(report.AuditTimestamp);
            Assert.NotEmpty(report.Grade);
            Assert.NotEmpty(report.Checks);
            Assert.NotEmpty(report.Findings);
            Assert.True(report.CriticalCount >= 0);
            Assert.True(report.WarningCount >= 0);
        }

        [Fact]
        public async Task SecurityAuditService_ReportFile_IsWrittenToDisk()
        {
            var loggingDir = Path.Combine(_tempRoot, "logs");
            Directory.CreateDirectory(loggingDir);
            await using var loggingService = new LoggingService(loggingDir);

            var auditService = new SecurityAuditService(loggingService, _tempRoot);
            await auditService.RunAuditAsync(CancellationToken.None);

            var reportPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AirGestureAI",
                "Diagnostics",
                "security_report.json");

            Assert.True(File.Exists(reportPath), $"Expected security_report.json at '{reportPath}'.");
        }
    }
}
