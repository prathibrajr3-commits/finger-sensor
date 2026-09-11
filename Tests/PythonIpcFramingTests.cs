using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AirGestureAI.HandTracking;
using Xunit;

namespace AirGestureAI.Tests
{
    public sealed class PythonIpcFramingTests : IDisposable
    {
        private Process? _process;
        private StreamWriter? _stdinWriter;
        private StreamReader? _stdoutReader;
        private readonly string _pythonExe;
        private readonly string _scriptPath;

        public PythonIpcFramingTests()
        {
            _pythonExe = PythonHandTracker.ResolvePythonExecutable();

            string? dir = AppDomain.CurrentDomain.BaseDirectory;
            string scriptPath = "";
            while (!string.IsNullOrEmpty(dir))
            {
                string candidate = Path.Combine(dir, "HandTracking", "hand_tracker.py");
                if (File.Exists(candidate))
                {
                    scriptPath = Path.GetFullPath(candidate);
                    break;
                }
                var parent = Directory.GetParent(dir);
                if (parent == null || parent.FullName == dir) break;
                dir = parent.FullName;
            }

            if (string.IsNullOrEmpty(scriptPath))
            {
                scriptPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "HandTracking", "hand_tracker.py"));
            }

            _scriptPath = scriptPath;
        }

        private async Task StartTrackerProcessAsync()
        {
            Assert.True(File.Exists(_scriptPath), $"Script file not found at: {_scriptPath}");

            var psi = new ProcessStartInfo
            {
                FileName = _pythonExe,
                Arguments = $"-u \"{_scriptPath}\"",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            _process = new Process { StartInfo = psi };
            bool started = _process.Start();
            Assert.True(started, "Failed to start Python process.");

            _stdinWriter = _process.StandardInput;
            _stdoutReader = _process.StandardOutput;

            // Wait for initialization line
            string? initLine = await _stdoutReader.ReadLineAsync();
            if (initLine == null)
            {
                string err = await _process.StandardError.ReadToEndAsync();
                Assert.Fail($"Process closed stdout immediately without initialization. Stderr: {err} | Python: {_pythonExe} | Script: {_scriptPath}");
            }
            Assert.Contains("AI Engine Ready", initLine);
        }

        public void Dispose()
        {
            if (_process != null && !_process.HasExited)
            {
                try
                {
                    _stdinWriter?.WriteLine("SHUTDOWN");
                    _stdinWriter?.Flush();
                    if (!_process.WaitForExit(2000))
                    {
                        _process.Kill(entireProcessTree: true);
                    }
                }
                catch { }
                _process.Dispose();
            }
        }

        [Fact]
        public async Task PingPongHandshake_ReturnsPong()
        {
            await StartTrackerProcessAsync();

            await _stdinWriter!.WriteLineAsync("PING");
            await _stdinWriter.FlushAsync();

            string? response = await _stdoutReader!.ReadLineAsync();
            Assert.Equal("PONG", response);
        }

        [Fact]
        public async Task ConsecutiveFrames_DoNotDesynchronizeStream()
        {
            await StartTrackerProcessAsync();

            // PING first
            await _stdinWriter!.WriteLineAsync("PING");
            await _stdinWriter.FlushAsync();
            string? pingResp = await _stdoutReader!.ReadLineAsync();
            Assert.Equal("PONG", pingResp);

            // Send 10 consecutive frames of 2x2 with embedded newline bytes (0x0A)
            int width = 2;
            int height = 2;
            int payloadSize = width * height * 3; // 12 bytes

            // Byte array with embedded 0x0A (ASCII '\n') to test that raw bytes are never misread as text commands
            byte[] rawFrame = new byte[] { 0, 1, 2, 10, 4, 5, 10, 7, 8, 9, 10, 11 };

            for (int i = 0; i < 10; i++)
            {
                byte[] headerBytes = Encoding.UTF8.GetBytes($"FRAME {width} {height}\n");
                await _stdinWriter.BaseStream.WriteAsync(headerBytes, 0, headerBytes.Length);
                await _stdinWriter.BaseStream.WriteAsync(rawFrame, 0, payloadSize);
                await _stdinWriter.BaseStream.FlushAsync();

                string? line = await _stdoutReader!.ReadLineAsync();
                Assert.NotNull(line);

                using var doc = JsonDocument.Parse(line);
                Assert.True(doc.RootElement.TryGetProperty("handDetected", out _), $"Frame {i} response missing 'handDetected': {line}");
                bool hasError = doc.RootElement.TryGetProperty("error", out var errorProp);
                Assert.False(hasError, $"Frame {i} returned error: {(hasError ? errorProp.GetString() : string.Empty)}");
            }

            // Verify PING works immediately after frames (proving stream remains in exact alignment)
            await _stdinWriter.WriteLineAsync("PING");
            await _stdinWriter.FlushAsync();
            string? postPing = await _stdoutReader!.ReadLineAsync();
            Assert.Equal("PONG", postPing);
        }

        [Fact]
        public async Task MalformedFrameHeader_ReportsErrorWithoutCrashing()
        {
            await StartTrackerProcessAsync();

            // Send invalid FRAME header
            await _stdinWriter!.WriteLineAsync("FRAME not_an_int also_not_int");
            await _stdinWriter.FlushAsync();

            string? errLine = await _stdoutReader!.ReadLineAsync();
            Assert.NotNull(errLine);
            using var doc = JsonDocument.Parse(errLine);
            Assert.True(doc.RootElement.TryGetProperty("error", out var errorProp));
            Assert.Contains("Invalid FRAME dimensions", errorProp.GetString());

            // Process must remain alive and responsive to PING
            await _stdinWriter.WriteLineAsync("PING");
            await _stdinWriter.FlushAsync();
            string? pong = await _stdoutReader!.ReadLineAsync();
            Assert.Equal("PONG", pong);
        }

        [Fact]
        public async Task UnknownCommand_ReportsErrorWithoutCrashing()
        {
            await StartTrackerProcessAsync();

            await _stdinWriter!.WriteLineAsync("UNKNOWN_TEST_COMMAND");
            await _stdinWriter.FlushAsync();

            string? errLine = await _stdoutReader!.ReadLineAsync();
            Assert.NotNull(errLine);
            using var doc = JsonDocument.Parse(errLine);
            Assert.True(doc.RootElement.TryGetProperty("error", out var errorProp));
            Assert.Contains("Unknown command", errorProp.GetString());

            // Process must remain alive and responsive to PING
            await _stdinWriter.WriteLineAsync("PING");
            await _stdinWriter.FlushAsync();
            string? pong = await _stdoutReader!.ReadLineAsync();
            Assert.Equal("PONG", pong);
        }

        [Fact]
        public async Task ShutdownCommand_ExitsCleanly()
        {
            await StartTrackerProcessAsync();

            await _stdinWriter!.WriteLineAsync("SHUTDOWN");
            await _stdinWriter.FlushAsync();

            bool exited = _process!.WaitForExit(3000);
            Assert.True(exited, "Process did not exit after SHUTDOWN command.");
            Assert.Equal(0, _process.ExitCode);
        }
    }
}
