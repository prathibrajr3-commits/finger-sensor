using System;
using System.IO;
using System.Text;
using AirGestureAI.Services;

namespace AirGestureAI.Utilities
{
    /// <summary>
    /// Thread-safe logger that outputs to file, debug console, and triggers events for UI presentation.
    /// Bridges to <see cref="LoggingService"/> when registered.
    /// </summary>
    public static class Logger
    {
        private static readonly object LockObj = new object();
        private static string? _logFilePath;
        private static LoggingService? _loggingService;
        private static StaticLoggerBridge? _bridgeSubscriber;

        /// <summary>
        /// Occurs when a new log message is written. Useful for UI log controls.
        /// </summary>
        public static event Action<string>? LogWritten;

        /// <summary>
        /// Initializes the logging system, setting up log file location.
        /// </summary>
        public static void Initialize(string appDataPath)
        {
            lock (LockObj)
            {
                try
                {
                    if (!Directory.Exists(appDataPath))
                    {
                        Directory.CreateDirectory(appDataPath);
                    }
                    _logFilePath = Path.Combine(appDataPath, "airgesture_log.txt");

                    // Start fresh log or append with divider
                    File.AppendAllText(_logFilePath, $"\n--- Logging Session Started at {DateTime.Now} ---\n", Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to initialize logger file: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Integrates the static Logger with the modern enterprise <see cref="LoggingService"/>.
        /// </summary>
        /// <param name="loggingService">The dependency-injected logging service instance.</param>
        public static void Initialize(LoggingService loggingService)
        {
            lock (LockObj)
            {
                _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
                _bridgeSubscriber = new StaticLoggerBridge();
                _loggingService.AddSubscriber(_bridgeSubscriber);
            }
        }

        /// <summary>
        /// Logs an informational message.
        /// </summary>
        public static void Info(string message)
        {
            if (_loggingService != null)
            {
                _loggingService.Information(message, "LegacyLogger");
            }
            else
            {
                Log("INFO", message);
            }
        }

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        public static void Warn(string message)
        {
            if (_loggingService != null)
            {
                _loggingService.Warning(message, "LegacyLogger");
            }
            else
            {
                Log("WARN", message);
            }
        }

        /// <summary>
        /// Logs an error message.
        /// </summary>
        public static void Error(string message, Exception? ex = null)
        {
            if (_loggingService != null)
            {
                _loggingService.Error(message, ex, "LegacyLogger");
            }
            else
            {
                var msg = ex != null ? $"{message} | Exception: {ex.Message}\n{ex.StackTrace}" : message;
                Log("ERROR", msg);
            }
        }

        private static void Log(string level, string message)
        {
            var formatted = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";

            // Output to Debug console
            System.Diagnostics.Debug.WriteLine(formatted);

            // Write to file
            lock (LockObj)
            {
                if (_logFilePath != null)
                {
                    try
                    {
                        File.AppendAllText(_logFilePath, formatted + "\n", Encoding.UTF8);
                    }
                    catch
                    {
                        // Ignore log write errors to prevent crashing
                    }
                }
            }

            // Notify listeners (UI)
            LogWritten?.Invoke(formatted);
        }

        private sealed class StaticLoggerBridge : ILogSubscriber
        {
            public LogLevel MinimumLevel => LogLevel.Trace;

            public void OnLogEntry(LogEntry entry)
            {
                var formatted = $"[{entry.TimestampUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss.fff}] [{GetLegacyLevelString(entry.Level)}] {entry.Message}";
                if (!string.IsNullOrEmpty(entry.ExceptionInfo))
                {
                    formatted += $" | Exception: {entry.ExceptionInfo}";
                }
                LogWritten?.Invoke(formatted);
            }

            private static string GetLegacyLevelString(LogLevel level) => level switch
            {
                LogLevel.Trace => "TRACE",
                LogLevel.Debug => "DEBUG",
                LogLevel.Information => "INFO",
                LogLevel.Warning => "WARN",
                LogLevel.Error => "ERROR",
                LogLevel.Critical => "CRITICAL",
                _ => "INFO"
            };
        }
    }
}
