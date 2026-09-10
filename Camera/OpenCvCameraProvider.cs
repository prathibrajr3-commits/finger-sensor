using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Threading;
using OpenCvSharp;

namespace AirGestureAI.Camera
{
    /// <summary>
    /// OpenCvSharp implementation of ICameraProvider that manages camera capture on a background thread.
    /// </summary>
    public class OpenCvCameraProvider : ICameraProvider
    {
        private VideoCapture? _videoCapture;
        private Thread? _captureThread;
        private bool _isRunning;
        private readonly object _lock = new object();
        private int _consecutiveErrorCount;
        private const int MaxConsecutiveErrors = 5;

        /// <summary>
        /// Occurs when a new frame is captured from the camera.
        /// </summary>
        public event EventHandler<FrameEventArgs>? FrameCaptured;

        /// <summary>
        /// Occurs if the camera stream encounters an error or disconnects.
        /// </summary>
        public event EventHandler<string>? CameraError;

        /// <summary>
        /// Gets a value indicating whether the camera is currently capturing frames.
        /// </summary>
        public bool IsRunning
        {
            get
            {
                lock (_lock)
                {
                    return _isRunning;
                }
            }
            private set
            {
                lock (_lock)
                {
                    _isRunning = value;
                }
            }
        }

        /// <summary>
        /// Discovers all available webcams on the system using WMI for friendly names, and verifies indices with OpenCV.
        /// </summary>
        /// <returns>A list of CameraInfo representing available webcams.</returns>
        public List<CameraInfo> GetAvailableCameras()
        {
            var cameras = new List<CameraInfo>();
            var friendlyNames = new List<string>();

            // 1. Get friendly names using WMI (Windows Management Instrumentation)
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE (PNPClass = 'Image' OR PNPClass = 'Camera')"))
                {
                    foreach (var device in searcher.Get())
                    {
                        var name = device["Caption"]?.ToString();
                        if (!string.IsNullOrEmpty(name))
                        {
                            friendlyNames.Add(name);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Utilities.Logger.Warn($"WMI camera query failed (this is expected if WMI is restricted): {ex.Message}");
            }

            // 2. Validate indices using OpenCV
            // We scan indices 0 to 5. If we can successfully open a camera, it is added to the list.
            for (int index = 0; index < 6; index++)
            {
                try
                {
                    using (var cap = new VideoCapture(index, VideoCaptureAPIs.DSHOW))
                    {
                        if (cap.IsOpened())
                        {
                            string friendlyName = friendlyNames.Count > index ? friendlyNames[index] : $"Webcam {index}";
                            cameras.Add(new CameraInfo
                            {
                                Index = index,
                                Name = friendlyName
                            });
                            cap.Release();
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Ignore errors during discovery scanning
                    Debug.WriteLine($"Error scanning camera index {index}: {ex.Message}");
                }
            }

            // Fallback: If no cameras were successfully validated but we found names, add index 0 as fallback
            if (cameras.Count == 0 && friendlyNames.Count > 0)
            {
                cameras.Add(new CameraInfo { Index = 0, Name = friendlyNames[0] });
            }

            Utilities.Logger.Info($"Discovered {cameras.Count} cameras.");
            return cameras;
        }

        /// <summary>
        /// Starts capturing frames from the specified camera index.
        /// </summary>
        /// <param name="cameraIndex">The index of the camera to open.</param>
        public void Start(int cameraIndex)
        {
            lock (_lock)
            {
                if (_isRunning)
                {
                    Utilities.Logger.Warn("Start called while camera is already running. Restarting camera...");
                    Stop();
                }

                Utilities.Logger.Info($"Starting camera index {cameraIndex}...");
                _isRunning = true;
                _consecutiveErrorCount = 0;

                _captureThread = new Thread(() => CaptureLoop(cameraIndex))
                {
                    IsBackground = true,
                    Name = $"CameraCaptureThread_Index{cameraIndex}"
                };
                _captureThread.Start();
            }
        }

        /// <summary>
        /// Stops capturing frames and releases camera resources.
        /// </summary>
        public void Stop()
        {
            lock (_lock)
            {
                if (!_isRunning) return;
                
                Utilities.Logger.Info("Stopping camera capture thread...");
                _isRunning = false;
            }

            // Join thread to ensure clean shutdown of native resources
            if (_captureThread != null && _captureThread.IsAlive)
            {
                if (!_captureThread.Join(TimeSpan.FromSeconds(3)))
                {
                    Utilities.Logger.Warn("Camera capture thread failed to stop within timeout.");
                }
            }

            _captureThread = null;
            Utilities.Logger.Info("Camera capture thread stopped and joined.");
        }

        private void CaptureLoop(int cameraIndex)
        {
            try
            {
                // Open VideoCapture with DirectShow API backend (highly recommended on Windows for speed/compliance)
                _videoCapture = new VideoCapture(cameraIndex, VideoCaptureAPIs.DSHOW);
                
                // Force target resolution of 640x480 for consistent tracking speeds
                _videoCapture.Set(VideoCaptureProperties.FrameWidth, 640);
                _videoCapture.Set(VideoCaptureProperties.FrameHeight, 480);

                if (!_videoCapture.IsOpened())
                {
                    throw new Exception($"Could not open camera device at index {cameraIndex}.");
                }

                double actualWidth = _videoCapture.Get(VideoCaptureProperties.FrameWidth);
                double actualHeight = _videoCapture.Get(VideoCaptureProperties.FrameHeight);
                Utilities.Logger.Info($"Camera {cameraIndex} opened. Resolution: {actualWidth}x{actualHeight}");

                // Keep track of frame timing
                var stopwatch = new Stopwatch();

                while (IsRunning)
                {
                    stopwatch.Restart();

                    var frame = new Mat();
                    try
                    {
                        // Blocking read
                        bool success = _videoCapture.Read(frame);
                        if (!success || frame.Empty())
                        {
                            frame.Dispose();
                            _consecutiveErrorCount++;
                            Utilities.Logger.Warn($"Camera read failure (count: {_consecutiveErrorCount})");

                            if (_consecutiveErrorCount >= MaxConsecutiveErrors)
                            {
                                throw new Exception("Camera disconnected or stopped sending frames.");
                            }

                            Thread.Sleep(33); // Small pause before retry
                            continue;
                        }

                        _consecutiveErrorCount = 0; // Reset error count on successful read

                        // Publish frame to subscribers
                        // Note: subscribers MUST clone the frame if they intend to process it asynchronously
                        FrameCaptured?.Invoke(this, new FrameEventArgs(frame, DateTime.UtcNow));

                        // Dispose local frame handle after event completes
                        frame.Dispose();

                        // Target approximately 30 FPS. 30 FPS = 33.3ms per frame.
                        // Calculate elapsed time and sleep for remaining duration.
                        long elapsedMs = stopwatch.ElapsedMilliseconds;
                        int sleepMs = (int)(33.3 - elapsedMs);
                        if (sleepMs > 0)
                        {
                            Thread.Sleep(sleepMs);
                        }
                    }
                    catch (Exception)
                    {
                        frame.Dispose();
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                Utilities.Logger.Error($"Fatal error in camera capture thread: {ex.Message}", ex);
                CameraError?.Invoke(this, ex.Message);
                IsRunning = false;
            }
            finally
            {
                CleanupResources();
            }
        }

        private void CleanupResources()
        {
            lock (_lock)
            {
                if (_videoCapture != null)
                {
                    try
                    {
                        _videoCapture.Release();
                        _videoCapture.Dispose();
                        Utilities.Logger.Info("Released OpenCV VideoCapture resources.");
                    }
                    catch (Exception ex)
                    {
                        Utilities.Logger.Error("Error disposing VideoCapture", ex);
                    }
                    _videoCapture = null;
                }
            }
        }

        /// <summary>
        /// Disposes the camera provider.
        /// </summary>
        public void Dispose()
        {
            Stop();
        }
    }
}
