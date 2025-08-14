// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using Xunit;
using Yarp.ReverseProxy.Common;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Transforms;

namespace Yarp.ReverseProxy;

public class WebSocketTests
{
    private readonly ITestOutputHelper _output;

    public WebSocketTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public static IEnumerable<object[]> WebSocketVersionNegotiation_TestData()
    {
        foreach (Version incomingVersion in new[] { HttpVersion.Version11, HttpVersion.Version20 })
        {
            foreach (HttpVersionPolicy versionPolicy in Enum.GetValues<HttpVersionPolicy>())
            {
                foreach (Version destinationVersion in new[] { HttpVersion.Version11, HttpVersion.Version20, HttpVersion.Version30 })
                {
                    foreach (HttpProtocols destinationProtocols in new[] { HttpProtocols.Http1, HttpProtocols.Http2, HttpProtocols.Http1AndHttp2 })
                    {
                        foreach (bool useHttpsOnDestination in new[] { true, false })
                        {
                            (int version, bool canDowngrade) = (destinationVersion.Major, versionPolicy, useHttpsOnDestination) switch
                            {
                                (1, HttpVersionPolicy.RequestVersionOrHigher, true) => (2, true),
                                (1, _, _) => (1, false),
                                (2, HttpVersionPolicy.RequestVersionOrLower, true) => (2, true),
                                (2, HttpVersionPolicy.RequestVersionOrLower, false) => (1, false),
                                (2, _, _) => (2, false),
                                (3, HttpVersionPolicy.RequestVersionOrLower, true) => (2, true),
                                (3, HttpVersionPolicy.RequestVersionOrLower, false) => (1, false),
                                (3, _, _) => (-1, false), // RequestCreation error
                                _ => throw new Exception()
                            };

                            ForwarderError? expectedProxyError = version == -1 ? ForwarderError.RequestCreation : null;
                            bool e2eWillFail = expectedProxyError.HasValue;

                            if (version == 2 && destinationProtocols == HttpProtocols.Http1)
                            {
                                // ALPN rejects HTTP/2.
                                if (canDowngrade)
                                {
                                    Debug.Assert(useHttpsOnDestination);
                                    version = 1;
                                }
                                else
                                {
                                    e2eWillFail = true;
                                    expectedProxyError = ForwarderError.Request;
                                }
                            }

                            if (version == 1 && destinationProtocols == HttpProtocols.Http2)
                            {
                                // ALPN rejects HTTP/1.1, or the server sends back an error response when not using TLS.
                                e2eWillFail = true;

                                // An error response is just a bad status code, not a failed request from the proxy's perspective.
                                if (useHttpsOnDestination)
                                {
                                    expectedProxyError = ForwarderError.Request;
                                }
                            }

                            if (version == 2 && destinationProtocols == HttpProtocols.Http1AndHttp2 && !useHttpsOnDestination)
                            {
                                // No ALPN, Kestrel doesn't know whether to use HTTP/1.1 or HTTP/2, defaulting to HTTP/1.1.
                                // YARP will see an 'HTTP_1_1_REQUIRED' error and return a 502.
                                Debug.Assert(!canDowngrade);
                                e2eWillFail = true;
                                expectedProxyError = ForwarderError.Request;
                            }

                            string expectedVersion = version == 1 ? "HTTP/1.1" : "HTTP/2";

                            yield return new object[] { incomingVersion, versionPolicy, destinationVersion, destinationProtocols, useHttpsOnDestination, expectedVersion, expectedProxyError, e2eWillFail };
                        }
                    }
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(WebSocketVersionNegotiation_TestData))]
    public async Task WebSocketVersionNegotiation(Version incomingVersion, HttpVersionPolicy versionPolicy, Version requestedDestinationVersion, HttpProtocols destinationProtocols, bool useHttpsOnDestination,
        string expectedVersion, ForwarderError? expectedProxyError, bool e2eWillFail)
    {
        using var cts = CreateTimer();

        var test = CreateTestEnvironment();
        test.ProxyProtocol = incomingVersion.Major == 1 ? HttpProtocols.Http1 : HttpProtocols.Http2;
        test.DestinationProtocol = destinationProtocols;
        test.DestinationHttpVersion = requestedDestinationVersion;
        test.DestinationHttpVersionPolicy = versionPolicy;
        test.UseHttpsOnDestination = useHttpsOnDestination;

        int proxyRequests = 0;
        ForwarderError? error = null;

        test.ConfigureProxyApp = builder =>
        {
            builder.Use(async (context, next) =>
            {
                proxyRequests++;
                await next(context);

                error = context.Features.Get<IForwarderErrorFeature>()?.Error;
            });
        };

        await test.Invoke(async uri =>
        {
            using var client = new ClientWebSocket();
            client.Options.HttpVersion = incomingVersion;
            client.Options.HttpVersionPolicy = HttpVersionPolicy.RequestVersionExact;

            if (e2eWillFail)
            {
                var ex = await Assert.ThrowsAsync<WebSocketException>(() => SendWebSocketRequestAsync(client, uri, expectedVersion, cts.Token));
                Assert.IsNotType<TaskCanceledException>(ex.InnerException);
            }
            else
            {
                await SendWebSocketRequestAsync(client, uri, expectedVersion, cts.Token);
            }
        }, cts.Token);

        Assert.Equal(1, proxyRequests);
        Assert.Equal(expectedProxyError, error);
    }

    [Theory]
    [InlineData(WebSocketMessageType.Binary)]
    [InlineData(WebSocketMessageType.Text)]
    public async Task WebSocketMessageTypes(WebSocketMessageType messageType)
    {
        using var cts = CreateTimer();

        var test = CreateTestEnvironment();

        await test.Invoke(async uri =>
        {
            using var client = new ClientWebSocket();
            var webSocketsTarget = uri.Replace("https://", "wss://").Replace("http://", "ws://");
            var targetUri = new Uri(new Uri(webSocketsTarget, UriKind.Absolute), "websockets");
            await client.ConnectAsync(targetUri, cts.Token);

            var buffer = new byte[1024];
            var textToSend = $"Hello World!";
            var numBytes = Encoding.UTF8.GetBytes(textToSend, buffer.AsSpan());
            await client.SendAsync(new ArraySegment<byte>(buffer, 0, numBytes),
                messageType,
                endOfMessage: true,
                cts.Token);

            var message = await client.ReceiveAsync(buffer, cts.Token);

            Assert.Equal(messageType, message.MessageType);
            Assert.True(message.EndOfMessage);

            var text = Encoding.UTF8.GetString(buffer.AsSpan(0, message.Count));
            Assert.Equal(textToSend, text);

            await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Bye", cts.Token);
            Assert.Equal(WebSocketCloseStatus.NormalClosure, client.CloseStatus);
            Assert.Equal("Bye", client.CloseStatusDescription);
        }, cts.Token);
    }

    [Fact]
    public async Task RawUpgradeTest()
    {
        using var cts = CreateTimer();

        var test = CreateTestEnvironment();

        await test.Invoke(async uri =>
        {
            using var client = WebSocketTests.CreateInvoker();
            var targetUri = new Uri(new Uri(uri, UriKind.Absolute), "rawupgrade");
            using var request = new HttpRequestMessage(HttpMethod.Get, targetUri);

            // TODO: https://github.com/dotnet/yarp/issues/255 Until this is fixed the "Upgrade: WebSocket" header is required.
            request.Headers.TryAddWithoutValidation("Upgrade", "WebSocket");

            request.Headers.TryAddWithoutValidation("Connection", "upgrade");
            request.Version = new Version(1, 1);

            var response = await client.SendAsync(request, cts.Token);

            Assert.Equal(HttpStatusCode.SwitchingProtocols, response.StatusCode);

            using var rawStream = await response.Content.ReadAsStreamAsync(cts.Token);

            var buffer = new byte[5];
            for (var i = 0; i <= 255; i++)
            {
                buffer[0] = (byte)i;
                await rawStream.WriteAsync(buffer, 0, buffer.Length, cts.Token);
                var read = await rawStream.ReadAsync(buffer, cts.Token);

                Assert.Equal(buffer.Length, read);
                Assert.Equal(i, buffer[0]);
            }

            await rawStream.WriteAsync(Encoding.UTF8.GetBytes("close"));
            while (await rawStream.ReadAsync(buffer, cts.Token) != 0) { }
            rawStream.Dispose();
        }, cts.Token);
    }

    [Fact]
    // https://github.com/dotnet/yarp/issues/255 IIS claims all requests are upgradeable.
    public async Task FalseUpgradeTest()
    {
        using var cts = CreateTimer();

        var test = CreateTestEnvironment(forceUpgradable: true);

        await test.Invoke(async uri =>
        {
            using var client = WebSocketTests.CreateInvoker();
            var targetUri = new Uri(new Uri(uri, UriKind.Absolute), "post");
            using var request = new HttpRequestMessage(HttpMethod.Post, targetUri);
            request.Content = new StringContent("Hello World");
            request.Version = new Version(1, 1);

            var response = await client.SendAsync(request, cts.Token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("Hello World", await response.Content.ReadAsStringAsync(cts.Token));
        }, cts.Token);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WebSocket11_To_11(bool useHttps)
    {
        using var cts = CreateTimer();

        var test = CreateTestEnvironment();
        test.ProxyProtocol = HttpProtocols.Http1;
        test.DestinationProtocol = HttpProtocols.Http1;
        test.DestinationHttpVersion = HttpVersion.Version11;
        test.UseHttpsOnProxy = useHttps;
        test.UseHttpsOnDestination = useHttps;

        await test.Invoke(async uri =>
        {
            using var client = new ClientWebSocket();
            await SendWebSocketRequestAsync(client, uri, "HTTP/1.1", cts.Token);
        }, cts.Token);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WebSocket20_To_20(bool useHttps)
    {
        using var cts = CreateTimer();

        var test = CreateTestEnvironment();
        test.ProxyProtocol = HttpProtocols.Http2;
        test.DestinationProtocol = HttpProtocols.Http2;
        test.DestinationHttpVersionPolicy = HttpVersionPolicy.RequestVersionExact;
        test.UseHttpsOnProxy = useHttps;
        test.UseHttpsOnDestination = useHttps;

        await test.Invoke(async uri =>
        {
            using var client = new ClientWebSocket();
            client.Options.HttpVersion = HttpVersion.Version20;
            client.Options.HttpVersionPolicy = HttpVersionPolicy.RequestVersionExact;
            await SendWebSocketRequestAsync(client, uri, "HTTP/2", cts.Token);
        }, cts.Token);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WebSocket20_To_11(bool useHttps)
    {
        using var cts = CreateTimer();

        var test = CreateTestEnvironment();
        test.ProxyProtocol = HttpProtocols.Http2;
        test.DestinationProtocol = HttpProtocols.Http1;
        test.DestinationHttpVersion = HttpVersion.Version11;
        test.UseHttpsOnProxy = useHttps;
        test.UseHttpsOnDestination = useHttps;

        await test.Invoke(async uri =>
        {
            using var client = new ClientWebSocket();
            client.Options.HttpVersion = HttpVersion.Version20;
            client.Options.HttpVersionPolicy = HttpVersionPolicy.RequestVersionExact;
            await SendWebSocketRequestAsync(client, uri, "HTTP/1.1", cts.Token);
        }, cts.Token);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WebSocket11_To_20(bool useHttps)
    {
        using var cts = CreateTimer();

        var test = CreateTestEnvironment();
        test.ProxyProtocol = HttpProtocols.Http1;
        test.DestinationProtocol = HttpProtocols.Http2;
        test.DestinationHttpVersionPolicy = HttpVersionPolicy.RequestVersionExact;
        test.UseHttpsOnProxy = useHttps;
        test.UseHttpsOnDestination = useHttps;

        await test.Invoke(async uri =>
        {
            using var client = new ClientWebSocket();
            client.Options.HttpVersion = HttpVersion.Version11;
            client.Options.HttpVersionPolicy = HttpVersionPolicy.RequestVersionExact;
            await SendWebSocketRequestAsync(client, uri, "HTTP/2", cts.Token);
        }, cts.Token);
    }

    [Fact]
    public async Task WebSocketFallbackFromH2()
    {
        using var cts = CreateTimer();

        var test = CreateTestEnvironment();
        test.ProxyProtocol = HttpProtocols.Http1;
        // The destination doesn't support HTTP/2, as determined by ALPN
        test.DestinationProtocol = HttpProtocols.Http1;
        test.DestinationHttpVersion = HttpVersion.Version20;
        test.UseHttpsOnDestination = true;

        await test.Invoke(async uri =>
        {
            using var client = new ClientWebSocket();
            await SendWebSocketRequestAsync(client, uri, "HTTP/1.1", cts.Token);
        }, cts.Token);
    }

    [Fact]
    public async Task WebSocketFallbackFromH2_FailureInSecondRequestTransform_TreatedAsRequestCreationFailure()
    {
        using var cts = CreateTimer();

        ForwarderError? error = null;

        var test = new TestEnvironment()
        {
            TestOutput = _output,
            ProxyProtocol = HttpProtocols.Http1,
            // The destination doesn't support HTTP/2, as determined by ALPN
            DestinationProtocol = HttpProtocols.Http1,
            DestinationHttpVersion = HttpVersion.Version20,
            UseHttpsOnDestination = true,
            ConfigureProxy = builder =>
            {
                builder.AddTransforms(transforms =>
                {
                    transforms.AddRequestTransform(context =>
                    {
                        if (context.ProxyRequest.Version.Major == 1)
                        {
                            // This is the second (downgrade) request.
                            throw new Exception("Foo");
                        }

                        return default;
                    });
                });
            },
            ConfigureProxyApp = builder =>
            {
                builder.Use(async (context, next) =>
                {
                    await next(context);

                    error = context.Features.Get<IForwarderErrorFeature>()?.Error;
                });
            },
        };

        await test.Invoke(async uri =>
        {
            try
            {
                using var client = new ClientWebSocket();
                await SendWebSocketRequestAsync(client, uri, "HTTP/1.1", cts.Token);
            }
            catch { }
        }, cts.Token);

        Assert.Equal(ForwarderError.RequestCreation, error);
    }

    // [Fact]
    [Fact(Skip = "Manual test only, the CI doesn't always have the IIS Express test cert installed.")]
    public async Task WebSocketFallbackFromH2WS()
    {
        if (!OperatingSystem.IsWindows())
        {
            // This test relies on Windows/HttpSys
            return;
        }

        using var cts = CreateTimer();

        var test = CreateTestEnvironment();
        test.ProxyProtocol = HttpProtocols.Http1;
        // The destination supports HTTP/2, but not H2WS
        test.UseHttpSysOnDestination = true;
        test.UseHttpsOnDestination = true;

        await test.Invoke(async uri =>
        {
            using var client = new ClientWebSocket();
            await SendWebSocketRequestAsync(client, uri, "HTTP/1.1", cts.Token);
        }, cts.Token);
    }

    [Theory]
    [InlineData(HttpVersionPolicy.RequestVersionExact, true)]
    [InlineData(HttpVersionPolicy.RequestVersionExact, false)]
    [InlineData(HttpVersionPolicy.RequestVersionOrHigher, true)]
    public async Task WebSocketCantFallbackFromH2(HttpVersionPolicy policy, bool useHttps)
    {
        using var cts = CreateTimer();

        var test = CreateTestEnvironment();
        test.ProxyProtocol = HttpProtocols.Http1;
        test.DestinationProtocol = HttpProtocols.Http1;
        test.DestinationHttpVersion = HttpVersion.Version20;
        test.DestinationHttpVersionPolicy = policy;
        test.UseHttpsOnDestination = useHttps;

        await test.Invoke(async uri =>
        {
            using var client = new ClientWebSocket();
            var webSocketsTarget = uri.Replace("https://", "wss://").Replace("http://", "ws://");
            var targetUri = new Uri(new Uri(webSocketsTarget, UriKind.Absolute), "websockets");
            using var invoker = CreateInvoker();
            var wse = await Assert.ThrowsAsync<WebSocketException>(() => client.ConnectAsync(targetUri, invoker, cts.Token));
            Assert.Equal("The server returned status code '502' when status code '101' was expected.", wse.Message);
        }, cts.Token);
    }

    [Theory]
    [InlineData(HttpProtocols.Http1)] // Checked by destination
    [InlineData(HttpProtocols.Http2)] // Checked by proxy
    public async Task InvalidKeyHeader_400(HttpProtocols destinationProtocol)
    {
        using var cts = CreateTimer();

        var test = CreateTestEnvironment();
        test.ProxyProtocol = HttpProtocols.Http1;
        test.DestinationProtocol = destinationProtocol;
        test.DestinationHttpVersionPolicy = HttpVersionPolicy.RequestVersionExact;
        test.DestinationHttpVersion = destinationProtocol == HttpProtocols.Http1 ? HttpVersion.Version11 : HttpVersion.Version20;

        test.ConfigureProxyApp = builder =>
        {
            builder.Use(async (context, next) =>
            {
                context.Request.Headers[HeaderNames.SecWebSocketKey] = "ThisIsAnIncorrectKeyHeaderLongerThan24Bytes";

                var logs = TestLogger.Collect();
                await next(context);

                if (destinationProtocol == HttpProtocols.Http1)
                {
                    Assert.DoesNotContain(logs, log => log.EventId == EventIds.InvalidSecWebSocketKeyHeader);
                }
                else
                {
                    Assert.Contains(logs, log => log.EventId == EventIds.InvalidSecWebSocketKeyHeader);
                }
            });
        };

        await test.Invoke(async uri =>
        {
            using var client = new ClientWebSocket();
            client.Options.CollectHttpResponseDetails = true;
            var webSocketsTarget = uri.Replace("https://", "wss://").Replace("http://", "ws://");
            var targetUri = new Uri(new Uri(webSocketsTarget, UriKind.Absolute), "websockets");
            client.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;
            var wse = await Assert.ThrowsAsync<WebSocketException>(() => client.ConnectAsync(targetUri, cts.Token));
            Assert.Equal("The server returned status code '400' when status code '101' was expected.", wse.Message);
            Assert.Equal(HttpStatusCode.BadRequest, client.HttpStatusCode);
            // TODO: Assert the version https://github.com/dotnet/runtime/issues/75353
        }, cts.Token);
    }

    [Fact]
    public async Task WebSocket20_To_11_WithWellFormedKeyHeader_OriginalKeyIsUsed()
    {
        using var cts = CreateTimer();

        var clientKey = ProtocolHelper.CreateSecWebSocketKey();

        var test = CreateTestEnvironment();
        test.ProxyProtocol = HttpProtocols.Http2;
        test.DestinationProtocol = HttpProtocols.Http1;

        var originalDestinationApp = test.ConfigureDestinationApp;
        test.ConfigureDestinationApp = app =>
        {
            app.Use((context, next) =>
            {
                Assert.True(context.Request.Headers.TryGetValue(HeaderNames.SecWebSocketKey, out var key));
                Assert.Equal(clientKey, key);
                return next(context);
            });
            originalDestinationApp(app);
        };

        await test.Invoke(async uri =>
        {
            using var client = new ClientWebSocket();
            client.Options.HttpVersion = HttpVersion.Version20;
            client.Options.HttpVersionPolicy = HttpVersionPolicy.RequestVersionExact;

            client.Options.SetRequestHeader(HeaderNames.SecWebSocketKey, clientKey);

            await SendWebSocketRequestAsync(client, uri, "HTTP/1.1", cts.Token);
        }, cts.Token);
    }

    [Fact]
    public async Task WebSocket20_To_11_WithInvalidKeyHeader_RequestRejected()
    {
        using var cts = CreateTimer();

        var test = CreateTestEnvironment();
        test.ProxyProtocol = HttpProtocols.Http2;
        test.DestinationProtocol = HttpProtocols.Http1;

        test.ConfigureProxyApp = builder =>
        {
            builder.Use(async (context, next) =>
            {
                var logs = TestLogger.Collect();
                await next(context);
                Assert.Contains(logs, log => log.EventId == EventIds.InvalidSecWebSocketKeyHeader);
            });
        };

        await test.Invoke(async uri =>
        {
            var webSocketsTarget = uri.Replace("http://", "ws://");
            var targetUri = new Uri(new Uri(webSocketsTarget, UriKind.Absolute), "websockets");

            using var client = new ClientWebSocket();
            client.Options.HttpVersion = HttpVersion.Version20;
            client.Options.HttpVersionPolicy = HttpVersionPolicy.RequestVersionExact;
            client.Options.CollectHttpResponseDetails = true;

            client.Options.SetRequestHeader(HeaderNames.SecWebSocketKey, "Foo");

            using var invoker = CreateInvoker();
            var wse = await Assert.ThrowsAsync<WebSocketException>(() => client.ConnectAsync(targetUri, invoker, cts.Token));
            Assert.Equal("The server returned status code '400' when status code '200' was expected.", wse.Message);
            Assert.Equal(HttpStatusCode.BadRequest, client.HttpStatusCode);
        }, cts.Token);
    }

    private async Task SendWebSocketRequestAsync(ClientWebSocket client, string uri, string destinationProtocol, CancellationToken token)
    {
        var webSocketsTarget = uri.Replace("https://", "wss://").Replace("http://", "ws://");
        var targetUri = new Uri(new Uri(webSocketsTarget, UriKind.Absolute), "websocketversion");
        using var invoker = CreateInvoker();
        await client.ConnectAsync(targetUri, invoker, token);
        _output.WriteLine("Client connected.");

        var buffer = new byte[1024];
        var textToSend = $"Hello World!";
        var numBytes = Encoding.UTF8.GetBytes(textToSend, buffer);
        await client.SendAsync(buffer.AsMemory(0, numBytes),
            WebSocketMessageType.Text,
            endOfMessage: true,
            token);
        _output.WriteLine($"Client sent {numBytes}.");

        var message = await client.ReceiveAsync(buffer, token);
        _output.WriteLine($"Client received {message.Count}.");

        Assert.Equal(WebSocketMessageType.Text, message.MessageType);
        Assert.True(message.EndOfMessage);

        var text = Encoding.UTF8.GetString(buffer.AsSpan(0, message.Count));
        Assert.Equal(destinationProtocol, text);

        _output.WriteLine($"Client sending Close.");
        await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Bye", token);
        Assert.Equal(WebSocketCloseStatus.NormalClosure, client.CloseStatus);
        Assert.Equal("Bye", client.CloseStatusDescription);
        _output.WriteLine($"Client Closed.");
    }

    private TestEnvironment CreateTestEnvironment(bool forceUpgradable = false)
    {
        return new TestEnvironment()
        {
            TestOutput = _output,
            ConfigureDestinationServices = destinationServices =>
            {
                destinationServices.AddRouting();
            },
            ConfigureDestinationApp = destinationApp =>
            {
                destinationApp.UseWebSockets();
                destinationApp.UseRouting();
                destinationApp.UseEndpoints(builder =>
                {
                    builder.Map("/websockets", WebSocket);
                    builder.Map("/websocketVersion", WebSocketVersion);
                    builder.Map("/rawupgrade", RawUpgrade);
                    builder.Map("/post", Post);
                });
            },
            ConfigureProxyApp = proxyApp =>
            {
                // Mimic the IIS issue https://github.com/dotnet/yarp/issues/255
                proxyApp.Use((context, next) =>
                {
                    if (forceUpgradable && !(context.Features.Get<IHttpUpgradeFeature>()?.IsUpgradableRequest == true))
                    {
                        context.Features.Set<IHttpUpgradeFeature>(new AlwaysUpgradeFeature());
                    }
                    return next();
                });
            },
        };

        static async Task WebSocket(HttpContext httpContext)
        {
            var logger = httpContext.RequestServices.GetRequiredService<ILogger<WebSocketTests>>();
            if (!httpContext.WebSockets.IsWebSocketRequest)
            {
                httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                logger.LogInformation("Non-WebSocket request refused.");
                return;
            }

            using var webSocket = await httpContext.WebSockets.AcceptWebSocketAsync();
            logger.LogInformation("WebSocket accepted.");

            var buffer = new byte[1024];
            while (true)
            {
                var message = await webSocket.ReceiveAsync(buffer, httpContext.RequestAborted);
                if (message.MessageType == WebSocketMessageType.Close)
                {
                    logger.LogInformation("WebSocket Close received {status}.", message.CloseStatus);
                    await webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, message.CloseStatusDescription, httpContext.RequestAborted);
                    logger.LogInformation("WebSocket Close sent {status}.", WebSocketCloseStatus.NormalClosure);
                    return;
                }

                logger.LogInformation("WebSocket received {count} bytes.", message.Count);

                await webSocket.SendAsync(buffer[0..message.Count],
                    message.MessageType,
                    message.EndOfMessage,
                    httpContext.RequestAborted);

                logger.LogInformation("WebSocket sent {count} bytes.", message.Count);
            }
        }

        static async Task WebSocketVersion(HttpContext httpContext)
        {
            var logger = httpContext.RequestServices.GetRequiredService<ILogger<WebSocketTests>>();
            if (!httpContext.WebSockets.IsWebSocketRequest)
            {
                httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                logger.LogInformation("Non-WebSocket request refused.");
                return;
            }

            using var webSocket = await httpContext.WebSockets.AcceptWebSocketAsync();
            logger.LogInformation("WebSocket accepted.");

            var buffer = new byte[1024];
            while (true)
            {
                var message = await webSocket.ReceiveAsync(buffer, httpContext.RequestAborted);
                if (message.MessageType == WebSocketMessageType.Close)
                {
                    logger.LogInformation("WebSocket Close received {status}.", message.CloseStatus);
                    await webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, message.CloseStatusDescription, httpContext.RequestAborted);
                    logger.LogInformation("WebSocket Close sent {status}.", WebSocketCloseStatus.NormalClosure);
                    return;
                }

                logger.LogInformation("WebSocket received {count} bytes.", message.Count);

                await webSocket.SendAsync(Encoding.ASCII.GetBytes(httpContext.Request.Protocol),
                    WebSocketMessageType.Text,
                    endOfMessage: true,
                    httpContext.RequestAborted);

                logger.LogInformation("WebSocket sent {count} bytes.", httpContext.Request.Protocol.Length);
            }
        }

        static async Task RawUpgrade(HttpContext httpContext)
        {
            var upgradeFeature = httpContext.Features.Get<IHttpUpgradeFeature>();
            if (upgradeFeature is null || !upgradeFeature.IsUpgradableRequest)
            {
                httpContext.Response.StatusCode = StatusCodes.Status426UpgradeRequired;
                return;
            }

            await using var stream = await upgradeFeature.UpgradeAsync();
            var buffer = new byte[5];
            int read;
            while ((read = await stream.ReadAsync(buffer, httpContext.RequestAborted)) != 0)
            {
                await stream.WriteAsync(buffer, 0, read, httpContext.RequestAborted);

                if (string.Equals("close", Encoding.UTF8.GetString(buffer, 0, read), StringComparison.Ordinal))
                {
                    break;
                }
            }
        }

        static async Task Post(HttpContext httpContext)
        {
            var body = await new StreamReader(httpContext.Request.Body).ReadToEndAsync();
            await httpContext.Response.WriteAsync(body);
        }
    }

    private static CancellationTokenSource CreateTimer()
    {
        if (Debugger.IsAttached)
        {
            return new CancellationTokenSource();
        }
        return new CancellationTokenSource(TimeSpan.FromSeconds(15));
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

    private class AlwaysUpgradeFeature : IHttpUpgradeFeature
    {
        public bool IsUpgradableRequest => true;

        public Task<Stream> UpgradeAsync()
        {
            throw new InvalidOperationException("This wasn't supposed to get called.");
        }
    }
}
