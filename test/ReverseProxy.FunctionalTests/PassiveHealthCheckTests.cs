// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Yarp.ReverseProxy.Common;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Health;

namespace Yarp.ReverseProxy;

public class PassiveHealthCheckTests
{
    private sealed class MockHttpClientFactory : IForwarderHttpClientFactory
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _sendAsync;

        public MockHttpClientFactory(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync)
        {
            _sendAsync = sendAsync;
        }

        public HttpMessageInvoker CreateClient(ForwarderHttpClientContext context)
        {
            return new HttpMessageInvoker(new MockHandler(_sendAsync));
        }

        private sealed class MockHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _sendAsync;

            public MockHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync)
            {
                _sendAsync = sendAsync;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return await _sendAsync(request, cancellationToken);
            }
        }
    }

    [Fact]
    public async Task PassiveHealthChecksEnabled_MultipleDestinationFailures_ProxyReturnsServiceUnavailable()
    {
        var destinationReached = false;
        IProxyStateLookup lookup = null;
        string clusterId = null;

        var test = new TestEnvironment(
            context =>
            {
                destinationReached = true;
                throw new InvalidOperationException();
            })
        {
            ConfigTransformer = (c, r) =>
            {
                c = c with
                {
                    HealthCheck = new HealthCheckConfig
                    {
                        AvailableDestinationsPolicy = HealthCheckConstants.AvailableDestinations.HealthyAndUnknown,
                        Passive = new PassiveHealthCheckConfig
                        {
                            Enabled = true
                        }
                    }
                };

                clusterId = c.ClusterId;

                return (c, r);
            },
            ConfigureProxy = proxyBuilder => proxyBuilder.Services.AddSingleton<IForwarderHttpClientFactory>(new MockHttpClientFactory((_, _) => throw new IOException())),
            ConfigureProxyApp = proxyApp =>
            {
                lookup = proxyApp.ApplicationServices.GetRequiredService<IProxyStateLookup>();
            },
        };

        await test.Invoke(async uri =>
        {
            using var client = new HttpClient();

            for (var i = 0; i < 10; i++)
            {
                using var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, uri));
                Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
            }

            Assert.NotNull(lookup);
            Assert.NotNull(clusterId);

            // The destination list will be updated asynchronously in the background.
            // Wait until that update takes effect.
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            while (true)
            {
                Assert.True(lookup.TryGetCluster(clusterId, out var cluster));
                Assert.Single(cluster.DestinationsState.AllDestinations);

                if (cluster.DestinationsState.AvailableDestinations.Count == 0)
                {
                    break;
                }

                await Task.Delay(10, cts.Token);
            }

            for (var i = 0; i < 42; i++)
            {
                using var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, uri));
                Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            }
        });

        Assert.False(destinationReached);
    }

    [Fact]
    public async Task PassiveHealthChecksEnabled_IncompleteClientRequests_ProxyHealthIsUnaffected()
    {
        var destinationReached = false;

        var shouldThrow = true;
        var requestStartedTcs = new TaskCompletionSource<byte>(TaskCreationOptions.RunContinuationsAsynchronously);

        var proxySendAsync = async (HttpRequestMessage request, CancellationToken ct) =>
        {
            requestStartedTcs.SetResult(0);

            if (shouldThrow)
            {
                await Task.Delay(-1, ct);

                throw new OperationCanceledException(ct);
            }
            else
            {
                return new HttpResponseMessage((HttpStatusCode)418)
                {
                    Content = new StringContent("Hello world")
                };
            }
        };

        var test = new TestEnvironment(
            context =>
            {
                destinationReached = true;
                throw new InvalidOperationException();
            })
        {
            ConfigTransformer = (c, r) =>
            {
                c = c with
                {
                    HealthCheck = new HealthCheckConfig
                    {
                        Passive = new PassiveHealthCheckConfig
                        {
                            Enabled = true
                        }
                    }
                };

                return (c, r);
            },
            ConfigureProxy = proxyBuilder => proxyBuilder.Services.AddSingleton<IForwarderHttpClientFactory>(new MockHttpClientFactory(proxySendAsync)),
        };

        await test.Invoke(async uri =>
        {
            using var client = new HttpClient();
            for (var i = 0; i < 42; i++)
            {
                using var cts = new CancellationTokenSource();
                _ = requestStartedTcs.Task.ContinueWith(_ => cts.Cancel());

                try
                {
                    await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, uri), cts.Token);
                    Assert.True(false);
                }
                catch { }

                requestStartedTcs = new TaskCompletionSource<byte>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            shouldThrow = false;

            using var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, uri));

            Assert.Equal(418, (int)response.StatusCode);
            Assert.Equal("Hello world", await response.Content.ReadAsStringAsync());
        });

        Assert.False(destinationReached);
    }
}
