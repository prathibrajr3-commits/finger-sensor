using System;
using System.Collections.Generic;
using OpenCvSharp;

namespace AirGestureAI.Camera
{
    /// <summary>
    /// Event arguments containing the captured camera frame.
    /// </summary>
    public class FrameEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the captured camera frame.
        /// </summary>
        public Mat Frame { get; }

        /// <summary>
        /// Gets the timestamp when the frame was captured.
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FrameEventArgs"/> class.
        /// </summary>
        public FrameEventArgs(Mat frame)
        {
            Frame = frame;
            Timestamp = DateTime.UtcNow;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FrameEventArgs"/> class with a specific timestamp.
        /// </summary>
        public FrameEventArgs(Mat frame, DateTime timestamp)
        {
            Frame = frame;
            Timestamp = timestamp;
        }
    }

    /// <summary>
    /// Holds information about a discovered camera device.
    /// </summary>
    public class CameraInfo
    {
        /// <summary>
        /// Gets or sets the camera index.
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Gets or sets the friendly name of the camera.
        /// </summary>
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// Defines operations for discovering, controlling, and reading frames from a webcam.
    /// </summary>
    public interface ICameraProvider : IDisposable
    {
        /// <summary>
        /// Occurs when a new frame is captured from the camera.
        /// </summary>
        event EventHandler<FrameEventArgs>? FrameCaptured;

        /// <summary>
        /// Occurs if the camera stream encounters an error or disconnects.
        /// </summary>
        event EventHandler<string>? CameraError;

        /// <summary>
        /// Gets a value indicating whether the camera is currently capturing frames.
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// Discovers all available webcams on the system.
        /// </summary>
        /// <returns>A list of CameraInfo representing available webcams.</returns>
        List<CameraInfo> GetAvailableCameras();

        /// <summary>
        /// Starts capturing frames from the specified camera index.
        /// </summary>
        /// <param name="cameraIndex">The index of the camera to open.</param>
        void Start(int cameraIndex);

        /// <summary>
        /// Stops capturing frames and releases camera resources.
        /// </summary>
        void Stop();
    }
}
