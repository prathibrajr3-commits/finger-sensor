using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AirGestureAI.IPC;
using AirGestureAI.HandTracking;
using AirGestureAI.Models;
using AirGestureAI.Utilities;
using OpenCvSharp;

namespace AirGestureAI.TrackerHost
{
    /// <summary>Represents an active coordinate-tracking session.</summary>
    public sealed class TrackerSession
    {
        /// <summary>Gets or sets the session identifier.</summary>
        public string SessionId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>Gets or sets whether tracking is currently active.</summary>
        public bool IsActive { get; set; }

        /// <summary>Gets or sets the frames-per-second observed in the last window.</summary>
        public double CurrentFps { get; set; }

        /// <summary>Gets or sets the average parsing latency in milliseconds.</summary>
        public double AverageLatencyMs { get; set; }
    }

    /// <summary>
    /// Profiles frame rates and coordinate parsing latencies for the tracker subprocess.
    /// </summary>
    public sealed class TrackerDiagnostics
    {
        private int _frameCount;
        private double _totalLatencyMs;

        /// <summary>Gets the number of frames counted in the current session.</summary>
        public int FrameCount => _frameCount;

        /// <summary>Gets the average latency in milliseconds.</summary>
        public double AverageLatencyMs => _frameCount == 0 ? 0 : _totalLatencyMs / _frameCount;

        /// <summary>Records a single frame parse event.</summary>
        public void RecordFrame(double latencyMs)
        {
            Interlocked.Increment(ref _frameCount);
            _totalLatencyMs += latencyMs;
        }

        /// <summary>Resets all counters.</summary>
        public void Reset()
        {
            _frameCount = 0;
            _totalLatencyMs = 0;
        }
    }

    /// <summary>
    /// Manages the physical lifetime of the tracker subprocess (via CLI args in self-launch mode).
    /// </summary>
    public sealed class TrackerProcess
    {
        private bool _isRunning;

        /// <summary>Gets whether the tracker process is running.</summary>
        public bool IsRunning => _isRunning;

        /// <summary>Starts the simulated tracker process.</summary>
        public void Start()
        {
            _isRunning = true;
            Logger.Info("TrackerProcess: Tracker process started (self-launch mode).");
        }

        /// <summary>Stops the tracker process.</summary>
        public void Stop()
        {
            _isRunning = false;
            Logger.Info("TrackerProcess: Tracker process stopped.");
        }
    }

    /// <summary>
    /// Bridges the main application to the tracker host over Named Pipe IPC.
    /// Acts as an IHandTracker implementation when running in out-of-process isolation mode.
    /// </summary>
    public sealed class TrackerBridge : IHandTracker
    {
        private readonly IpcClient _client;
        private bool _isTracking;

        /// <summary>Occurs when hand landmarks have been successfully updated.</summary>
        public event EventHandler<HandTrackedEventArgs>? HandTracked;

        /// <summary>Occurs when the tracker encounters a fatal process or parsing error.</summary>
        public event EventHandler<string>? TrackerError;

        /// <summary>Gets a value indicating whether the tracking process is active and running.</summary>
        public bool IsTracking => _isTracking;

        /// <summary>Initializes a new instance of <see cref="TrackerBridge"/>.</summary>
        public TrackerBridge()
        {
            _client = new IpcClient("AirGestureAI_Tracker_Pipe");
        }

        /// <summary>Sends a ping to the tracker host and returns the response.</summary>
        public async Task<string> PingAsync(CancellationToken ct = default)
        {
            var response = await _client.SendAsync(new IpcMessage { Method = "Ping", Payload = "health_check" }, ct);
            return response.Payload;
        }

        /// <summary>Starts tracking mode on the remote host.</summary>
        public async Task StartAsync()
        {
            _isTracking = true;
            Logger.Info("TrackerBridge: Remote tracking mode initiated.");
            await Task.CompletedTask;
        }

        /// <summary>Stops tracking mode on the remote host.</summary>
        public async Task StopAsync()
        {
            _isTracking = false;
            Logger.Info("TrackerBridge: Remote tracking mode stopped.");
            await Task.CompletedTask;
        }

        /// <summary>
        /// Converts the frame to a Base64-encoded JPEG and submits it to the remote TrackerHost via Named Pipe.
        /// </summary>
        public async Task ProcessFrameAsync(Mat frame, DateTime captureTimestamp)
        {
            if (!_isTracking || frame == null || frame.Empty()) return;

            try
            {
                // Encode the frame as JPEG to reduce IPC payload size
                Cv2.ImEncode(".jpg", frame, out byte[] jpegBytes);
                var payload = Convert.ToBase64String(jpegBytes);

                var response = await _client.SendAsync(new IpcMessage { Method = "ProcessFrame", Payload = payload });
                if (response.Method == "ProcessFrame_Response")
                {
                    if (response.Payload.StartsWith("ERROR:"))
                    {
                        TrackerError?.Invoke(this, response.Payload);
                    }
                    else
                    {
                        var handData = JsonSerializer.Deserialize<HandData>(response.Payload);
                        if (handData != null)
                        {
                            HandTracked?.Invoke(this, new HandTrackedEventArgs(handData, captureTimestamp));
                        }
                    }
                }
                else
                {
                    TrackerError?.Invoke(this, $"IPC response error: {response.Payload}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("TrackerBridge: ProcessFrameAsync failed", ex);
                TrackerError?.Invoke(this, ex.Message);
            }
        }

        /// <summary>Releases bridge resources.</summary>
        public void Dispose()
        {
            _isTracking = false;
        }
    }

    /// <summary>
    /// Runs the tracker command loop when the application is launched with --tracker-host.
    /// Exposes a real MediaPipe hand coordinate tracking pipeline to the parent process.
    /// </summary>
    public sealed class TrackerHost
    {
        private readonly TrackerProcess _process = new();
        private readonly TrackerDiagnostics _diagnostics = new();
        private readonly IpcRouter _router = new();
        private IpcServer? _server;
        private PythonHandTracker? _pythonTracker;
        private TaskCompletionSource<HandTrackedEventArgs>? _pendingFrameTcs;

        /// <summary>Gets the current tracker session.</summary>
        public TrackerSession Session { get; } = new();

        /// <summary>Gets the diagnostics profiler for this host.</summary>
        public TrackerDiagnostics Diagnostics => _diagnostics;

        /// <summary>Starts the tracker host IPC server.</summary>
        public void Start()
        {
            _process.Start();
            Session.IsActive = true;

            // Instantiate and start the real MediaPipe hand tracker python runner
            _pythonTracker = new PythonHandTracker();
            _pythonTracker.HandTracked += OnHandTracked;
            _pythonTracker.TrackerError += OnTrackerError;
            
            try
            {
                _pythonTracker.StartAsync().Wait();
            }
            catch (Exception ex)
            {
                Logger.Error("TrackerHost: Failed to start Python tracker process", ex);
            }

            // Register handlers in our router
            _router.Register("Ping", req => "PONG");
            _router.Register("ProcessFrame", req =>
            {
                if (_pythonTracker == null || !_pythonTracker.IsTracking)
                {
                    return "ERROR: PythonHandTracker is not active.";
                }

                try
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    
                    // Decode image from base64 JPEG payload
                    var jpegBytes = Convert.FromBase64String(req.Payload);
                    using var mat = Cv2.ImDecode(jpegBytes, ImreadModes.Color);
                    
                    if (mat == null || mat.Empty())
                    {
                        return "ERROR: Decoded Mat frame is empty.";
                    }

                    // Coordinate response via TaskCompletionSource
                    var tcs = new TaskCompletionSource<HandTrackedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
                    _pendingFrameTcs = tcs;

                    _pythonTracker.ProcessFrameAsync(mat, DateTime.UtcNow).Wait();

                    // Block and wait for Python subprocess response with a 500ms timeout
                    if (tcs.Task.Wait(500))
                    {
                        sw.Stop();
                        _diagnostics.RecordFrame(sw.Elapsed.TotalMilliseconds);
                        return JsonSerializer.Serialize(tcs.Task.Result.HandData);
                    }
                    else
                    {
                        return "{\"IsDetected\":false,\"Error\":\"MediaPipe process timeout\"}";
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("TrackerHost: ProcessFrame handler failed", ex);
                    return $"ERROR: {ex.Message}";
                }
            });

            _server = new IpcServer("AirGestureAI_Tracker_Pipe", _router);
            _server.Start();
            Logger.Info("TrackerHost: IPC server started on 'AirGestureAI_Tracker_Pipe'.");
        }

        private void OnHandTracked(object? sender, HandTrackedEventArgs e)
        {
            _pendingFrameTcs?.TrySetResult(e);
        }

        private void OnTrackerError(object? sender, string error)
        {
            Logger.Error($"TrackerHost: Python tracker error: {error}");
        }

        /// <summary>Stops the tracker host.</summary>
        public void Stop()
        {
            _server?.Stop();
            if (_pythonTracker != null)
            {
                _pythonTracker.HandTracked -= OnHandTracked;
                _pythonTracker.TrackerError -= OnTrackerError;
                _pythonTracker.StopAsync().Wait();
                _pythonTracker.Dispose();
            }
            _process.Stop();
            Session.IsActive = false;
        }
    }
}
