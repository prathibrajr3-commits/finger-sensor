using System;
using System.Threading.Tasks;
using AirGestureAI.Models;
using OpenCvSharp;

namespace AirGestureAI.HandTracking
{
    /// <summary>
    /// Event arguments containing the hand tracking result.
    /// </summary>
    public class HandTrackedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the hand tracking data.
        /// </summary>
        public HandData HandData { get; }

        /// <summary>
        /// Gets the capture timestamp of the frame that produced this hand data.
        /// </summary>
        public DateTime CaptureTimestamp { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="HandTrackedEventArgs"/> class.
        /// </summary>
        public HandTrackedEventArgs(HandData handData, DateTime captureTimestamp)
        {
            HandData = handData;
            CaptureTimestamp = captureTimestamp;
        }
    }

    /// <summary>
    /// Defines operations for hand landmark detection using MediaPipe.
    /// </summary>
    public interface IHandTracker : IDisposable
    {
        /// <summary>
        /// Occurs when a frame has been processed and hand data is available.
        /// </summary>
        event EventHandler<HandTrackedEventArgs>? HandTracked;

        /// <summary>
        /// Occurs if the tracking engine encounters an error.
        /// </summary>
        event EventHandler<string>? TrackerError;

        /// <summary>
        /// Gets a value indicating whether the tracking subprocess is active.
        /// </summary>
        bool IsTracking { get; }

        /// <summary>
        /// Starts the hand tracking engine (e.g. prepares the Python subprocess).
        /// </summary>
        Task StartAsync();

        /// <summary>
        /// Stops the hand tracking engine and releases resources.
        /// </summary>
        Task StopAsync();

        /// <summary>
        /// Submits an image frame to the hand tracker asynchronously.
        /// </summary>
        /// <param name="frame">The Mat image frame to process.</param>
        /// <param name="captureTimestamp">The timestamp when the frame was captured.</param>
        Task ProcessFrameAsync(Mat frame, DateTime captureTimestamp);
    }
}
