using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AirGestureAI.IPC;
using Xunit;

namespace AirGestureAI.Tests
{
    public sealed class AsyncIpcTests
    {
        private const string TestPipeName = "AirGestureAI_UnitTest_Pipe";

        [Fact]
        public async Task TestAsyncIpcRequestResponse()
        {
            // Arrange
            var router = new IpcRouter();
            router.RegisterAsync("AddNumbers", async (msg, ct) =>
            {
                await Task.Delay(10, ct);
                var parts = msg.Payload.Split(',');
                var sum = int.Parse(parts[0]) + int.Parse(parts[1]);
                return sum.ToString();
            });

            var server = new IpcServer(TestPipeName, router);
            server.Start();

            var client = new IpcClient(TestPipeName);

            try
            {
                // Act
                var request = new IpcMessage { Method = "AddNumbers", Payload = "5,7" };
                var response = await client.SendAsync(request);

                // Assert
                Assert.Equal("AddNumbers_Response", response.Method);
                Assert.Equal("12", response.Payload);
            }
            finally
            {
                server.Stop();
            }
        }

        [Fact]
        public async Task TestIpcRequestTimeout()
        {
            // Arrange
            var router = new IpcRouter();
            router.RegisterAsync("SlowMethod", async (msg, ct) =>
            {
                await Task.Delay(2000, ct); // Very slow processing
                return "Done";
            });

            var pipeName = TestPipeName + "_Timeout";
            var server = new IpcServer(pipeName, router);
            server.Start();

            var client = new IpcClient(pipeName)
            {
                RequestTimeoutMs = 100 // Enforce strict 100ms timeout
            };

            try
            {
                // Act
                var request = new IpcMessage { Method = "SlowMethod", Payload = "" };
                var response = await client.SendAsync(request);

                // Assert
                Assert.Equal("Error", response.Method);
                Assert.Contains("timeout", response.Payload, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                server.Stop();
            }
        }

        [Fact]
        public async Task TestIpcCancellation()
        {
            // Arrange
            var router = new IpcRouter();
            router.RegisterAsync("Ping", (msg, ct) => Task.FromResult("PONG"));

            var pipeName = TestPipeName + "_Cancellation";
            var server = new IpcServer(pipeName, router);
            server.Start();

            var client = new IpcClient(pipeName);
            using var cts = new CancellationTokenSource();
            cts.Cancel(); // Pre-cancel

            try
            {
                // Act
                var request = new IpcMessage { Method = "Ping", Payload = "" };
                var response = await client.SendAsync(request, cts.Token);

                // Assert
                Assert.Equal("Error", response.Method);
                Assert.Contains("failed", response.Payload, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                server.Stop();
            }
        }

        [Fact]
        public async Task TestUnknownMethodReturnsError()
        {
            // Arrange
            var router = new IpcRouter();
            var pipeName = TestPipeName + "_Unknown";
            var server = new IpcServer(pipeName, router);
            server.Start();

            var client = new IpcClient(pipeName);

            try
            {
                // Act
                var request = new IpcMessage { Method = "NonExistentMethod", Payload = "" };
                var response = await client.SendAsync(request);

                // Assert
                Assert.Equal("NonExistentMethod_Response", response.Method);
                Assert.Contains("Unknown method", response.Payload);
            }
            finally
            {
                server.Stop();
            }
        }

        [Fact]
        public async Task TestCorrelationIdPropagation()
        {
            // Arrange
            var router = new IpcRouter();
            router.RegisterAsync("Echo", (msg, ct) => Task.FromResult(msg.Payload));

            var pipeName = TestPipeName + "_Correlation";
            var server = new IpcServer(pipeName, router);
            server.Start();

            var client = new IpcClient(pipeName);
            var customCorrelationId = "Trace-Id-9999-XYZ";

            try
            {
                // Act
                var request = new IpcMessage { Method = "Echo", Payload = "Hello", CorrelationId = customCorrelationId };
                var response = await client.SendAsync(request);

                // Assert
                Assert.Equal(customCorrelationId, response.CorrelationId);
            }
            finally
            {
                server.Stop();
            }
        }

        [Fact]
        public async Task TestConcurrentIpcRequests()
        {
            // Arrange
            var router = new IpcRouter();
            router.RegisterAsync("DelayedEcho", async (msg, ct) =>
            {
                await Task.Delay(50, ct);
                return msg.Payload;
            });

            var pipeName = TestPipeName + "_Concurrent";
            var server = new IpcServer(pipeName, router);
            server.Start();

            var client = new IpcClient(pipeName);
            int requestCount = 10;

            try
            {
                // Act
                var tasks = Enumerable.Range(0, requestCount).Select(i =>
                    client.SendAsync(new IpcMessage { Method = "DelayedEcho", Payload = $"Item-{i}" })
                ).ToArray();

                var responses = await Task.WhenAll(tasks);

                // Assert
                Assert.Equal(requestCount, responses.Length);
                for (int i = 0; i < requestCount; i++)
                {
                    Assert.Equal($"DelayedEcho_Response", responses[i].Method);
                    Assert.Equal($"Item-{i}", responses[i].Payload);
                }
            }
            finally
            {
                server.Stop();
            }
        }

        [Fact]
        public async Task TestIpcRouterMiddlewarePipeline()
        {
            // Arrange
            var router = new IpcRouter();

            // Register handler
            router.RegisterAsync("Uppercase", (msg, ct) => Task.FromResult(msg.Payload.ToUpperInvariant()));

            // Register interceptor middleware
            router.Use(async (msg, ct, next) =>
            {
                // Pre-processing: append suffix to payload
                msg.Payload += "_intercepted";
                var result = await next(msg, ct);
                // Post-processing: prepend suffix
                return "interceptor_" + result;
            });

            var pipeName = TestPipeName + "_Middleware";
            var server = new IpcServer(pipeName, router);
            server.Start();

            var client = new IpcClient(pipeName);

            try
            {
                // Act
                var request = new IpcMessage { Method = "Uppercase", Payload = "hello" };
                var response = await client.SendAsync(request);

                // Assert
                // Flow: hello -> hello_intercepted -> HELLO_INTERCEPTED -> interceptor_HELLO_INTERCEPTED
                Assert.Equal("interceptor_HELLO_INTERCEPTED", response.Payload);
            }
            finally
            {
                server.Stop();
            }
        }
    }
}
