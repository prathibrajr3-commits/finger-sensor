using System;

namespace AirGestureAI.Models
{
    /// <summary>
    /// Immutable snapshot of the currently active foreground application context.
    /// </summary>
    public sealed class ApplicationContext
    {
        /// <summary>Gets the detected application type.</summary>
        public ApplicationType AppType { get; }

        /// <summary>Gets the process name (e.g. "chrome", "vlc").</summary>
        public string ProcessName { get; }

        /// <summary>Gets the full executable path, or empty if access was denied.</summary>
        public string ExecutablePath { get; }

        /// <summary>Gets the foreground window title text.</summary>
        public string WindowTitle { get; }

        /// <summary>Gets the native window handle (HWND).</summary>
        public IntPtr WindowHandle { get; }

        /// <summary>Gets the OS process ID.</summary>
        public int ProcessId { get; }

        /// <summary>Gets the UTC time this snapshot was captured.</summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Initializes an <see cref="ApplicationContext"/> snapshot.
        /// </summary>
        public ApplicationContext(
            ApplicationType appType,
            string processName,
            string executablePath,
            string windowTitle,
            IntPtr windowHandle,
            int processId)
        {
            AppType        = appType;
            ProcessName    = processName;
            ExecutablePath = executablePath;
            WindowTitle    = windowTitle;
            WindowHandle   = windowHandle;
            ProcessId      = processId;
            Timestamp      = DateTime.UtcNow;
        }

        /// <summary>Returns a human-readable summary of this context.</summary>
        public override string ToString() =>
            $"[AppContext {AppType} | PID={ProcessId} | \"{WindowTitle}\"]";

        /// <summary>A default empty context used before the first detection cycle.</summary>
        public static readonly ApplicationContext Empty = new ApplicationContext(
            ApplicationType.Unknown, string.Empty, string.Empty,
            string.Empty, IntPtr.Zero, 0);
    }
}
