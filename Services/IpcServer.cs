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
    /// Receives and dispatches Named Pipe client requests asynchronously and concurrently
    /// without blocking connection threads.
    /// </summary>
    public sealed class IpcServer
    {
        private readonly string _pipeName;
        private readonly IpcRouter _router;
        private CancellationTokenSource? _cts;
        private Task? _listenTask;

        /// <summary>
        /// Initializes a new instance of <see cref="IpcServer"/>.
        /// </summary>
        public IpcServer(string pipeName, IpcRouter router)
        {
            _pipeName = pipeName;
            _router = router;
        }

        /// <summary>
        /// Starts the asynchronous named pipe server connection loop.
        /// </summary>
        public void Start()
        {
            _cts = new CancellationTokenSource();
            _listenTask = Task.Run(() => ListenLoopAsync(_cts.Token));
            Logger.Info($"IpcServer: Started listener task for pipe '{_pipeName}'.");
        }

        /// <summary>
        /// Stops the listener loop and cancels any active connections.
        /// </summary>
        public void Stop()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                try
                {
                    // Await the listener task completion with a short timeout to handle graceful shutdown
                    _listenTask?.Wait(500);
                }
                catch
                {
                    // Ignore background task termination issues
                }
                _cts.Dispose();
                _cts = null;
                Logger.Info($"IpcServer: Stopped listener task for pipe '{_pipeName}'.");
            }
        }

        private async Task ListenLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    // Create server stream with multi-instance support enabled
                    var server = new NamedPipeServerStream(
                        _pipeName,
                        PipeDirection.InOut,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    // Await incoming client connection asynchronously
                    await server.WaitForConnectionAsync(token).ConfigureAwait(false);

                    // Run the request processing asynchronously on the ThreadPool to keep listener loop responsive
                    _ = Task.Run(() => ProcessConnectionAsync(server, token), token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Error($"IpcServer: Loop connection error on pipe '{_pipeName}': {ex.Message}", ex);
                    try
                    {
                        await Task.Delay(100, token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }

        private async Task ProcessConnectionAsync(NamedPipeServerStream server, CancellationToken token)
        {
            using (server)
            {
                try
                {
                    using var reader = new StreamReader(server, new UTF8Encoding(false), leaveOpen: true);
                    using var writer = new StreamWriter(server, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };

                    var line = await reader.ReadLineAsync(token).ConfigureAwait(false);
                    if (line != null)
                    {
                        var req = IpcSerializer.Deserialize(line);

                        // Propagate incoming CorrelationId to the routing process
                        var responseText = await _router.RouteAsync(req, token).ConfigureAwait(false);

                        var responseMsg = new IpcMessage
                        {
                            Method = req.Method + "_Response",
                            Payload = responseText,
                            CorrelationId = req.CorrelationId
                        };

                        var serializedResponse = IpcSerializer.Serialize(responseMsg);
                        await writer.WriteLineAsync(serializedResponse.AsMemory(), token).ConfigureAwait(false);
                        await writer.FlushAsync().ConfigureAwait(false);

                        // Wait for the client to drain (read) the response before the server
                        // disposes and disconnects. WaitForPipeDrain blocks until all data is
                        // consumed by the client. We run it with a short timeout so a dead
                        // client cannot stall the server loop indefinitely.
                        using var drainCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                        try
                        {
                            await Task.Run(() => server.WaitForPipeDrain(), drainCts.Token)
                                      .ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            // Client didn't read in time — disconnect anyway
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Clean cancellation
                }
                catch (Exception ex)
                {
                    Logger.Error($"IpcServer: Error handling connection on pipe '{_pipeName}': {ex.Message}", ex);
                }
            }
        }
    }
}
