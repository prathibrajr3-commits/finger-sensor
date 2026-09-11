using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AirGestureAI.Configuration;
using AirGestureAI.Utilities;

namespace AirGestureAI.Services
{
    // ── Wizard step state ────────────────────────────────────────────────────

    /// <summary>Represents the outcome of a single setup wizard environment check.</summary>
    public sealed class WizardCheckResult
    {
        /// <summary>Gets the name of the check performed.</summary>
        public string CheckName { get; init; } = string.Empty;

        /// <summary>Gets whether the check passed.</summary>
        public bool Passed { get; init; }

        /// <summary>Gets a human-readable description of the outcome.</summary>
        public string Message { get; init; } = string.Empty;
    }

    /// <summary>Aggregate of all environment pre-check results.</summary>
    public sealed class EnvironmentCheckReport
    {
        /// <summary>Gets the individual check results.</summary>
        public IReadOnlyList<WizardCheckResult> Results { get; init; } = Array.Empty<WizardCheckResult>();

        /// <summary>Gets whether all required checks passed.</summary>
        public bool AllPassed => Results.All(r => r.Passed);
    }

    // ── Service ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Handles first-run detection and the environment pre-checks executed during the
    /// Setup Wizard (camera availability, Python &amp; MediaPipe presence, ONNX model
    /// verification, GPU provider detection).
    /// Persists completion state to <c>appsettings.json</c> via <see cref="AppConfig"/>.
    /// </summary>
    public sealed class SetupWizardService
    {
        private const string SettingsFileName = "appsettings.json";

        private readonly AppConfig _config;
        private readonly string    _settingsPath;

        /// <summary>
        /// Initializes a new instance of <see cref="SetupWizardService"/>.
        /// </summary>
        /// <param name="config">Application configuration singleton.</param>
        /// <param name="settingsPath">Optional explicit settings file path.</param>
        public SetupWizardService(AppConfig config, string? settingsPath = null)
        {
            _config       = config ?? throw new ArgumentNullException(nameof(config));
            _settingsPath = settingsPath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AirGestureAI",
                SettingsFileName);
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Returns <c>true</c> when the first-run wizard has never been completed on
        /// this machine, indicating it should be shown on startup.
        /// </summary>
        public bool IsFirstRun => !_config.IsFirstRunComplete;

        /// <summary>
        /// Runs all environment pre-checks asynchronously and returns an
        /// <see cref="EnvironmentCheckReport"/> with individual results.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        public async Task<EnvironmentCheckReport> RunChecksAsync(CancellationToken ct = default)
        {
            Logger.Info("SetupWizardService: Running environment pre-checks…");

            var results = new List<WizardCheckResult>();

            results.Add(await CheckCameraAsync(ct));
            results.Add(await CheckPythonAsync(ct));
            results.Add(await CheckMediaPipeAsync(ct));
            results.Add(CheckOnnxModels());
            results.Add(CheckGpuProvider());

            var report = new EnvironmentCheckReport { Results = results };
            Logger.Info($"SetupWizardService: Checks complete. AllPassed={report.AllPassed}");
            return report;
        }

        /// <summary>
        /// Marks the wizard as complete and persists the state to <c>appsettings.json</c>.
        /// </summary>
        public void MarkComplete()
        {
            _config.IsFirstRunComplete = true;
            PersistConfig();
            Logger.Info("SetupWizardService: First-run wizard marked complete.");
        }

        // ── Individual checks ─────────────────────────────────────────────────

        private async Task<WizardCheckResult> CheckCameraAsync(CancellationToken ct)
        {
            try
            {
                await Task.Delay(50, ct); // OpenCV probe is synchronous internally; yield for UI
                // Attempt to open the configured camera index to verify availability
                using var cap = new OpenCvSharp.VideoCapture(_config.CameraIndex);
                bool opened = cap.IsOpened();
                return new WizardCheckResult
                {
                    CheckName = "Camera",
                    Passed    = opened,
                    Message   = opened
                        ? $"Camera device {_config.CameraIndex} detected successfully."
                        : $"Camera device {_config.CameraIndex} could not be opened. Please connect a webcam."
                };
            }
            catch (Exception ex)
            {
                Logger.Warn($"SetupWizardService.CheckCameraAsync: {ex.Message}");
                return new WizardCheckResult
                {
                    CheckName = "Camera",
                    Passed    = false,
                    Message   = $"Camera check failed: {ex.Message}"
                };
            }
        }

        private async Task<WizardCheckResult> CheckPythonAsync(CancellationToken ct)
        {
            try
            {
                string pyExe = AirGestureAI.HandTracking.PythonHandTracker.ResolvePythonExecutable();
                var psi = new ProcessStartInfo(pyExe, "--version")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                };

                using var proc = Process.Start(psi);
                if (proc is null)
                {
                    return new WizardCheckResult
                    {
                        CheckName = "Python",
                        Passed    = false,
                        Message   = "Python could not be launched. Ensure Python 3.10+ is installed and on PATH."
                    };
                }

                var output = await proc.StandardOutput.ReadToEndAsync(ct);
                var error  = await proc.StandardError.ReadToEndAsync(ct);
                await proc.WaitForExitAsync(ct);

                string version = (output + error).Trim();
                bool passed    = proc.ExitCode == 0;

                return new WizardCheckResult
                {
                    CheckName = "Python",
                    Passed    = passed,
                    Message   = passed
                        ? $"Python detected: {version}"
                        : "Python not found. Install Python 3.10+ and add it to PATH."
                };
            }
            catch (Exception ex)
            {
                Logger.Warn($"SetupWizardService.CheckPythonAsync: {ex.Message}");
                return new WizardCheckResult
                {
                    CheckName = "Python",
                    Passed    = false,
                    Message   = "Python not found on PATH. Install Python 3.10+ to enable gesture tracking."
                };
            }
        }

        private async Task<WizardCheckResult> CheckMediaPipeAsync(CancellationToken ct)
        {
            try
            {
                string pyExe = AirGestureAI.HandTracking.PythonHandTracker.ResolvePythonExecutable();
                var psi = new ProcessStartInfo(pyExe, "-c \"import mediapipe; print(mediapipe.__version__)\"")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                };

                using var proc = Process.Start(psi);
                if (proc is null)
                    return Fail("MediaPipe", "Failed to start Python process.");

                var output = await proc.StandardOutput.ReadToEndAsync(ct);
                var error  = await proc.StandardError.ReadToEndAsync(ct);
                await proc.WaitForExitAsync(ct);

                bool passed = proc.ExitCode == 0 && !output.Trim().StartsWith("Error");
                return new WizardCheckResult
                {
                    CheckName = "MediaPipe",
                    Passed    = passed,
                    Message   = passed
                        ? $"MediaPipe {output.Trim()} detected."
                        : "MediaPipe not installed. Run: pip install mediapipe"
                };
            }
            catch (Exception ex)
            {
                Logger.Warn($"SetupWizardService.CheckMediaPipeAsync: {ex.Message}");
                return Fail("MediaPipe", $"MediaPipe check failed: {ex.Message}");
            }
        }

        private WizardCheckResult CheckOnnxModels()
        {
            try
            {
                var modelDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AirGestureAI", "Models");

                bool dirExists  = Directory.Exists(modelDir);
                bool hasModels  = dirExists && Directory.GetFiles(modelDir, "*.onnx").Length > 0;

                return new WizardCheckResult
                {
                    CheckName = "ONNX Models",
                    Passed    = true, // Fallback simulation is always available
                    Message   = hasModels
                        ? $"ONNX model files found in {modelDir}."
                        : "No .onnx model files found — CPU fallback simulation will be used."
                };
            }
            catch (Exception ex)
            {
                Logger.Warn($"SetupWizardService.CheckOnnxModels: {ex.Message}");
                return new WizardCheckResult { CheckName = "ONNX Models", Passed = true, Message = "ONNX check skipped — fallback active." };
            }
        }

        private WizardCheckResult CheckGpuProvider()
        {
            try
            {
                // Query DirectML provider availability via ONNX Runtime
                var sessionOptions = new Microsoft.ML.OnnxRuntime.SessionOptions();
                sessionOptions.AppendExecutionProvider_DML(0);
                string provider = "DirectML (GPU)";

                return new WizardCheckResult
                {
                    CheckName = "GPU Provider",
                    Passed    = true,
                    Message   = $"{provider} acceleration is available."
                };
            }
            catch
            {
                return new WizardCheckResult
                {
                    CheckName = "GPU Provider",
                    Passed    = true,
                    Message   = "GPU acceleration not available — CPU multi-threaded fallback will be used."
                };
            }
        }

        // ── Persistence ───────────────────────────────────────────────────────

        private void PersistConfig()
        {
            try
            {
                var dir = Path.GetDirectoryName(_settingsPath);
                if (dir is not null) Directory.CreateDirectory(dir);

                // Read existing JSON (if any) and upsert IsFirstRunComplete
                Dictionary<string, object>? data = null;
                if (File.Exists(_settingsPath))
                {
                    try
                    {
                        var raw = File.ReadAllText(_settingsPath);
                        if (!string.IsNullOrWhiteSpace(raw))
                        {
                            data = JsonSerializer.Deserialize<Dictionary<string, object>>(raw);
                        }
                    }
                    catch
                    {
                        data = null;
                    }
                }
                data ??= new Dictionary<string, object>();

                data["IsFirstRunComplete"] = true;
                File.WriteAllText(_settingsPath, JsonSerializer.Serialize(data,
                    new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                Logger.Error("SetupWizardService: Failed to persist config", ex);
            }
        }

        private static WizardCheckResult Fail(string name, string msg) =>
            new() { CheckName = name, Passed = false, Message = msg };
    }
}
