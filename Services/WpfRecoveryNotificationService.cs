using System;
using System.Threading.Tasks;
using System.Windows;
using AirGestureAI.Views;

namespace AirGestureAI.Services
{
    /// <summary>
    /// WPF-native implementation of <see cref="IRecoveryNotificationService"/>.
    /// Shows a transient toast notification on the UI dispatcher thread when
    /// an abnormal shutdown is detected during startup recovery.
    /// </summary>
    public sealed class WpfRecoveryNotificationService : IRecoveryNotificationService
    {
        private readonly LoggingService _logging;

        /// <summary>
        /// Initialises a new <see cref="WpfRecoveryNotificationService"/>.
        /// </summary>
        /// <param name="logging">Structured logging service.</param>
        public WpfRecoveryNotificationService(LoggingService logging)
        {
            _logging = logging ?? throw new ArgumentNullException(nameof(logging));
        }

        /// <inheritdoc />
        public Task ShowRecoveryNotificationAsync(RecoveryReport report, SessionValidationReport validation)
        {
            if (Application.Current == null)
                return Task.CompletedTask;

            var source = report.RecoverySource;
            var severity = validation.OverallSeverity;
            var safeMode = report.SafeModeActive;

            // Build user-friendly message
            var title = safeMode
                ? "⚠ Safe Mode Active"
                : "↩ Session Restored";

            var message = safeMode
                ? "AirGesture AI started in Safe Mode after repeated failures. Workflows are paused."
                : $"Your previous session was recovered from {source}. Severity: {severity}.";

            _logging.Information(
                $"[Recovery] Showing user notification. Source={source}, Severity={severity}, SafeMode={safeMode}.",
                "WpfRecoveryNotificationService");

            // Show on UI thread; fire-and-forget — we don't block on UI
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    var type = safeMode ? NotificationType.Warning : NotificationType.Info;
                    var win = new NotificationWindow(title, message, type, durationMs: 8000);
                    win.Show();
                }
                catch (Exception ex)
                {
                    _logging.Warning(
                        $"[Recovery] Failed to show recovery notification window: {ex.Message}",
                        "WpfRecoveryNotificationService");
                }
            }));

            return Task.CompletedTask;
        }
    }
}
