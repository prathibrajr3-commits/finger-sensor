using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AirGestureAI.Security;
using AirGestureAI.Utilities;

namespace AirGestureAI.IPC
{
    /// <summary>
    /// Routes incoming Named Pipe payloads to asynchronous or synchronous handlers,
    /// supporting pipeline middlewares, token validation, payload injection checks,
    /// security audit logging, and diagnostics.
    /// </summary>
    public sealed class IpcRouter
    {
        private readonly ConcurrentDictionary<string, Func<IpcMessage, CancellationToken, Task<string>>> _handlers =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly List<Func<IpcMessage, CancellationToken, Func<IpcMessage, CancellationToken, Task<string>>, Task<string>>> _middlewares = new();

        /// <summary>
        /// Optional callback invoked on every IPC authentication failure.
        /// Wire to <see cref="AirGestureAI.Security.RuntimeProtectionService.RecordIpcAuthFailure"/> in the DI root.
        /// </summary>
        public Action? OnAuthFailure { get; set; }

        /// <summary>
        /// Optional callback invoked on every successful IPC authentication.
        /// Wire to <see cref="AirGestureAI.Security.RuntimeProtectionService.RecordIpcAuthSuccess"/> in the DI root.
        /// </summary>
        public Action? OnAuthSuccess { get; set; }

        // ── Registration ────────────────────────────────────────────────────────

        /// <summary>
        /// Registers a synchronous backward-compatible handler.
        /// </summary>
        public void Register(string method, Func<IpcMessage, string> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            RegisterAsync(method, (msg, _) => Task.FromResult(handler(msg)));
        }

        /// <summary>
        /// Registers an asynchronous handler.
        /// </summary>
        public void RegisterAsync(string method, Func<IpcMessage, CancellationToken, Task<string>> handler)
        {
            if (string.IsNullOrWhiteSpace(method)) throw new ArgumentException("Method name cannot be null or empty.", nameof(method));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            _handlers[method] = handler;
            Logger.Info($"IpcRouter: Registered handler for method '{method}'.");
        }

        /// <summary>
        /// Adds a middleware delegate to the pipeline.
        /// </summary>
        public void Use(Func<IpcMessage, CancellationToken, Func<IpcMessage, CancellationToken, Task<string>>, Task<string>> middleware)
        {
            if (middleware == null) throw new ArgumentNullException(nameof(middleware));
            _middlewares.Add(middleware);
        }

        /// <summary>
        /// Installs the built-in security middleware at the head of the pipeline.
        /// Enforces: token validation, payload injection checking, auth failure tracking,
        /// and per-request security audit logging.
        /// </summary>
        public void UseSecurityMiddleware()
        {
            // Insert at position 0 so security always runs first
            _middlewares.Insert(0, async (msg, ct, next) =>
            {
                // Step 1: Validate IPC authentication token
                if (!IpcSecurity.ValidateToken(msg.Token))
                {
                    Logger.Warn($"IpcRouter [SecurityMiddleware]: Unauthorized token for '{msg.Method}'. CorrelationId={msg.CorrelationId}");
                    OnAuthFailure?.Invoke();
                    return "ERROR: Unauthorized. Invalid or missing IPC security token.";
                }

                OnAuthSuccess?.Invoke();

                // Step 2: Validate payload for injection characters
                var payloadCheck = InputSecurity.Validate(msg.Payload);
                if (!payloadCheck.IsValid)
                {
                    Logger.Warn($"IpcRouter [SecurityMiddleware]: Injection rejected for '{msg.Method}'. Token='{payloadCheck.InvalidToken}'. CorrelationId={msg.CorrelationId}");
                    return $"ERROR: Payload rejected — {payloadCheck.ErrorMessage}";
                }

                // Step 3: Execute remaining pipeline
                var result = await next(msg, ct).ConfigureAwait(false);

                // Step 4: Audit-log the outcome
                Logger.Info($"IpcRouter [SecurityMiddleware]: Method='{msg.Method}' | CorrelationId={msg.CorrelationId} | AUDIT=OK");
                return result;
            });
        }

        // ── Routing ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Routes the request envelope asynchronously, executing middlewares and the targeted handler.
        /// </summary>
        public async Task<string> RouteAsync(IpcMessage request, CancellationToken cancellationToken)
        {
            if (request == null) return "ERROR: Request is null.";

            Logger.Info($"IpcRouter: Routing '{request.Method}' (CorrelationId: {request.CorrelationId})");

            var sw = Stopwatch.StartNew();

            try
            {
                int middlewareIndex = 0;

                Func<IpcMessage, CancellationToken, Task<string>>? next = null;
                next = async (msg, ct) =>
                {
                    ct.ThrowIfCancellationRequested();

                    if (middlewareIndex < _middlewares.Count)
                    {
                        var middleware = _middlewares[middlewareIndex++];
                        return await middleware(msg, ct, next!).ConfigureAwait(false);
                    }

                    if (_handlers.TryGetValue(msg.Method, out var handler))
                        return await handler(msg, ct).ConfigureAwait(false);

                    return $"ERROR: Unknown method '{msg.Method}'";
                };

                var response = await next(request, cancellationToken).ConfigureAwait(false);
                sw.Stop();
                Logger.Info($"IpcRouter: Processed '{request.Method}' in {sw.ElapsedMilliseconds}ms.");
                return response;
            }
            catch (OperationCanceledException)
            {
                Logger.Warn($"IpcRouter: Cancelled for '{request.Method}' (CorrelationId: {request.CorrelationId}).");
                throw;
            }
            catch (Exception ex)
            {
                sw.Stop();
                Logger.Error($"IpcRouter: Error in '{request.Method}'", ex);
                return $"ERROR: Exception executing method: {ex.Message}";
            }
        }

        /// <summary>
        /// Routes the request and blocks synchronously (backward-compatible wrapper).
        /// </summary>
        public string Route(IpcMessage request)
        {
            return Task.Run(() => RouteAsync(request, CancellationToken.None)).GetAwaiter().GetResult();
        }
    }
}
