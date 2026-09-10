using System;
using System.Windows;
using AirGestureAI.Models;

namespace AirGestureAI.Cursor
{
    /// <summary>
    /// Defines operations for virtual cursor coordinate processing, smoothing, state transition, and hover selection.
    /// </summary>
    public interface ICursorEngine : IDisposable
    {
        /// <summary>
        /// Occurs when the virtual cursor position changes.
        /// </summary>
        event EventHandler<Point>? CursorMoved;

        /// <summary>
        /// Occurs when the virtual cursor state changes (e.g. Ready, Hovering, Selected, HandLost).
        /// </summary>
        event EventHandler<CursorState>? StateChanged;

        /// <summary>
        /// Gets the current cursor position on the screen.
        /// </summary>
        Point CurrentPosition { get; }

        /// <summary>
        /// Gets the current state of the virtual cursor.
        /// </summary>
        CursorState CurrentState { get; }

        /// <summary>
        /// Updates the cursor engine with the latest hand tracking data.
        /// </summary>
        /// <param name="handData">The detected hand tracking landmarks.</param>
        void Update(HandData handData);

        /// <summary>
        /// Shows the virtual cursor overlay window on screen.
        /// </summary>
        void Show();

        /// <summary>
        /// Hides the virtual cursor overlay window.
        /// </summary>
        void Hide();
    }
}
