using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using AirGestureAI.Plugins;
using AirGestureAI.Services;
using Xunit;

namespace AirGestureAI.Tests
{
    public sealed class DiagnosticsTests : IDisposable
    {
        private readonly string _tempRoot;

        public DiagnosticsTests()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), "AirGestureAI_DiagnosticsTests_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(_tempRoot);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempRoot, recursive: true); } catch { }
        }

        // ── Contrast Ratio Math Helper ──────────────────────────────────────────

        private static double GetRelativeLuminance(Color c)
        {
            double r = c.R / 255.0;
            double g = c.G / 255.0;
            double b = c.B / 255.0;

            r = r <= 0.03928 ? r / 12.92 : Math.Pow((r + 0.055) / 1.055, 2.4);
            g = g <= 0.03928 ? g / 12.92 : Math.Pow((g + 0.055) / 1.055, 2.4);
            b = b <= 0.03928 ? b / 12.92 : Math.Pow((b + 0.055) / 1.055, 2.4);

            return 0.2126 * r + 0.7152 * g + 0.0722 * b;
        }

        private static double GetContrastRatio(Color c1, Color c2)
        {
            double l1 = GetRelativeLuminance(c1);
            double l2 = GetRelativeLuminance(c2);

            double lighter = Math.Max(l1, l2);
            double darker  = Math.Min(l1, l2);

            return (lighter + 0.05) / (darker + 0.05);
        }

        [Fact]
        public void VerifyContrastCalculations_WhiteOnBlack_HasPerfectContrast()
        {
            var white = Colors.White;
            var black = Colors.Black;

            double ratio = GetContrastRatio(white, black);
            // White-on-Black ratio must be exactly 21:1
            Assert.True(Math.Abs(ratio - 21.0) < 0.1);
        }

        [Fact]
        public void VerifyContrastCalculations_GrayOnWhite_HasPoorContrast()
        {
            var gray = Color.FromRgb(200, 200, 200);
            var white = Colors.White;

            double ratio = GetContrastRatio(gray, white);
            // Contrast should be very low (typically less than 3:1)
            Assert.True(ratio < 3.0);
        }

        // ── Performance Profiler ────────────────────────────────────────────────

        [Fact]
        public async Task PerformanceProfiler_GeneratesReport_WithExpectedSchema()
        {
            await using var profiler = new PerformanceProfilerService(_tempRoot);

            // Record some mock measurements
            profiler.RecordIpcLatency(15.5);
            profiler.RecordIpcLatency(18.2);
            profiler.RecordFrameLatency(22.0);
            profiler.RecordGestureLatency(5.0);
            profiler.RecordAiResponse(45.0);

            // Trigger internal collection manually
            typeof(PerformanceProfilerService)
                .GetMethod("CollectSnapshot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(profiler, null);

            profiler.GenerateReport();

            var reportPath = Path.Combine(_tempRoot, "Diagnostics", "performance_report.json");
            Assert.True(File.Exists(reportPath));

            var content = File.ReadAllText(reportPath);
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty("timestamp", out _));
            Assert.True(root.TryGetProperty("averages", out _));
            Assert.True(root.TryGetProperty("max", out _));
            Assert.True(root.TryGetProperty("p95", out _));
            Assert.True(root.TryGetProperty("hotspots", out _));
            Assert.True(root.TryGetProperty("recommendations", out _));
        }

        // ── Memory Diagnostics ──────────────────────────────────────────────────

        [Fact]
        public void MemoryDiagnostics_TrackAndUntrack_LeaksAnalysis()
        {
            var memoryDiag = new MemoryDiagnosticsService(_tempRoot);
            var leakyObject = new object();
            var shortLivedObject = new object();

            memoryDiag.Track(leakyObject, "Timers");
            memoryDiag.Track(shortLivedObject, "Mats");

            memoryDiag.Untrack(shortLivedObject); // Short-lived object is untracked properly

            memoryDiag.GenerateReport();

            var reportPath = Path.Combine(_tempRoot, "Diagnostics", "memory_report.json");
            Assert.True(File.Exists(reportPath));

            var content = File.ReadAllText(reportPath);
            using var doc = JsonDocument.Parse(content);
            var categories = doc.RootElement.GetProperty("categories");

            // LeakyObject categorized under "Timers" should still exist in categories
            Assert.Equal(1, categories.GetProperty("Timers").GetInt32());
            // ShortLivedObject categorized under "Mats" should not exist as it was untracked
            Assert.False(categories.TryGetProperty("Mats", out _));
        }

        // ── Accessibility Verifier ──────────────────────────────────────────────

        [Fact]
        public void AccessibilityVerifier_GeneratesReport_WpfActiveWindow()
        {
            var accVerifier = new AccessibilityVerifierService(_tempRoot);

            // Execute verification on active thread context
            accVerifier.VerifyAccessibility();

            var reportPath = Path.Combine(_tempRoot, "Diagnostics", "accessibility_report.json");
            Assert.True(File.Exists(reportPath));

            var content = File.ReadAllText(reportPath);
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty("timestamp", out _));
            Assert.True(root.TryGetProperty("totalElementsChecked", out _));
            Assert.True(root.TryGetProperty("issues", out _));
        }

        // ── System Diagnostics ──────────────────────────────────────────────────

        [Fact]
        public void SystemDiagnostics_CollectsEnvironmentDetails()
        {
            var sysDiag = new SystemDiagnosticsService(_tempRoot);
            sysDiag.GenerateReport();

            var reportPath = Path.Combine(_tempRoot, "Diagnostics", "system_report.json");
            Assert.True(File.Exists(reportPath));

            var content = File.ReadAllText(reportPath);
            using var doc = JsonDocument.Parse(content);
            var hardware = doc.RootElement.GetProperty("hardware");

            Assert.True(hardware.GetProperty("cpuLogicalCores").GetInt32() > 0);
            Assert.NotEmpty(hardware.GetProperty("gpuName").GetString()!);
        }

        // ── Plugin Diagnostics ──────────────────────────────────────────────────

        [Fact]
        public void PluginDiagnostics_GeneratesReport_WithoutPluginManager()
        {
            var pluginDiag = new PluginDiagnosticsService(_tempRoot, pluginManager: null);
            pluginDiag.GenerateReport();

            var reportPath = Path.Combine(_tempRoot, "Diagnostics", "plugin_report.json");
            Assert.True(File.Exists(reportPath));

            var content = File.ReadAllText(reportPath);
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            Assert.Equal(0, root.GetProperty("totalAttempted").GetInt32());
            Assert.Equal(0, root.GetProperty("loadedCount").GetInt32());
        }

        // ── Diagnostics Manager Orchestration ───────────────────────────────────

        [Fact]
        public async Task DiagnosticsManager_OrchestratesExecution()
        {
            await using var profiler = new PerformanceProfilerService(_tempRoot);
            var memoryDiag = new MemoryDiagnosticsService(_tempRoot);
            var accVerifier = new AccessibilityVerifierService(_tempRoot);
            var sysDiag = new SystemDiagnosticsService(_tempRoot);
            var pluginDiag = new PluginDiagnosticsService(_tempRoot, null);

            using var manager = new DiagnosticsManager(profiler, memoryDiag, accVerifier, sysDiag, pluginDiag);
            manager.RunAll();

            // All 5 reports should be generated in the subfolder
            var diagDir = Path.Combine(_tempRoot, "Diagnostics");
            Assert.True(File.Exists(Path.Combine(diagDir, "performance_report.json")));
            Assert.True(File.Exists(Path.Combine(diagDir, "memory_report.json")));
            Assert.True(File.Exists(Path.Combine(diagDir, "accessibility_report.json")));
            Assert.True(File.Exists(Path.Combine(diagDir, "system_report.json")));
            Assert.True(File.Exists(Path.Combine(diagDir, "plugin_report.json")));
        }
    }
}
