using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AirGestureAI.Models;
using OpenCvSharp;

namespace AirGestureAI.HandTracking
{
    /// <summary>
    /// Manages the Python hand landmarker subprocess, communicating via stdin/stdout streams.
    /// </summary>
    public class PythonHandTracker : IHandTracker
    {
        private Process? _pythonProcess;
        private StreamWriter? _stdinWriter;
        private readonly object _processLock = new object();
        private readonly SemaphoreSlim _writeSemaphore = new SemaphoreSlim(1, 1);
        private bool _isTracking;
        private int _crashCount;
        private const int MaxCrashes = 3;
        private string? _scriptPath;
        private readonly System.Collections.Concurrent.ConcurrentQueue<DateTime> _timestampQueue = 
            new System.Collections.Concurrent.ConcurrentQueue<DateTime>();

        /// <summary>
        /// Occurs when hand landmarks have been successfully updated.
        /// </summary>
        public event EventHandler<HandTrackedEventArgs>? HandTracked;

        /// <summary>
        /// Occurs when the tracker encounters a fatal process or parsing error.
        /// </summary>
        public event EventHandler<string>? TrackerError;

        /// <summary>
        /// Gets a value indicating whether the tracking process is active and running.
        /// </summary>
        public bool IsTracking
        {
            get
            {
                lock (_processLock)
                {
                    return _isTracking && _pythonProcess != null && !_pythonProcess.HasExited;
                }
            }
            private set
            {
                lock (_processLock)
                {
                    _isTracking = value;
                }
            }
        }

        /// <summary>
        /// Starts the Python tracking subprocess and performs a communication PING handshake.
        /// </summary>
        public async Task StartAsync()
        {
            lock (_processLock)
            {
                if (IsTracking)
                {
                    Utilities.Logger.Warn("StartAsync called while hand tracker is already running.");
                    return;
                }

                _scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HandTracking", "hand_tracker.py");
                if (!File.Exists(_scriptPath))
                {
                    // Fallback to local project directory structure if running in debug/IDE mode
                    _scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "HandTracking", "hand_tracker.py");
                }

                if (!File.Exists(_scriptPath))
                {
                    throw new FileNotFoundException($"MediaPipe Python script not found at path: {_scriptPath}");
                }

                IsTracking = true;
                _crashCount = 0;
            }

            await StartProcessWithHandshakeAsync();
        }

        private async Task StartProcessWithHandshakeAsync()
        {
            try
            {
                lock (_processLock)
                {
                    // Ensure any lingering processes are terminated
                    CleanupProcess();

                    Utilities.Logger.Info($"Launching Python subprocess with script: {_scriptPath}");

                    // Launch python with '-u' (unbuffered stdout/stdin) to eliminate pipeline latency
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = ResolvePythonExecutable(),
                        Arguments = $"-u \"{_scriptPath}\"",
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    _pythonProcess = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
                    _pythonProcess.Exited += OnProcessExited;

                    if (!_pythonProcess.Start())
                    {
                        throw new Exception("Failed to launch Python subprocess.");
                    }

                    _stdinWriter = _pythonProcess.StandardInput;
                }

                // 1. Wait for "AI Engine Ready" initialization message
                var initTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                string? initLine = await _pythonProcess.StandardOutput.ReadLineAsync(initTimeoutCts.Token);
                if (initLine == null || !initLine.Contains("AI Engine Ready"))
                {
                    throw new Exception($"Failed to receive initialization handshake. Python Output: '{initLine}'");
                }
                Utilities.Logger.Info("Python AI Engine successfully initialized.");

                // 2. Perform PING/PONG communication verification handshake
                bool handshakeSuccess = await VerifyCommunicationAsync();
                if (!handshakeSuccess)
                {
                    throw new Exception("Handshake PING/PONG communication verification failed.");
                }

                // 3. Start background stdout/stderr listening loops
                _ = Task.Run(() => ReadStdoutLoopAsync(_pythonProcess));
                _ = Task.Run(() => ReadStderrLoopAsync(_pythonProcess));
            }
            catch (Exception ex)
            {
                Utilities.Logger.Error("Failed to start Python subprocess or complete handshake", ex);
                TrackerError?.Invoke(this, $"Startup error: {ex.Message}");
                StopAsync().Wait();
                throw;
            }
        }

        private async Task<bool> VerifyCommunicationAsync()
        {
            if (_stdinWriter == null || _pythonProcess == null) return false;

            Utilities.Logger.Info("Sending PING to AI Engine...");
            await _stdinWriter.WriteLineAsync("PING");
            await _stdinWriter.FlushAsync();

            var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            string? response = await _pythonProcess.StandardOutput.ReadLineAsync(timeoutCts.Token);
            Utilities.Logger.Info($"Received response: {response}");

            return response == "PONG";
        }

        /// <summary>
        /// Stops the Python tracking subprocess.
        /// </summary>
        public async Task StopAsync()
        {
            IsTracking = false;
            
            Utilities.Logger.Info("Stopping Python hand tracker...");
            
            // Try to send friendly shutdown signal
            if (_stdinWriter != null)
            {
                try
                {
                    await _stdinWriter.WriteLineAsync("SHUTDOWN");
                    await _stdinWriter.FlushAsync();
                }
                catch
                {
                    // Ignore write failures on exit
                }
            }

            CleanupProcess();
            Utilities.Logger.Info("Python hand tracker stopped.");
        }

        private void CleanupProcess()
        {
            lock (_processLock)
            {
                if (_stdinWriter != null)
                {
                    try { _stdinWriter.Dispose(); } catch { }
                    _stdinWriter = null;
                }

                if (_pythonProcess != null)
                {
                    try
                    {
                        _pythonProcess.Exited -= OnProcessExited;
                        if (!_pythonProcess.HasExited)
                        {
                            _pythonProcess.Kill(entireProcessTree: true);
                        }
                        _pythonProcess.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Utilities.Logger.Error("Error disposing Python process", ex);
                    }
                    _pythonProcess = null;
                }
                
                // Clear the timestamp queue to prevent stale entries on restart
                while (_timestampQueue.TryDequeue(out _)) { }
            }
        }

        private byte[]? _pixelBuffer;

        /// <summary>
        /// Sends a raw BGR Mat image frame to the Python subprocess stdin.
        /// </summary>
        public async Task ProcessFrameAsync(Mat frame, DateTime captureTimestamp)
        {
            if (!IsTracking || _stdinWriter == null || _pythonProcess == null) return;

            // Enforce sequential thread-safe access to standard input stream to prevent framing corruption
            await _writeSemaphore.WaitAsync();
            try
            {
                if (frame.Empty()) return;

                // Enqueue the capture timestamp before writing the frame so it stays in exact alignment
                _timestampQueue.Enqueue(captureTimestamp);

                int width = frame.Width;
                int height = frame.Height;
                int size = width * height * 3; // BGR 24-bit

                // Reuse the pixel buffer to avoid frequent allocations (approx 900KB per frame)
                if (_pixelBuffer == null || _pixelBuffer.Length != size)
                {
                    _pixelBuffer = new byte[size];
                }
                Marshal.Copy(frame.Data, _pixelBuffer, 0, size);

                // Send frame write command
                string cmd = $"FRAME {width} {height}";
                byte[] cmdBytes = Encoding.UTF8.GetBytes(cmd + "\n");
                
                // Write command header
                await _stdinWriter.BaseStream.WriteAsync(cmdBytes, 0, cmdBytes.Length);
                
                // Write raw pixel payload
                await _stdinWriter.BaseStream.WriteAsync(_pixelBuffer, 0, size);
                await _stdinWriter.BaseStream.FlushAsync();
            }
            catch (Exception ex)
            {
                Utilities.Logger.Error("Error writing frame to Python process stdin", ex);
                // Trigger pipeline error
                TrackerError?.Invoke(this, $"Pipeline write error: {ex.Message}");
            }
            finally
            {
                _writeSemaphore.Release();
            }
        }

        private async Task ReadStdoutLoopAsync(Process process)
        {
            var reader = process.StandardOutput;
            while (IsTracking && !process.HasExited)
            {
                try
                {
                    string? line = await reader.ReadLineAsync();
                    if (line == null) break; // EOF

                    if (line == "PONG") continue; // Handshake lines handled separately on startup, skip in main loop

                    // Parse tracking output JSON
                    ParseAndNotifyTrackingData(line);
                }
                catch (Exception ex)
                {
                    Utilities.Logger.Error("Error reading stdout stream from Python", ex);
                }
            }
        }

        private async Task ReadStderrLoopAsync(Process process)
        {
            var reader = process.StandardError;
            while (IsTracking && !process.HasExited)
            {
                try
                {
                    string? line = await reader.ReadLineAsync();
                    if (line == null) break;

                    Utilities.Logger.Warn($"[Python Engine] {line}");
                }
                catch (Exception ex)
                {
                    Utilities.Logger.Error("Error reading stderr stream from Python", ex);
                }
            }
        }

        private void ParseAndNotifyTrackingData(string jsonLine)
        {
            // Dequeue corresponding capture timestamp
            if (!_timestampQueue.TryDequeue(out DateTime captureTimestamp))
            {
                captureTimestamp = DateTime.UtcNow;
            }

            try
            {
                using (var doc = JsonDocument.Parse(jsonLine))
                {
                    var root = doc.RootElement;
                    if (root.TryGetProperty("error", out var errorProp))
                    {
                        Utilities.Logger.Warn($"Python inference returned error: {errorProp.GetString()}");
                        return;
                    }

                    bool handDetected = root.GetProperty("handDetected").GetBoolean();
                    var handData = new HandData { IsDetected = handDetected };

                    if (handDetected)
                    {
                        handData.Wrist = ParseLandmark(root.GetProperty("wrist"));
                        handData.PalmCenter = ParseLandmark(root.GetProperty("palmCenter"));
                        handData.ThumbTip = ParseLandmark(root.GetProperty("thumbTip"));
                        handData.IndexTip = ParseLandmark(root.GetProperty("indexTip"));

                        // Parse the full 21 landmarks array if present
                        if (root.TryGetProperty("landmarks", out var landmarksProp) && landmarksProp.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var lmElement in landmarksProp.EnumerateArray())
                            {
                                handData.Landmarks.Add(ParseLandmark(lmElement));
                            }
                        }
                    }

                    HandTracked?.Invoke(this, new HandTrackedEventArgs(handData, captureTimestamp));
                }
            }
            catch (Exception ex)
            {
                Utilities.Logger.Error($"Failed to parse JSON landmark payload: {jsonLine}", ex);
            }
        }

        private Landmark ParseLandmark(JsonElement element)
        {
            return new Landmark(
                (float)element.GetProperty("x").GetDouble(),
                (float)element.GetProperty("y").GetDouble(),
                (float)element.GetProperty("z").GetDouble()
            );
        }

        private void OnProcessExited(object? sender, EventArgs e)
        {
            if (!IsTracking) return; // Expected termination

            Utilities.Logger.Warn("Python subprocess exited unexpectedly.");

            lock (_processLock)
            {
                _crashCount++;
                if (_crashCount >= MaxCrashes)
                {
                    Utilities.Logger.Error("Python tracker subprocess has crashed repeatedly. Disabling hand tracker.");
                    TrackerError?.Invoke(this, "AI Engine crashed repeatedly. Please check Python dependencies or scripts.");
                    IsTracking = false;
                    CleanupProcess();
                    return;
                }
            }

            // Attempt automatic recovery
            Utilities.Logger.Info($"Attempting to recover Python subprocess (attempt {_crashCount}/{MaxCrashes})...");
            _ = Task.Run(async () =>
            {
                await Task.Delay(2000); // Wait 2s before restart to prevent tight loops
                if (IsTracking)
                {
                    try
                    {
                        await StartProcessWithHandshakeAsync();
                    }
                    catch (Exception ex)
                    {
                        Utilities.Logger.Error("Failed to recover Python process during auto-restart", ex);
                    }
                }
            });
        }

        /// <summary>
        /// Resolves the Python executable path, prioritizing a local .venv if present,
        /// and falling back to "python" on the system PATH.
        /// </summary>
        public static string ResolvePythonExecutable()
        {
            try
            {
                string? dir = AppDomain.CurrentDomain.BaseDirectory;
                while (!string.IsNullOrEmpty(dir))
                {
                    string venvPy = Path.Combine(dir, ".venv", "Scripts", "python.exe");
                    if (File.Exists(venvPy))
                    {
                        return Path.GetFullPath(venvPy);
                    }
                    var parent = Directory.GetParent(dir);
                    if (parent == null || parent.FullName == dir) break;
                    dir = parent.FullName;
                }

                string cwdPy = Path.Combine(Directory.GetCurrentDirectory(), ".venv", "Scripts", "python.exe");
                if (File.Exists(cwdPy))
                {
                    return Path.GetFullPath(cwdPy);
                }
            }
            catch
            {
                // Fall back safely to "python" on any path resolution exception
            }

            return "python";
        }

        /// <summary>
        /// Disposes unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            StopAsync().Wait();
            _writeSemaphore.Dispose();
        }
    }
}
