namespace AirGestureAI.Services
{
    /// <summary>
    /// Defines the contract for an active subscriber to live application log events.
    /// </summary>
    public interface ILogSubscriber
    {
        /// <summary>
        /// Gets the minimum severity level that this subscriber receives.
        /// </summary>
        LogLevel MinimumLevel { get; }

        /// <summary>
        /// Invoked when a new log entry is processed and passes the minimum level check.
        /// </summary>
        /// <param name="entry">The structured log entry.</param>
        void OnLogEntry(LogEntry entry);
    }
}
