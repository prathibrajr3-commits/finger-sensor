using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace AirGestureAI.Utilities
{
    /// <summary>
    /// Manages Python environment verification and package installation.
    /// </summary>
    public class DependencyManager
    {
        private readonly string _pythonCommand = "python";

        /// <summary>
        /// Occurs when a dependency check or install progress log is available.
        /// </summary>
        public event Action<string>? ProgressLogged;

        /// <summary>
        /// Checks if Python is installed, validates version support, and installs required packages (mediapipe, opencv-python, numpy) if they are missing.
        /// </summary>
        /// <returns>True if Python environment is ready; false otherwise.</returns>
        public async Task<bool> VerifyDependenciesAsync()
        {
            LogProgress("Verifying Python environment...");

            // 1. Check if Python is installed and get version
            var (isInstalled, versionStr) = await CheckPythonInstalledAsync();
            if (!isInstalled)
            {
                LogProgress("Error: Python is not detected on the system. Please install Python 3.8+ (x64) and add it to your PATH.");
                LogProgress("You can install Python easily via PowerShell: winget install Python.Python.3.11");
                return false;
            }

            LogProgress($"Python detected: {versionStr}");

            // Verify version is supported (>= 3.8)
            if (!IsVersionSupported(versionStr, out int major, out int minor))
            {
                LogProgress($"Error: Detected Python version {versionStr} is not officially supported. AirGesture AI requires Python 3.8+.");
                return false;
            }

            LogProgress($"Python version {major}.{minor} is compatible.");

            // 2. Check if required packages are installed (mediapipe, opencv-python, numpy)
            LogProgress("Checking required packages (mediapipe, opencv-python, numpy)...");
            bool packagesInstalled = await CheckPythonPackagesAsync();
            if (packagesInstalled)
            {
                LogProgress("All Python packages are verified and ready.");
                return true;
            }

            // 3. Attempt to install packages if missing
            LogProgress("Required packages are missing. Attempting to install via pip...");
            bool installSuccess = await InstallPythonPackagesAsync();
            if (installSuccess)
            {
                LogProgress("Successfully installed required Python packages.");
                return true;
            }

            LogProgress("Error: Failed to install Python dependencies. Please run manually: pip install mediapipe opencv-python numpy");
            return false;
        }

        private async Task<(bool isInstalled, string version)> CheckPythonInstalledAsync()
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = _pythonCommand,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process == null) return (false, string.Empty);

                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                // Python version might print to stdout or stderr depending on version
                string result = (output + error).Trim();
                if (process.ExitCode == 0 && !string.IsNullOrEmpty(result))
                {
                    return (true, result);
                }
                return (false, string.Empty);
            }
            catch (Exception ex)
            {
                Logger.Error("Python installation check failed", ex);
                return (false, string.Empty);
            }
        }

        private bool IsVersionSupported(string versionStr, out int major, out int minor)
        {
            major = 0;
            minor = 0;

            try
            {
                // Clean version string (e.g. "Python 3.10.5" -> "3.10.5")
                string cleanVersion = versionStr.Replace("Python", "").Trim();
                string[] parts = cleanVersion.Split('.');
                if (parts.Length >= 2 && int.TryParse(parts[0], out major) && int.TryParse(parts[1], out minor))
                {
                    return major == 3 && minor >= 8;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to parse Python version string '{versionStr}'", ex);
            }

            return false;
        }

        private async Task<bool> CheckPythonPackagesAsync()
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = _pythonCommand,
                    Arguments = "-c \"import cv2, mediapipe, numpy\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process == null) return false;

                await process.WaitForExitAsync();
                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                Logger.Error("Python package validation check failed", ex);
                return false;
            }
        }

        private async Task<bool> InstallPythonPackagesAsync()
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = _pythonCommand,
                    Arguments = "-m pip install --upgrade pip",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                LogProgress("Upgrading pip...");
                using (var process = Process.Start(startInfo))
                {
                    if (process != null) await process.WaitForExitAsync();
                }

                startInfo.Arguments = "-m pip install mediapipe opencv-python numpy";
                LogProgress("Executing: pip install mediapipe opencv-python numpy");

                using var pipProcess = Process.Start(startInfo);
                if (pipProcess == null) return false;

                // Read output to log progress
                var outputTask = ReadStreamAsync(pipProcess.StandardOutput);
                var errorTask = ReadStreamAsync(pipProcess.StandardError);

                await Task.WhenAll(outputTask, errorTask, pipProcess.WaitForExitAsync());

                return pipProcess.ExitCode == 0;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to auto-install Python packages", ex);
                return false;
            }
        }

        private async Task ReadStreamAsync(StreamReader reader)
        {
            while (!reader.EndOfStream)
            {
                string? line = await reader.ReadLineAsync();
                if (line != null)
                {
                    LogProgress($"[Pip] {line}");
                }
            }
        }

        private void LogProgress(string message)
        {
            Logger.Info(message);
            ProgressLogged?.Invoke(message);
        }
    }
}
