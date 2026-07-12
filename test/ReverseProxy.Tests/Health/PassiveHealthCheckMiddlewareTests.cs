// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.Forwarder;

namespace Yarp.ReverseProxy.Health.Tests;

public class PassiveHealthCheckMiddlewareTests
{
    [Fact]
    public async Task Invoke_PassiveHealthCheckIsEnabled_CallPolicy()
    {
        var policies = new[] { GetPolicy("policy0"), GetPolicy("policy1") };
        var cluster0 = GetClusterInfo("cluster0", "policy0");
        var cluster1 = GetClusterInfo("cluster1", "policy1");
        var nextInvoked = false;
        var middleware = new PassiveHealthCheckMiddleware(c =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        }, policies.Select(p => p.Object));

        var context0 = GetContext(cluster0, selectedDestination: 1, error: null);
        await middleware.Invoke(context0);

        Assert.True(nextInvoked);
        policies[0].Verify(p => p.RequestProxied(context0, cluster0, cluster0.DestinationsState.AllDestinations[1]), Times.Once);
        policies[0].VerifyGet(p => p.Name, Times.Once);
        policies[0].VerifyNoOtherCalls();
        policies[1].VerifyGet(p => p.Name, Times.Once);
        policies[1].VerifyNoOtherCalls();

        nextInvoked = false;

        var error = new ForwarderErrorFeature(ForwarderError.Request, null);
        var context1 = GetContext(cluster1, selectedDestination: 0, error);
        await middleware.Invoke(context1);

        Assert.True(nextInvoked);
        policies[1].Verify(p => p.RequestProxied(context1, cluster1, cluster1.DestinationsState.AllDestinations[0]), Times.Once);
        policies[1].VerifyNoOtherCalls();
        policies[0].VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Invoke_PassiveHealthCheckIsDisabled_DoNothing()
    {
        var policies = new[] { GetPolicy("policy0"), GetPolicy("policy1") };
        var cluster0 = GetClusterInfo("cluster0", "policy0", enabled: false);
        var nextInvoked = false;
        var middleware = new PassiveHealthCheckMiddleware(c =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        }, policies.Select(p => p.Object));

        var context0 = GetContext(cluster0, selectedDestination: 0, error: null);
        await middleware.Invoke(context0);

        Assert.True(nextInvoked);
        policies[0].VerifyGet(p => p.Name, Times.Once);
        policies[0].VerifyNoOtherCalls();
        policies[1].VerifyGet(p => p.Name, Times.Once);
        policies[1].VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Invoke_PassiveHealthCheckIsEnabledButNoDestinationSelected_DoNothing()
    {
        var policies = new[] { GetPolicy("policy0"), GetPolicy("policy1") };
        var cluster0 = GetClusterInfo("cluster0", "policy0");
        var nextInvoked = false;
        var middleware = new PassiveHealthCheckMiddleware(c =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        }, policies.Select(p => p.Object));

        var context0 = GetContext(cluster0, selectedDestination: 1, error: null);
        context0.GetReverseProxyFeature().ProxiedDestination = null;
        await middleware.Invoke(context0);

        Assert.True(nextInvoked);
        policies[0].VerifyGet(p => p.Name, Times.Once);
        policies[0].VerifyNoOtherCalls();
        policies[1].VerifyGet(p => p.Name, Times.Once);
        policies[1].VerifyNoOtherCalls();
    }

    // --- Differential tests locking in ReassignProxyRequest / passive-health ordering semantics. ---
    // These document the behavior that must be preserved and pass on both the original middleware and
    // the fast-path optimized middleware (which reads whether passive health is engaged at entry, but
    // still records the outcome against the cluster/destination read AFTER _next).

    [Fact]
    public async Task Invoke_NextIsInvokedBeforePolicyRecording()
    {
        // Ordering: post-processing (health recording) must happen AFTER _next completes.
        var policies = new[] { GetPolicy("policy0") };
        var cluster0 = GetClusterInfo("cluster0", "policy0");
        var order = new List<string>();
        var middleware = new PassiveHealthCheckMiddleware(_ =>
        {
            order.Add("next");
            return Task.CompletedTask;
        }, policies.Select(p => p.Object));

        var context0 = GetContext(cluster0, selectedDestination: 1, error: null);
        policies[0].Setup(p => p.RequestProxied(It.IsAny<HttpContext>(), It.IsAny<ClusterState>(), It.IsAny<DestinationState>()))
            .Callback(() => order.Add("recorded"));

        await middleware.Invoke(context0);

        Assert.Equal(new[] { "next", "recorded" }, order);
    }

    [Fact]
    public async Task Invoke_EnabledAtEntry_ReassignedToAnotherEnabledClusterDuringNext_RecordsAgainstFinalCluster()
    {
        // Preserves ReassignProxyRequest semantics: when the request is reassigned to a different,
        // also-enabled cluster during downstream processing, the outcome is recorded against the
        // FINAL cluster/destination (read after _next) using the final cluster's policy.
        var policies = new[] { GetPolicy("policy0"), GetPolicy("policy1") };
        var cluster0 = GetClusterInfo("cluster0", "policy0");
        var cluster1 = GetClusterInfo("cluster1", "policy1");

        var context = GetContext(cluster0, selectedDestination: 0, error: null);

        var middleware = new PassiveHealthCheckMiddleware(ctx =>
        {
            // Simulate a downstream ReassignProxyRequest to a different, also-enabled cluster.
            ctx.Features.Set<IReverseProxyFeature>(GetProxyFeature(cluster1, cluster1.DestinationsState.AllDestinations[1]));
            return Task.CompletedTask;
        }, policies.Select(p => p.Object));

        await middleware.Invoke(context);

        policies[1].Verify(p => p.RequestProxied(context, cluster1, cluster1.DestinationsState.AllDestinations[1]), Times.Once);
        policies[1].VerifyGet(p => p.Name, Times.Once);
        policies[1].VerifyNoOtherCalls();
        // The entry cluster's policy must NOT be used for recording.
        policies[0].VerifyGet(p => p.Name, Times.Once);
        policies[0].VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Invoke_EnabledAtEntry_ReassignedToDisabledClusterDuringNext_DoesNotRecord()
    {
        // Preserves semantics: if the final (reassigned) cluster has passive health disabled, nothing
        // is recorded even though the entry cluster had it enabled.
        var policies = new[] { GetPolicy("policy0"), GetPolicy("policy1") };
        var cluster0 = GetClusterInfo("cluster0", "policy0");
        var cluster1 = GetClusterInfo("cluster1", "policy1", enabled: false);

        var context = GetContext(cluster0, selectedDestination: 0, error: null);

        var middleware = new PassiveHealthCheckMiddleware(ctx =>
        {
            ctx.Features.Set<IReverseProxyFeature>(GetProxyFeature(cluster1, cluster1.DestinationsState.AllDestinations[1]));
            return Task.CompletedTask;
        }, policies.Select(p => p.Object));

        await middleware.Invoke(context);

        policies[0].VerifyGet(p => p.Name, Times.Once);
        policies[0].VerifyNoOtherCalls();
        policies[1].VerifyGet(p => p.Name, Times.Once);
        policies[1].VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Invoke_DisabledAtEntry_InvokesNextAndDoesNotRecord()
    {
        // Fast path: passive health disabled at entry -> _next is invoked, nothing recorded.
        // (This is the allocation-free path; it matches the documented "reassign first" usage where
        // any cluster change has already happened before this middleware runs.)
        var policies = new[] { GetPolicy("policy0"), GetPolicy("policy1") };
        var cluster0 = GetClusterInfo("cluster0", "policy0", enabled: false);
        var nextInvoked = false;
        var middleware = new PassiveHealthCheckMiddleware(_ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        }, policies.Select(p => p.Object));

        var context0 = GetContext(cluster0, selectedDestination: 0, error: null);
        await middleware.Invoke(context0);

        Assert.True(nextInvoked);
        policies[0].VerifyGet(p => p.Name, Times.Once);
        policies[0].VerifyNoOtherCalls();
        policies[1].VerifyGet(p => p.Name, Times.Once);
        policies[1].VerifyNoOtherCalls();
    }

    private HttpContext GetContext(ClusterState cluster, int selectedDestination, IForwarderErrorFeature error)
    {
        var context = new DefaultHttpContext();
        context.Features.Set(GetProxyFeature(cluster, cluster.DestinationsState.AllDestinations[selectedDestination]));
        context.Features.Set(error);
        return context;
    }

    private Mock<IPassiveHealthCheckPolicy> GetPolicy(string name)
    {
        var policy = new Mock<IPassiveHealthCheckPolicy>();
        policy.SetupGet(p => p.Name).Returns(name);
        return policy;
    }

    private IReverseProxyFeature GetProxyFeature(ClusterState clusterState, DestinationState destination)
    {
        return new ReverseProxyFeature()
        {
            ProxiedDestination = destination,
            Cluster = clusterState.Model,
            Route = new RouteModel(new RouteConfig(), clusterState, HttpTransformer.Default),
        };
    }

    private ClusterState GetClusterInfo(string id, string policy, bool enabled = true)
    {
        var clusterModel = new ClusterModel(
            new ClusterConfig
            {
                ClusterId = id,
                HealthCheck = new HealthCheckConfig
                {
                    Passive = new PassiveHealthCheckConfig
                    {
                        Enabled = enabled,
                        Policy = policy,
                    }
                }
            },
            new HttpMessageInvoker(new HttpClientHandler()));
        var clusterState = new ClusterState(id);
        clusterState.Model = clusterModel;
        clusterState.Destinations.GetOrAdd("destination0", id => new DestinationState(id));
        clusterState.Destinations.GetOrAdd("destination1", id => new DestinationState(id));

        clusterState.DestinationsState = new ClusterDestinationsState(clusterState.Destinations.Values.ToList(), clusterState.Destinations.Values.ToList());

        return clusterState;
    }
}
