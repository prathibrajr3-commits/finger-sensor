using System;
using System.Text.Json.Serialization;

namespace AirGestureAI.Services
{
    /// <summary>
    /// Represents a structured log entry within the AirGesture AI application.
    /// </summary>
    public sealed record LogEntry
    {
        /// <summary>
        /// Gets the UTC timestamp when the log entry was created.
        /// </summary>
        public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Gets the severity level of this log entry.
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public LogLevel Level { get; init; } = LogLevel.Information;

        /// <summary>
        /// Gets the subsystem or class associated with the log entry.
        /// </summary>
        public string Subsystem { get; init; } = string.Empty;

        /// <summary>
        /// Gets the correlation identifier for tracing request/response loops.
        /// </summary>
        public string CorrelationId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the log message text.
        /// </summary>
        public string Message { get; init; } = string.Empty;

        /// <summary>
        /// Gets the serialized exception details, if any exception occurred.
        /// </summary>
        public string? ExceptionInfo { get; init; }
    }
}
