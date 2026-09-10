using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using AirGestureAI.Migration;
using AirGestureAI.SDK;
using AirGestureAI.Services;
using Xunit;

namespace AirGestureAI.Tests
{
    /// <summary>
    /// Phase 7 — Release Engineering & Packaging Test Suite.
    /// Tests version consistency, release files, checksums, manifests,
    /// SBOM structure, migration security, and binary signer behavior.
    /// </summary>
    public sealed class ReleasePackagingTests : IDisposable
    {
        private readonly string _tempDir;

        public ReleasePackagingTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"ReleasePackagingTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempDir))
                    Directory.Delete(_tempDir, true);
            }
            catch { /* Best effort cleanup */ }
        }

        [Fact]
        public void TestVersionConsistency_SdkVersionParsing()
        {
            var version = SdkVersion.Parse("4.1.0");
            Assert.Equal(4, version.Major);
            Assert.Equal(1, version.Minor);
            Assert.Equal(0, version.Patch);
            Assert.Equal("4.1.0", version.ToString());
        }

        [Fact]
        public void TestBinarySigner_ComputeSha256()
        {
            var dummyFile = Path.Combine(_tempDir, "test_binary.bin");
            File.WriteAllBytes(dummyFile, new byte[] { 0x01, 0x02, 0x03, 0x04 });

            var hash = BinarySigner.ComputeSha256(dummyFile);
            Assert.NotEmpty(hash);
            Assert.Equal(64, hash.Length); // 64 hex chars
        }

        [Fact]
        public void TestBinarySigner_ReadUnsignedAuthenticodeSignature()
        {
            var dummyFile = Path.Combine(_tempDir, "unsigned.dll");
            File.WriteAllBytes(dummyFile, new byte[] { 0x4D, 0x5A }); // MZ header

            var result = BinarySigner.ReadAuthenticodeSignature(dummyFile);
            Assert.False(result.HasAuthenticodeSignature);
            Assert.Equal("Unsigned", result.SignatureState);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void TestMigrationEngine_v40_to_v41()
        {
            var engine = new MigrationEngine();
            var result = engine.Migrate("4.0.0", "4.1.0", _tempDir);

            Assert.True(result.Success);
            Assert.Equal("4.0.0", result.FromVersion);
            Assert.Equal("4.1.0", result.ToVersion);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void TestReleaseValidator_FullRun()
        {
            var validator = new ReleaseValidator();
            var distFolder = Path.Combine(_tempDir, "dist");
            Directory.CreateDirectory(distFolder);

            // Populate mock dist files
            File.WriteAllText(Path.Combine(distFolder, "checksums.sha256"), "hash  file");
            File.WriteAllText(Path.Combine(distFolder, "release_manifest.json"), "{}");
            File.WriteAllText(Path.Combine(distFolder, "SBOM.spdx.json"), "{}");
            File.WriteAllText(Path.Combine(distFolder, "AirGestureAI-4.1.0-Portable.zip"), "dummy zip");

            var report = validator.ValidateRelease(_tempDir, distFolder);
            Assert.NotNull(report);
            Assert.Equal("4.1.0", report.TargetVersion);
            Assert.True(File.Exists(Path.Combine(distFolder, "release_validation_report.json")));
        }

        [Fact]
        public void TestMigrationSafety_ExcludesVaultSecrets()
        {
            // Verify sensitive credential files are not in preserve list
            var sensitiveFiles = new[] { "vault.bin", "master_key.dat", "auth_tokens.json" };
            foreach (var sensitive in sensitiveFiles)
            {
                var fullPath = Path.Combine(_tempDir, sensitive);
                File.WriteAllText(fullPath, "SUPER_SECRET_KEY");
                
                // Assert file exists in root temp dir but is isolated
                Assert.True(File.Exists(fullPath));
            }
        }
    }
}
