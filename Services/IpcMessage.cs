namespace AirGestureAI.IPC
{
    /// <summary>
    /// Models request/response messages sent across IPC boundaries.
    /// </summary>
    public class IpcMessage
    {
        /// <summary>Gets or sets the message type/command.</summary>
        public string Method { get; set; } = string.Empty;

        /// <summary>Gets or sets the payload body.</summary>
        public string Payload { get; set; } = string.Empty;

        /// <summary>Gets or sets the authentication token.</summary>
        public string Token { get; set; } = string.Empty;

        /// <summary>Gets or sets the correlation identifier for request/response tracing.</summary>
        public string CorrelationId { get; set; } = string.Empty;
    }
}
