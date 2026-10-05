// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Health;
using Yarp.ReverseProxy.Model;

namespace Yarp.ReverseProxy.Routing.Tests;

public class ReverseProxyIEndpointRouteBuilderExtensionsTests
{
    private const string PassivePolicyName = "TestPassivePolicy";
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MapReverseProxy_Success_RecordsPassiveHealthAfterForwardingExactlyOnce(bool useDefaultPipeline)
    {
        var order = new List<string>();
        var forwardingStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var completeForwarding = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var forwarder = new Mock<IHttpForwarder>();
        forwarder.Setup(f => f.SendAsync(
                It.IsAny<HttpContext>(),
                It.IsAny<string>(),
                It.IsAny<HttpMessageInvoker>(),
                It.IsAny<ForwarderRequestConfig>(),
                It.IsAny<HttpTransformer>()))
            .Returns(async () =>
            {
                forwardingStarted.SetResult(true);
                await completeForwarding.Task;
                order.Add("forwarded");
                return ForwarderError.None;
            });
        var policy = CreatePassivePolicy();
        policy.Setup(p => p.RequestProxied(
                It.IsAny<HttpContext>(),
                It.IsAny<ClusterState>(),
                It.IsAny<DestinationState>()))
            .Callback(() => order.Add("recorded"));

        using var host = await CreateHostAsync(useDefaultPipeline, forwarder.Object, policy.Object);
        var requestTask = host.GetTestClient().GetAsync("/");

        await forwardingStarted.Task.WaitAsync(TestTimeout);
        policy.Verify(p => p.RequestProxied(
            It.IsAny<HttpContext>(),
            It.IsAny<ClusterState>(),
            It.IsAny<DestinationState>()), Times.Never);

        completeForwarding.SetResult(true);
        using var response = await requestTask.WaitAsync(TestTimeout);

        response.EnsureSuccessStatusCode();
        Assert.Equal(new[] { "forwarded", "recorded" }, order);
        forwarder.Verify(f => f.SendAsync(
            It.IsAny<HttpContext>(),
            It.IsAny<string>(),
            It.IsAny<HttpMessageInvoker>(),
            It.IsAny<ForwarderRequestConfig>(),
            It.IsAny<HttpTransformer>()), Times.Once);
        policy.Verify(p => p.RequestProxied(
            It.IsAny<HttpContext>(),
            It.IsAny<ClusterState>(),
            It.IsAny<DestinationState>()), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MapReverseProxy_ForwarderThrows_PropagatesExceptionWithoutRecording(bool useDefaultPipeline)
    {
        var expectedException = new InvalidOperationException("Forwarder failure");
        var forwarder = new Mock<IHttpForwarder>();
        forwarder.Setup(f => f.SendAsync(
                It.IsAny<HttpContext>(),
                It.IsAny<string>(),
                It.IsAny<HttpMessageInvoker>(),
                It.IsAny<ForwarderRequestConfig>(),
                It.IsAny<HttpTransformer>()))
            .Returns(async () =>
            {
                await Task.Yield();
                throw expectedException;
            });
        var policy = CreatePassivePolicy();

        using var host = await CreateHostAsync(useDefaultPipeline, forwarder.Object, policy.Object);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.GetTestClient().GetAsync("/").WaitAsync(TestTimeout));

        Assert.Same(expectedException, exception);
        policy.Verify(p => p.RequestProxied(
            It.IsAny<HttpContext>(),
            It.IsAny<ClusterState>(),
            It.IsAny<DestinationState>()), Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MapReverseProxy_ClientCancellation_RemainsCanceledWithoutRecording(bool useDefaultPipeline)
    {
        var forwardingStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var forwardingCanceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var forwarder = new Mock<IHttpForwarder>();
        forwarder.Setup(f => f.SendAsync(
                It.IsAny<HttpContext>(),
                It.IsAny<string>(),
                It.IsAny<HttpMessageInvoker>(),
                It.IsAny<ForwarderRequestConfig>(),
                It.IsAny<HttpTransformer>()))
            .Returns((HttpContext context, string _, HttpMessageInvoker _, ForwarderRequestConfig _, HttpTransformer _) =>
                WaitForCancellationAsync(context.RequestAborted, forwardingStarted, forwardingCanceled));
        var policy = CreatePassivePolicy();

        using var host = await CreateHostAsync(useDefaultPipeline, forwarder.Object, policy.Object);
        using var cancellationSource = new CancellationTokenSource();
        var requestTask = host.GetTestClient().GetAsync("/", cancellationSource.Token);

        await forwardingStarted.Task.WaitAsync(TestTimeout);
        cancellationSource.Cancel();

        await forwardingCanceled.Task.WaitAsync(TestTimeout);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => requestTask.WaitAsync(TestTimeout));
        Assert.True(requestTask.IsCanceled);
        policy.Verify(p => p.RequestProxied(
            It.IsAny<HttpContext>(),
            It.IsAny<ClusterState>(),
            It.IsAny<DestinationState>()), Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MapReverseProxy_ForwarderError_RecordsPassiveHealthAfterForwardingExactlyOnce(bool useDefaultPipeline)
    {
        var order = new List<string>();
        var forwarderException = new IOException("Destination failure");
        var forwarder = new Mock<IHttpForwarder>();
        forwarder.Setup(f => f.SendAsync(
                It.IsAny<HttpContext>(),
                It.IsAny<string>(),
                It.IsAny<HttpMessageInvoker>(),
                It.IsAny<ForwarderRequestConfig>(),
                It.IsAny<HttpTransformer>()))
            .Returns((HttpContext context, string _, HttpMessageInvoker _, ForwarderRequestConfig _, HttpTransformer _) =>
            {
                return CompleteWithErrorAsync(context, order, forwarderException);
            });
        var policy = CreatePassivePolicy();
        policy.Setup(p => p.RequestProxied(
                It.IsAny<HttpContext>(),
                It.IsAny<ClusterState>(),
                It.IsAny<DestinationState>()))
            .Callback<HttpContext, ClusterState, DestinationState>((context, _, _) =>
            {
                var error = Assert.IsAssignableFrom<IForwarderErrorFeature>(context.Features.Get<IForwarderErrorFeature>());
                Assert.Equal(ForwarderError.Request, error.Error);
                Assert.Same(forwarderException, error.Exception);
                order.Add("recorded");
            });

        using var host = await CreateHostAsync(useDefaultPipeline, forwarder.Object, policy.Object);
        using var response = await host.GetTestClient().GetAsync("/").WaitAsync(TestTimeout);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal(new[] { "forwarded", "recorded" }, order);
        policy.Verify(p => p.RequestProxied(
            It.IsAny<HttpContext>(),
            It.IsAny<ClusterState>(),
            It.IsAny<DestinationState>()), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MapReverseProxy_NoDestinations_ReturnsServiceUnavailableWithoutRecording(bool useDefaultPipeline)
    {
        var forwarder = new Mock<IHttpForwarder>();
        var policy = CreatePassivePolicy();

        using var host = await CreateHostAsync(
            useDefaultPipeline,
            forwarder.Object,
            policy.Object,
            includeDestination: false);
        using var response = await host.GetTestClient().GetAsync("/").WaitAsync(TestTimeout);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        forwarder.Verify(f => f.SendAsync(
            It.IsAny<HttpContext>(),
            It.IsAny<string>(),
            It.IsAny<HttpMessageInvoker>(),
            It.IsAny<ForwarderRequestConfig>(),
            It.IsAny<HttpTransformer>()), Times.Never);
        policy.Verify(p => p.RequestProxied(
            It.IsAny<HttpContext>(),
            It.IsAny<ClusterState>(),
            It.IsAny<DestinationState>()), Times.Never);
    }

    private static Mock<IPassiveHealthCheckPolicy> CreatePassivePolicy()
    {
        var policy = new Mock<IPassiveHealthCheckPolicy>();
        policy.SetupGet(p => p.Name).Returns(PassivePolicyName);
        return policy;
    }

    private static async ValueTask<ForwarderError> WaitForCancellationAsync(
        CancellationToken cancellationToken,
        TaskCompletionSource<bool> forwardingStarted,
        TaskCompletionSource<bool> forwardingCanceled)
    {
        forwardingStarted.SetResult(true);
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return ForwarderError.None;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            forwardingCanceled.SetResult(true);
            throw;
        }
    }

    private static async ValueTask<ForwarderError> CompleteWithErrorAsync(
        HttpContext context,
        List<string> order,
        Exception exception)
    {
        await Task.Yield();
        context.Response.StatusCode = StatusCodes.Status502BadGateway;
        context.Features.Set<IForwarderErrorFeature>(
            new ForwarderErrorFeature(ForwarderError.Request, exception));
        order.Add("forwarded");
        return ForwarderError.Request;
    }

    private static Task<IHost> CreateHostAsync(
        bool useDefaultPipeline,
        IHttpForwarder forwarder,
        IPassiveHealthCheckPolicy policy,
        bool includeDestination = true)
    {
        var routes = new[]
        {
            new RouteConfig
            {
                RouteId = "route1",
                ClusterId = "cluster1",
                Match = new RouteMatch { Path = "/{**catchall}" },
            }
        };
        var destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase);
        if (includeDestination)
        {
            destinations.Add("destination1", new DestinationConfig { Address = "http://localhost/" });
        }

        var clusters = new[]
        {
            new ClusterConfig
            {
                ClusterId = "cluster1",
                Destinations = destinations,
                HealthCheck = new HealthCheckConfig
                {
                    Passive = new PassiveHealthCheckConfig
                    {
                        Enabled = true,
                        Policy = PassivePolicyName,
                    }
                }
            }
        };

        return new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services =>
                {
                    services.AddReverseProxy().LoadFromMemory(routes, clusters);
                    services.AddSingleton(forwarder);
                    services.AddSingleton(policy);
                });
                webHost.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        if (useDefaultPipeline)
                        {
                            endpoints.MapReverseProxy();
                        }
                        else
                        {
                            endpoints.MapReverseProxy(proxyApp =>
                            {
                                proxyApp.UseLoadBalancing();
                                proxyApp.UsePassiveHealthChecks();
                            });
                        }
                    });
                });
            })
            .StartAsync();
    }
}
