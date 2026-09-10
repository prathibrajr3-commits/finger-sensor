using System;
using System.Windows;
using System.Windows.Threading;

namespace AirGestureAI.Services
{
    /// <summary>Specifies the severity of a transient system notification.</summary>
    public enum NotificationType
    {
        /// <summary>Confirmation of a gesture trigger event.</summary>
        GestureTrigger,
        /// <summary>Informational message about system status changes.</summary>
        Info,
        /// <summary>Security audit or policy alert.</summary>
        Warning,
        /// <summary>Service health error or hardware disconnection warning.</summary>
        Error,
    }

    /// <summary>
    /// Service that displays transient overlay alerts in the bottom-right corner of the
    /// desktop screen. Supports custom timeout and auto-dismiss.
    /// Thread-safe: can be called from background threads (safely posts to WPF dispatcher).
    /// </summary>
    public sealed class NotificationService
    {
        /// <summary>
        /// Displays a notification popup in the corner of the primary screen.
        /// </summary>
        /// <param name="title">The heading text.</param>
        /// <param name="message">The detail description text.</param>
        /// <param name="type">The notification type / icon indicator.</param>
        /// <param name="durationMs">Timeout duration before auto-dismiss (default: 3000ms).</param>
        public void Show(string title, string message, NotificationType type = NotificationType.Info, int durationMs = 3000)
        {
            var app = Application.Current;
            if (app is null) return;

            app.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
            {
                try
                {
                    var win = new Views.NotificationWindow(title, message, type, durationMs);
                    win.Show();
                }
                catch (Exception ex)
                {
                    Utilities.Logger.Warn($"NotificationService.Show failed: {ex.Message}");
                }
            }));
        }
    }
}
