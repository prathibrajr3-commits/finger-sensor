namespace AirGestureAI.Services
{
    /// <summary>
    /// Specifies the severity level of a structured log entry.
    /// </summary>
    public enum LogLevel
    {
        /// <summary>
        /// Highly detailed diagnostic log messages.
        /// </summary>
        Trace,

        /// <summary>
        /// Debugging messages for internal troubleshooting.
        /// </summary>
        Debug,

        /// <summary>
        /// Informational messages highlighting application progression.
        /// </summary>
        Information,

        /// <summary>
        /// Warning messages indicating potential non-fatal anomalies.
        /// </summary>
        Warning,

        /// <summary>
        /// Error messages indicating operation failures.
        /// </summary>
        Error,

        /// <summary>
        /// Critical failures that require immediate system administrative attention.
        /// </summary>
        Critical
    }
}
