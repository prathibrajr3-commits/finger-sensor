namespace AirGestureAI.IPC
{
    /// <summary>
    /// Represents the structured response payload returned by an asynchronous IPC handler.
    /// </summary>
    public sealed class IpcResponse
    {
        /// <summary>Gets or sets whether the request was processed successfully.</summary>
        public bool Success { get; set; } = true;

        /// <summary>Gets or sets the response payload string.</summary>
        public string Payload { get; set; } = string.Empty;

        /// <summary>Gets or sets the error message, if processing failed.</summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>Gets or sets the correlation identifier copied from the request.</summary>
        public string CorrelationId { get; set; } = string.Empty;
    }
}
