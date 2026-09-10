using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using AirGestureAI.Utilities;

namespace AirGestureAI.Release
{
    /// <summary>
    /// Manages the Release Candidate build process for AirGesture AI.
    /// Produces Portable ZIP, MSI configuration, and release artifacts.
    /// </summary>
    public sealed class ReleaseManager
    {
        private const string AppName    = "AirGestureAI";
        private const string AppVersion = "4.1.0";

        /// <summary>Raised after each build step completes.</summary>
        public event Action<string>? StepCompleted;

        // ── Portable ZIP ──────────────────────────────────────────────────────

        /// <summary>
        /// Builds a portable ZIP archive of the application directory.
        /// </summary>
        /// <param name="sourceDirectory">Root directory of the application binaries.</param>
        /// <param name="outputDirectory">Directory where the ZIP is written.</param>
        /// <returns>Absolute path to the generated ZIP file.</returns>
        public string BuildPortableZip(string sourceDirectory, string outputDirectory)
        {
            Logger.Info("ReleaseManager: Building Portable ZIP…");
            StepCompleted?.Invoke("Building Portable ZIP…");

            Directory.CreateDirectory(outputDirectory);
            var zipPath = Path.Combine(outputDirectory, $"{AppName}-{AppVersion}-Portable.zip");

            if (File.Exists(zipPath)) File.Delete(zipPath);
            ZipFile.CreateFromDirectory(sourceDirectory, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);

            // Write SHA-256 checksum
            var checksum = ComputeSha256(zipPath);
            File.WriteAllText(zipPath + ".sha256", $"{checksum}  {Path.GetFileName(zipPath)}\n");

            Logger.Info($"ReleaseManager: Portable ZIP → '{zipPath}' (SHA-256: {checksum})");
            StepCompleted?.Invoke($"✓ Portable ZIP: {Path.GetFileName(zipPath)}");
            return zipPath;
        }

        // ── Release Notes ─────────────────────────────────────────────────────

        /// <summary>
        /// Generates a Markdown release notes file for Version 1.0.0-RC1.
        /// </summary>
        /// <param name="outputPath">Absolute path where the file is written.</param>
        public void GenerateReleaseNotes(string outputPath)
        {
            Logger.Info("ReleaseManager: Generating release notes…");
            var content = $"""
            # AirGesture AI {AppVersion} Release Notes

            **Release Date**: {DateTime.UtcNow:yyyy-MM-dd} UTC

            ## What's New

            - Phase 1–11: Core tracking, cursor engine, gesture recognition, hover selection, plugin framework
            - Phase 12: Calibration & Personalization Engine
            - Phase 13: Custom Gesture Studio & Machine Learning Engine
            - Phase 14: AI Intent Engine & Smart Context Automation
            - Phase 15: Testing Framework & Benchmark Suite
            - Phase 17: Voice Assistant & Multimodal Interaction Engine
            - Phase 18: Natural Language Commands & Macro Engine
            - Phase 19: Computer Vision UI Understanding
            - Phase 20: Cloud Sync & Cross-Device Ecosystem
            - Phase 21: AI Agent Ecosystem & Skill Marketplace
            - Phase 22: Enterprise Deployment & Administration Console
            - Phase 23: Digital Twin & Simulation Environment
            - Phase 24: Public SDK, Extensions, Documentation Portal
            - Phase 25: Performance Optimization, Security Hardening, Accessibility & Release Candidate

            ## Known Issues

            None for RC1.

            ## Upgrade Instructions

            Run the installer or extract the Portable ZIP.
            Run the Migration Engine from Settings if upgrading from an earlier build.
            """;
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(outputPath, content);
            Logger.Info($"ReleaseManager: Release notes → '{outputPath}'");
            StepCompleted?.Invoke($"✓ Release Notes: {Path.GetFileName(outputPath)}");
        }

        // ── Release Readiness Score ───────────────────────────────────────────

        /// <summary>
        /// Computes an overall release readiness score (0–100) and a label.
        /// </summary>
        public (int Score, string Label) ComputeReadinessScore(
            bool securityClean, bool accessibilityReady, bool performanceGreen)
        {
            var score = 70; // base
            if (securityClean)      score += 10;
            if (accessibilityReady) score += 10;
            if (performanceGreen)   score += 10;

            var label = score >= 100 ? "Production Ready"
                : score >= 80  ? "Release Candidate"
                : score >= 60  ? "Beta"
                : "Alpha";

            return (score, label);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static string ComputeSha256(string filePath)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            using var fs  = File.OpenRead(filePath);
            return BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLowerInvariant();
        }
    }
}
