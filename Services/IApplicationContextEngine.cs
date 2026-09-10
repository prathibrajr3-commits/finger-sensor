using System;
using AirGestureAI.Models;

namespace AirGestureAI.Services
{
    /// <summary>
    /// Contract for the Application Context Engine.
    /// Monitors the foreground window and publishes context change events.
    /// This engine never executes actions — it only reports context.
    /// </summary>
    public interface IApplicationContextEngine : IDisposable
    {
        /// <summary>
        /// Raised when the active foreground application or window title changes.
        /// </summary>
        event EventHandler<ApplicationChangedEventArgs>? ApplicationChanged;

        /// <summary>Gets the most recently captured application context.</summary>
        ApplicationContext CurrentContext { get; }

        /// <summary>Starts the background polling loop.</summary>
        void Start();

        /// <summary>Stops the background polling loop cleanly.</summary>
        void Stop();
    }
}
