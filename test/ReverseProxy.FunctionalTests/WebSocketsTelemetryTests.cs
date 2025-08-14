// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Yarp.ReverseProxy.Common;
using Yarp.Telemetry.Consumption;
using Yarp.Tests.Common;

namespace Yarp.ReverseProxy;

public class WebSocketsTelemetryTests
{
    private readonly ITestOutputHelper _output;

    public WebSocketsTelemetryTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task NoWebSocketsUpgrade_NoTelemetryWritten()
    {
        var telemetry = await TestAsync(
            async uri =>
            {
                using var client = new HttpClient();
                await client.GetStringAsync(uri);
            },
            (context, webSocket) => throw new InvalidOperationException("Shouldn't be reached"));

        Assert.Null(telemetry);
    }

    [Theory]
    [InlineData(0, 0, 42)]
    [InlineData(0, 1, 42)]
    [InlineData(1, 0, 42)]
    [InlineData(23, 29, 0)]
    [InlineData(17, 19, 1)]
    [InlineData(11, 13, 100)]
    [InlineData(5, 7, 1_000)]
    [InlineData(2, 3, 100_000)]
    public async Task MessagesExchanged_CorrectNumberReported(int read, int written, int messageSize)
    {
        var telemetry = await TestAsync(
            async uri =>
            {
                using var client = new ClientWebSocket();
                await client.ConnectAsync(uri, CancellationToken.None);
                var webSocket = new WebSocketAdapter(client);

                await Task.WhenAll(
                    SendMessagesAndCloseAsync(webSocket, read, messageSize),
                    ReceiveAllMessagesAsync(webSocket));
            },
            async (context, webSocket) =>
            {
                await Task.WhenAll(
                    SendMessagesAndCloseAsync(webSocket, written, messageSize),
                    ReceiveAllMessagesAsync(webSocket));
            },
            new TestTimeProvider(new TimeSpan(42)));

        Assert.NotNull(telemetry);
        Assert.Equal(42, telemetry!.EstablishedTime.Ticks);
        Assert.Contains(telemetry.CloseReason, new[] { WebSocketCloseReason.ClientGracefulClose, WebSocketCloseReason.ServerGracefulClose });
        Assert.Equal(read, telemetry!.MessagesRead);
        Assert.Equal(written, telemetry.MessagesWritten);
    }

    [Fact]
    public async Task Http2WebSocketsWork()
    {
        var read = 11;
        var written = 13;
        var messageSize = 100;
        var telemetry = await TestAsync(
            async uri =>
            {
                using var invoker = CreateInvoker();
                using var client = new ClientWebSocket();
                client.Options.HttpVersion = HttpVersion.Version20;
                client.Options.HttpVersionPolicy = HttpVersionPolicy.RequestVersionExact;
                await client.ConnectAsync(uri, invoker, CancellationToken.None);
                var webSocket = new WebSocketAdapter(client);

                await Task.WhenAll(
                    SendMessagesAndCloseAsync(webSocket, read, messageSize),
                    ReceiveAllMessagesAsync(webSocket));
            },
            async (context, webSocket) =>
            {
                await Task.WhenAll(
                    SendMessagesAndCloseAsync(webSocket, written, messageSize),
                    ReceiveAllMessagesAsync(webSocket));
            },
            new TestTimeProvider(new TimeSpan(42)),
            http2Proxy: true);

        Assert.NotNull(telemetry);
        Assert.Equal(42, telemetry!.EstablishedTime.Ticks);
        Assert.Contains(telemetry.CloseReason, new[] { WebSocketCloseReason.ClientGracefulClose, WebSocketCloseReason.ServerGracefulClose });
        Assert.Equal(read, telemetry!.MessagesRead);
        Assert.Equal(written, telemetry.MessagesWritten);
    }

    public enum Behavior
    {
        ClosesConnection = 1,
        SendsClose_WaitsForClose = 2,
        SendsClose_ClosesConnection = 4 | ClosesConnection,
        WaitsForClose_SendsClose = 8,
        WaitsForClose_ClosesConnection = 16 | ClosesConnection,
    }

    [Theory]
    // Both sides close the connection - race between which is noticed first
    [InlineData(Behavior.ClosesConnection, Behavior.ClosesConnection, WebSocketCloseReason.Unknown, WebSocketCloseReason.ClientDisconnect, WebSocketCloseReason.ServerDisconnect)]
    // One side sends a graceful close
    [InlineData(Behavior.SendsClose_ClosesConnection, Behavior.WaitsForClose_ClosesConnection, WebSocketCloseReason.ClientGracefulClose)]
    [InlineData(Behavior.SendsClose_WaitsForClose, Behavior.WaitsForClose_ClosesConnection, WebSocketCloseReason.ClientGracefulClose)]
    [InlineData(Behavior.WaitsForClose_ClosesConnection, Behavior.SendsClose_ClosesConnection, WebSocketCloseReason.ServerGracefulClose)]
    [InlineData(Behavior.WaitsForClose_ClosesConnection, Behavior.SendsClose_WaitsForClose, WebSocketCloseReason.ServerGracefulClose)]
    // One side sends a graceful close while the other disconnects - race between which is noticed first
    [InlineData(Behavior.SendsClose_WaitsForClose, Behavior.ClosesConnection, WebSocketCloseReason.ClientGracefulClose, WebSocketCloseReason.ServerDisconnect)]
    [InlineData(Behavior.SendsClose_ClosesConnection, Behavior.ClosesConnection, WebSocketCloseReason.ClientGracefulClose, WebSocketCloseReason.ServerDisconnect)]
    [InlineData(Behavior.ClosesConnection, Behavior.SendsClose_ClosesConnection, WebSocketCloseReason.ServerGracefulClose, WebSocketCloseReason.ClientDisconnect)]
    [InlineData(Behavior.ClosesConnection, Behavior.SendsClose_WaitsForClose, WebSocketCloseReason.ServerGracefulClose, WebSocketCloseReason.ClientDisconnect)]
    // One side closes the connection while the other is waiting for messages
    [InlineData(Behavior.ClosesConnection, Behavior.WaitsForClose_SendsClose, WebSocketCloseReason.ClientDisconnect)]
    [InlineData(Behavior.ClosesConnection, Behavior.WaitsForClose_ClosesConnection, WebSocketCloseReason.ClientDisconnect)]
    [InlineData(Behavior.WaitsForClose_SendsClose, Behavior.ClosesConnection, WebSocketCloseReason.ServerDisconnect)]
    [InlineData(Behavior.WaitsForClose_ClosesConnection, Behavior.ClosesConnection, WebSocketCloseReason.ServerDisconnect)]
    // Graceful, mutual close - other side closes as a reaction to receiving close
    [InlineData(Behavior.SendsClose_WaitsForClose, Behavior.WaitsForClose_SendsClose, WebSocketCloseReason.ClientGracefulClose)]
    [InlineData(Behavior.SendsClose_ClosesConnection, Behavior.WaitsForClose_SendsClose, WebSocketCloseReason.ClientGracefulClose)]
    [InlineData(Behavior.WaitsForClose_SendsClose, Behavior.SendsClose_WaitsForClose, WebSocketCloseReason.ServerGracefulClose)]
    [InlineData(Behavior.WaitsForClose_SendsClose, Behavior.SendsClose_ClosesConnection, WebSocketCloseReason.ServerGracefulClose)]
    // Graceful, mutual close - both sides close at the same time - race between which is noticed first
    [InlineData(Behavior.SendsClose_WaitsForClose, Behavior.SendsClose_WaitsForClose, WebSocketCloseReason.ClientGracefulClose, WebSocketCloseReason.ServerGracefulClose)]
    [InlineData(Behavior.SendsClose_WaitsForClose, Behavior.SendsClose_ClosesConnection, WebSocketCloseReason.ClientGracefulClose, WebSocketCloseReason.ServerGracefulClose)]
    [InlineData(Behavior.SendsClose_ClosesConnection, Behavior.SendsClose_WaitsForClose, WebSocketCloseReason.ClientGracefulClose, WebSocketCloseReason.ServerGracefulClose)]
    [InlineData(Behavior.SendsClose_ClosesConnection, Behavior.SendsClose_ClosesConnection, WebSocketCloseReason.ClientGracefulClose, WebSocketCloseReason.ServerGracefulClose)]
    public async Task ConnectionClosed_BlameAttributedCorrectly(Behavior clientBehavior, Behavior serverBehavior, params WebSocketCloseReason[] expectedReasons)
    {
        var serverSawClose = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var telemetry = await TestAsync(
            async uri =>
            {
                using var client = new ClientWebSocket();

                // Keep sending messages from the client in order to observe a server disconnect sooner
                client.Options.KeepAliveInterval = TimeSpan.FromMilliseconds(10);

                await client.ConnectAsync(uri, CancellationToken.None);
                var webSocket = new WebSocketAdapter(client);

                try
                {
                    await ProcessAsync(webSocket, clientBehavior, client: client);
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"Ignored client exception: {ex}");

                    Assert.True(serverBehavior.HasFlag(Behavior.ClosesConnection));
                }
            },
            async (context, webSocket) =>
            {
                try
                {
                    await ProcessAsync(webSocket, serverBehavior, context: context);
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"Ignored destination exception: {ex}");

                    Assert.True(clientBehavior.HasFlag(Behavior.ClosesConnection));
                }
            });

        Assert.NotNull(telemetry);
        Assert.Contains(telemetry!.CloseReason, expectedReasons);

        async Task ProcessAsync(WebSocketAdapter webSocket, Behavior behavior, ClientWebSocket? client = null, HttpContext? context = null)
        {
            if (behavior == Behavior.SendsClose_WaitsForClose ||
                behavior == Behavior.SendsClose_ClosesConnection)
            {
                await webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Bye");
            }

            if (behavior == Behavior.SendsClose_WaitsForClose ||
                behavior == Behavior.WaitsForClose_SendsClose ||
                behavior == Behavior.WaitsForClose_ClosesConnection)
            {
                await ReceiveAllMessagesAsync(webSocket);

                if (context is not null)
                {
                    serverSawClose.SetResult();
                }
            }

            if (behavior == Behavior.WaitsForClose_SendsClose)
            {
                await webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Bye");
            }

            if (behavior.HasFlag(Behavior.ClosesConnection))
            {
                if (client is not null &&
                    behavior is Behavior.SendsClose_ClosesConnection &&
                    serverBehavior is Behavior.WaitsForClose_SendsClose or Behavior.WaitsForClose_ClosesConnection)
                {
                    // If we're sending a close message and expect the server to receive it, wait before killing the connection.
                    await serverSawClose.Task.WaitAsync(TimeSpan.FromMinutes(1));
                }

                client?.Abort();

                if (context is not null)
                {
                    await context.Response.Body.FlushAsync();
                    context.Abort();
                }
            }
        }
    }

    [Theory]
    [InlineData(100, 200, WebSocketCloseReason.ClientGracefulClose)]
    [InlineData(200, 100, WebSocketCloseReason.ServerGracefulClose)]
    [InlineData(100, 100, WebSocketCloseReason.ServerGracefulClose)] // Implementation detail
    public async Task ConnectionClosed_BlameReliesOnCloseTimes(long clientCloseTime, long serverCloseTime, WebSocketCloseReason expectedCloseReason)
    {
        var timeProvider = new TestTimeProvider(new TimeSpan(1));

        var telemetry = await TestAsync(
            async uri =>
            {
                using var client = new ClientWebSocket();
                await client.ConnectAsync(uri, CancellationToken.None);
                var webSocket = new WebSocketAdapter(client);

                await ProcessAsync(webSocket, timeProvider, clientCloseTime, sendCloseFirst: clientCloseTime <= serverCloseTime);
            },
            async (context, webSocket) =>
            {
                await ProcessAsync(webSocket, timeProvider, serverCloseTime, sendCloseFirst: serverCloseTime < clientCloseTime);
            },
            timeProvider);

        Assert.NotNull(telemetry);
        Assert.Equal(1, telemetry!.EstablishedTime.Ticks);
        Assert.Equal(expectedCloseReason, telemetry.CloseReason);

        static async Task ProcessAsync(WebSocketAdapter webSocket, TestTimeProvider timeProvider, long closeTime, bool sendCloseFirst)
        {
            await SendAndAcknowledgeMessageAsync(webSocket);

            var receiveTask = ReceiveAllMessagesAsync(webSocket);

            if (!sendCloseFirst)
            {
                await receiveTask;
            }

            lock (timeProvider)
            {
                timeProvider.AdvanceTo(TimeSpan.FromTicks(closeTime));
            }

            await webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Bye", CancellationToken.None);

            await receiveTask;
        }
    }

    private static async Task SendAndAcknowledgeMessageAsync(WebSocketAdapter webSocket)
    {
        var receiveBuffer = new byte[10];

        var sendTask = webSocket.SendAsync("Hello"u8.ToArray(), WebSocketMessageType.Text, endOfMessage: true).AsTask();
        var receiveTask = webSocket.ReceiveAsync(receiveBuffer).AsTask();

        await Task.WhenAll(sendTask, receiveTask);

        Assert.Equal("Hello", Encoding.UTF8.GetString(receiveBuffer[..(await receiveTask).Count]));
    }

    private static async Task ReceiveAllMessagesAsync(WebSocketAdapter webSocket)
    {
        Memory<byte> buffer = new byte[1024];

        while (true)
        {
            var result = await webSocket.ReceiveAsync(buffer);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                break;
            }
        }
    }

    private static async Task SendMessagesAndCloseAsync(WebSocketAdapter webSocket, int messageCount, int messageSize)
    {
        var rng = new Random(42);
        var buffer = new byte[1024];

        for (var i = 0; i < messageCount; i++)
        {
            var remaining = messageSize;

            while (remaining > 1)
            {
                var chunkSize = Math.Min(buffer.Length, remaining - 1);
                remaining -= chunkSize;
                var chunk = buffer.AsMemory(0, chunkSize);
                rng.NextBytes(chunk.Span);
                await webSocket.SendAsync(chunk, WebSocketMessageType.Binary, endOfMessage: false);
            }

            await webSocket.SendAsync(buffer.AsMemory(0, remaining), WebSocketMessageType.Binary, endOfMessage: true);
        }

        await webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Bye", CancellationToken.None);
    }

    private class WebSocketAdapter
    {
        private readonly ClientWebSocket? _client;
        private readonly WebSocket? _server;

        public WebSocketAdapter(ClientWebSocket? client = null, WebSocket? server = null)
        {
            Assert.True(client is null ^ server is null);
            _client = client;
            _server = server;
        }

        public ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return _client is not null
                ? _client.ReceiveAsync(buffer, cancellationToken)
                : _server!.ReceiveAsync(buffer, cancellationToken);
        }

        public ValueTask SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken = default)
        {
            return _client is not null
                ? _client.SendAsync(buffer, messageType, endOfMessage, cancellationToken)
                : _server!.SendAsync(buffer, messageType, endOfMessage, cancellationToken);
        }

        public Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken = default)
        {
            return _client is not null
                ? _client.CloseOutputAsync(closeStatus, statusDescription, cancellationToken)
                : _server!.CloseOutputAsync(closeStatus, statusDescription, cancellationToken);
        }
    }

    private static async Task<WebSocketsTelemetry?> TestAsync(Func<Uri, Task> requestDelegate, Func<HttpContext, WebSocketAdapter, Task> destinationDelegate, TimeProvider? timeProvider = null, bool http2Proxy = false)
    {
        var telemetryConsumer = new TelemetryConsumer();

        var test = new TestEnvironment()
        {
            ConfigureDestinationApp = destinationApp =>
            {
                destinationApp.UseWebSockets();

                destinationApp.Run(async context =>
                {
                    if (context.WebSockets.IsWebSocketRequest)
                    {
                        var webSocket = await context.WebSockets.AcceptWebSocketAsync();

                        await destinationDelegate(context, new WebSocketAdapter(server: webSocket));
                    }
                });
            },
            ConfigureProxyServices = proxyServices =>
            {
                if (timeProvider is not null)
                {
                    proxyServices.AddSingleton(timeProvider);
                }
            },
            ConfigureProxy = proxyBuilder =>
            {
                proxyBuilder.Services.AddTelemetryConsumer(telemetryConsumer);
            },
            ConfigureProxyApp = proxyApp =>
            {
                proxyApp.UseWebSocketsTelemetry();
            },
        };

        if (http2Proxy)
        {
            test.ProxyProtocol = HttpProtocols.Http2;
        }

        await test.Invoke(async uri =>
        {
            var webSocketsTarget = uri.Replace("https://", "wss://").Replace("http://", "ws://");
            var webSocketsUri = new Uri(webSocketsTarget, UriKind.Absolute);

            await requestDelegate(webSocketsUri);
        });

        return telemetryConsumer.Telemetry;
    }

    private record WebSocketsTelemetry(DateTime Timestamp, DateTime EstablishedTime, WebSocketCloseReason CloseReason, long MessagesRead, long MessagesWritten);

    private class TelemetryConsumer : IWebSocketsTelemetryConsumer
    {
        public WebSocketsTelemetry? Telemetry { get; private set; }

        public void OnWebSocketClosed(DateTime timestamp, DateTime establishedTime, WebSocketCloseReason closeReason, long messagesRead, long messagesWritten)
        {
            Telemetry = new WebSocketsTelemetry(timestamp, establishedTime, closeReason, messagesRead, messagesWritten);
        }
    }

    private static HttpMessageInvoker CreateInvoker()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None,
            UseCookies = false,
            UseProxy = false
        };
        handler.SslOptions.RemoteCertificateValidationCallback = (_, _, _, _) => true;
        return new HttpMessageInvoker(handler);
    }
}
