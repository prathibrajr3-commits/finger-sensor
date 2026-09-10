using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AirGestureAI.Utilities;

namespace AirGestureAI.IPC
{
    /// <summary>
    /// Sends messages over Named Pipe channels asynchronously with timeouts, cancellation,
    /// and correlation tracking.
    /// </summary>
    public sealed class IpcClient
    {
        private readonly string _pipeName;
        private int _requestTimeoutMs = 5000; // Default: 5 seconds

        /// <summary>
        /// Initializes a new instance of <see cref="IpcClient"/>.
        /// </summary>
        public IpcClient(string pipeName)
        {
            _pipeName = pipeName;
        }

        /// <summary>
        /// Gets or sets the request processing timeout in milliseconds.
        /// </summary>
        public int RequestTimeoutMs
        {
            get => _requestTimeoutMs;
            set => _requestTimeoutMs = value > 0 ? value : throw new ArgumentException("Timeout must be positive.");
        }

        /// <summary>
        /// Sends an IPC message asynchronously and awaits a response.
        /// </summary>
        public async Task<IpcMessage> SendAsync(IpcMessage msg, CancellationToken cancellationToken = default)
        {
            if (msg == null) throw new ArgumentNullException(nameof(msg));

            msg.Token = "SECURE_AIRGESTURE_TOKEN_v4";
            if (string.IsNullOrWhiteSpace(msg.CorrelationId))
            {
                msg.CorrelationId = Guid.NewGuid().ToString("N");
            }

            Logger.Info($"IpcClient: Initiating connection to pipe '{_pipeName}' for method '{msg.Method}' (CorrelationId: {msg.CorrelationId})");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_requestTimeoutMs);

            try
            {
                using var pipe = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

                // Connect asynchronously (timeout is enforced by linked cancellation token)
                await pipe.ConnectAsync(cts.Token).ConfigureAwait(false);

                var raw = IpcSerializer.Serialize(msg);

                using (var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true })
                {
                    await writer.WriteLineAsync(raw.AsMemory(), cts.Token).ConfigureAwait(false);
                }

                using var reader = new StreamReader(pipe, new UTF8Encoding(false), leaveOpen: true);
                var line = await reader.ReadLineAsync(cts.Token).ConfigureAwait(false);

                if (line != null)
                {
                    var response = IpcSerializer.Deserialize(line);
                    Logger.Info($"IpcClient: Received response for method '{msg.Method}' (CorrelationId: {msg.CorrelationId})");
                    return response;
                }

                return new IpcMessage { Method = "Error", Payload = "Null response received from server." };
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                Logger.Error($"IpcClient: Timeout of {_requestTimeoutMs}ms exceeded waiting for response to method '{msg.Method}' (CorrelationId: {msg.CorrelationId})");
                return new IpcMessage { Method = "Error", Payload = $"IPC Send failed: Request timeout of {_requestTimeoutMs}ms exceeded." };
            }
            catch (Exception ex)
            {
                Logger.Error($"IpcClient: Exception sending message for method '{msg.Method}' (CorrelationId: {msg.CorrelationId})", ex);
                return new IpcMessage { Method = "Error", Payload = $"IPC Send failed: {ex.Message}" };
            }
        }
    }
}
