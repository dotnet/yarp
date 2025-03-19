// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Yarp.Tests.Common;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.Forwarder;

namespace Yarp.ReverseProxy.Health.Tests;

public class TransportFailureRateHealthPolicyTests
{
    [Fact]
    public void RequestProxied_FailureRateLimitExceeded_MarkDestinationUnhealthy()
    {
        var options = Options.Create(
            new TransportFailureRateHealthPolicyOptions { DefaultFailureRateLimit = 0.5, DetectionWindowSize = TimeSpan.FromSeconds(30), MinimalTotalCountThreshold = 1 });
        var timeProvider = new TestTimeProvider(TimeSpan.FromMilliseconds(10000));
        var healthUpdater = new Mock<IDestinationHealthUpdater>();
        var policy = new TransportFailureRateHealthPolicy(options, timeProvider, healthUpdater.Object);
        Assert.Equal(HealthCheckConstants.PassivePolicy.TransportFailureRate, policy.Name);

        var reactivationPeriod0 = TimeSpan.FromSeconds(60);
        var reactivationPeriod1 = TimeSpan.FromSeconds(100);
        var cluster0 = GetClusterInfo("cluster0", destinationCount: 2);
        var cluster1 = GetClusterInfo("cluster1", destinationCount: 2, failureRateLimit: 0.61, reactivationPeriod1);

        // Initial state
        Assert.All(cluster0.Destinations.Values, d => Assert.Equal(DestinationHealth.Unknown, d.Health.Passive));
        Assert.All(cluster1.Destinations.Values, d => Assert.Equal(DestinationHealth.Unknown, d.Health.Passive));

        // Successful requests
        for (var i = 0; i < 3; i++)
        {
            policy.RequestProxied(new DefaultHttpContext(), cluster0, cluster0.Destinations.Values.First());
            policy.RequestProxied(new DefaultHttpContext(), cluster0, cluster0.Destinations.Values.Skip(1).First());
            policy.RequestProxied(new DefaultHttpContext(), cluster1, cluster1.Destinations.Values.First());
            policy.RequestProxied(new DefaultHttpContext(), cluster1, cluster1.Destinations.Values.Skip(1).First());
            timeProvider.Advance(TimeSpan.FromMilliseconds(4000));
        }

        healthUpdater.Verify(u => u.SetPassive(cluster0, cluster0.Destinations.Values.First(), DestinationHealth.Healthy, reactivationPeriod0), Times.Exactly(3));
        healthUpdater.Verify(u => u.SetPassive(cluster0, cluster0.Destinations.Values.Skip(1).First(), DestinationHealth.Healthy, reactivationPeriod0), Times.Exactly(3));
        healthUpdater.Verify(u => u.SetPassive(cluster1, cluster1.Destinations.Values.First(), DestinationHealth.Healthy, reactivationPeriod1), Times.Exactly(3));
        healthUpdater.Verify(u => u.SetPassive(cluster1, cluster1.Destinations.Values.Skip(1).First(), DestinationHealth.Healthy, reactivationPeriod1), Times.Exactly(3));
        healthUpdater.VerifyNoOtherCalls();

        // Failed requests
        for (var i = 0; i < 3; i++)
        {
            policy.RequestProxied(GetFailedRequestContext(ForwarderError.RequestTimedOut), cluster0, cluster0.Destinations.Values.Skip(1).First());
            policy.RequestProxied(GetFailedRequestContext(ForwarderError.Request), cluster1, cluster1.Destinations.Values.First());
            timeProvider.Advance(TimeSpan.FromMilliseconds(4000));
        }

        healthUpdater.Verify(u => u.SetPassive(cluster0, cluster0.Destinations.Values.Skip(1).First(), DestinationHealth.Healthy, reactivationPeriod0), Times.Exactly(5));
        healthUpdater.Verify(u => u.SetPassive(cluster0, cluster0.Destinations.Values.Skip(1).First(), DestinationHealth.Unhealthy, reactivationPeriod0), Times.Once);
        healthUpdater.Verify(u => u.SetPassive(cluster1, cluster1.Destinations.Values.First(), DestinationHealth.Healthy, reactivationPeriod1), Times.Exactly(6));
        healthUpdater.VerifyNoOtherCalls();

        // Two more failed requests
        policy.RequestProxied(GetFailedRequestContext(ForwarderError.Request), cluster1, cluster1.Destinations.Values.First());
        // End of the detection window
        timeProvider.Advance(TimeSpan.FromMilliseconds(6000));
        policy.RequestProxied(GetFailedRequestContext(ForwarderError.Request), cluster1, cluster1.Destinations.Values.First());

        healthUpdater.Verify(u => u.SetPassive(cluster1, cluster1.Destinations.Values.First(), DestinationHealth.Healthy, reactivationPeriod1), Times.Exactly(7));
        healthUpdater.Verify(u => u.SetPassive(cluster1, cluster1.Destinations.Values.First(), DestinationHealth.Unhealthy, reactivationPeriod1), Times.Once);
        healthUpdater.VerifyNoOtherCalls();
    }

    [Fact]
    public void RequestProxied_FailureMovedOutOfDetectionWindow_MarkDestinationHealthy()
    {
        var options = Options.Create(
            new TransportFailureRateHealthPolicyOptions { DefaultFailureRateLimit = 0.5, DetectionWindowSize = TimeSpan.FromSeconds(30), MinimalTotalCountThreshold = 1 });
        var timeProvider = new TestTimeProvider(TimeSpan.FromMilliseconds(10000));
        var healthUpdater = new Mock<IDestinationHealthUpdater>();
        var policy = new TransportFailureRateHealthPolicy(options, timeProvider, healthUpdater.Object);

        var cluster = GetClusterInfo("cluster0", destinationCount: 2);

        // Initial failed requests
        for (var i = 0; i < 2; i++)
        {
            policy.RequestProxied(GetFailedRequestContext(ForwarderError.RequestTimedOut), cluster, cluster.Destinations.Values.Skip(1).First());
            timeProvider.Advance(TimeSpan.FromMilliseconds(1000));
        }

        healthUpdater.Verify(u => u.SetPassive(cluster, cluster.Destinations.Values.Skip(1).First(), DestinationHealth.Unhealthy, TimeSpan.FromSeconds(60)), Times.Exactly(2));
        healthUpdater.VerifyNoOtherCalls();

        // Successful requests
        for (var i = 0; i < 4; i++)
        {
            policy.RequestProxied(new DefaultHttpContext(), cluster, cluster.Destinations.Values.First());
            policy.RequestProxied(new DefaultHttpContext(), cluster, cluster.Destinations.Values.Skip(1).First());
            timeProvider.Advance(TimeSpan.FromMilliseconds(5000));
        }

        healthUpdater.Verify(u => u.SetPassive(cluster, cluster.Destinations.Values.First(), DestinationHealth.Healthy, TimeSpan.FromSeconds(60)), Times.Exactly(4));
        healthUpdater.Verify(u => u.SetPassive(cluster, cluster.Destinations.Values.Skip(1).First(), DestinationHealth.Healthy, TimeSpan.FromSeconds(60)), Times.Exactly(2));
        healthUpdater.Verify(u => u.SetPassive(cluster, cluster.Destinations.Values.Skip(1).First(), DestinationHealth.Unhealthy, TimeSpan.FromSeconds(60)), Times.Exactly(4));
        healthUpdater.VerifyNoOtherCalls();

        // Failed requests
        for (var i = 0; i < 2; i++)
        {
            policy.RequestProxied(GetFailedRequestContext(ForwarderError.RequestTimedOut), cluster, cluster.Destinations.Values.Skip(1).First());
            timeProvider.Advance(TimeSpan.FromMilliseconds(1));
        }

        healthUpdater.Verify(u => u.SetPassive(cluster, cluster.Destinations.Values.Skip(1).First(), DestinationHealth.Healthy, TimeSpan.FromSeconds(60)), Times.Exactly(3));
        healthUpdater.Verify(u => u.SetPassive(cluster, cluster.Destinations.Values.Skip(1).First(), DestinationHealth.Unhealthy, TimeSpan.FromSeconds(60)), Times.Exactly(5));
        healthUpdater.VerifyNoOtherCalls();

        // Shift the detection window to the future
        timeProvider.Advance(TimeSpan.FromMilliseconds(10998));

        // New successful requests
        for (var i = 0; i < 2; i++)
        {
            policy.RequestProxied(new DefaultHttpContext(), cluster, cluster.Destinations.Values.Skip(1).First());
            timeProvider.Advance(TimeSpan.FromMilliseconds(1));
        }

        // New failed request, but 2 oldest failures have moved out of the detection window
        policy.RequestProxied(GetFailedRequestContext(ForwarderError.RequestTimedOut), cluster, cluster.Destinations.Values.Skip(1).First());

        healthUpdater.Verify(u => u.SetPassive(cluster, cluster.Destinations.Values.Skip(1).First(), DestinationHealth.Healthy, TimeSpan.FromSeconds(60)), Times.Exactly(6));
        healthUpdater.VerifyNoOtherCalls();
    }

    [Fact]
    public void RequestProxied_FailedAndReactivationLessDetection_UseDetectionPeriodForReactivation()
    {
        var options = Options.Create(
            new TransportFailureRateHealthPolicyOptions { DefaultFailureRateLimit = 0.5, DetectionWindowSize = TimeSpan.FromSeconds(30), MinimalTotalCountThreshold = 1 });
        var timeProvider = new TestTimeProvider(TimeSpan.FromMilliseconds(10000));
        var healthUpdater = new Mock<IDestinationHealthUpdater>();
        var policy = new TransportFailureRateHealthPolicy(options, timeProvider, healthUpdater.Object);

        var cluster = GetClusterInfo("cluster0", destinationCount: 2, reactivationPeriod: TimeSpan.FromSeconds(10));

        // Initial failed requests
        for (var i = 0; i < 2; i++)
        {
            policy.RequestProxied(GetFailedRequestContext(ForwarderError.RequestTimedOut), cluster, cluster.Destinations.Values.Skip(1).First());
            timeProvider.Advance(TimeSpan.FromMilliseconds(1000));
        }

        healthUpdater.Verify(u => u.SetPassive(cluster, cluster.Destinations.Values.Skip(1).First(), DestinationHealth.Unhealthy, TimeSpan.FromSeconds(30)), Times.Exactly(2));
        healthUpdater.VerifyNoOtherCalls();

        // Simulate a reactivation
        timeProvider.Advance(TimeSpan.FromMilliseconds(31000));
        cluster.Destinations.Values.Skip(1).First().Health.Passive = DestinationHealth.Unknown;

        // One successful request to the reactivated destination
        policy.RequestProxied(new DefaultHttpContext(), cluster, cluster.Destinations.Values.Skip(1).First());
        timeProvider.Advance(TimeSpan.FromMilliseconds(100));

        healthUpdater.Verify(u => u.SetPassive(cluster, cluster.Destinations.Values.Skip(1).First(), DestinationHealth.Healthy, TimeSpan.FromSeconds(30)), Times.Exactly(1));
        healthUpdater.Verify(u => u.SetPassive(cluster, cluster.Destinations.Values.Skip(1).First(), DestinationHealth.Unhealthy, TimeSpan.FromSeconds(30)), Times.Exactly(2));
        healthUpdater.VerifyNoOtherCalls();
    }

    [Fact]
    public void RequestProxied_MultipleConcurrentRequests_MarkDestinationUnhealthyAndHealthyAgain()
    {
        var options = Options.Create(
            new TransportFailureRateHealthPolicyOptions { DefaultFailureRateLimit = 0.5, DetectionWindowSize = TimeSpan.FromSeconds(30), MinimalTotalCountThreshold = 1 });
        var timeProvider = new TestTimeProvider(TimeSpan.FromMilliseconds(10000));
        var healthUpdater = new Mock<IDestinationHealthUpdater>();
        var reactivationPeriod = TimeSpan.FromSeconds(40);
        var policy = new TransportFailureRateHealthPolicy(options, timeProvider, healthUpdater.Object);

        var cluster = GetClusterInfo("cluster0", destinationCount: 2, reactivationPeriod: reactivationPeriod);

        // Initial state
        Assert.All(cluster.Destinations.Values, d => Assert.Equal(DestinationHealth.Unknown, d.Health.Passive));

        // Initial successful requests
        for (var i = 0; i < 2; i++)
        {
            policy.RequestProxied(new DefaultHttpContext(), cluster, cluster.Destinations.Values.Skip(1).First());
        }

        healthUpdater.Verify(u => u.SetPassive(cluster, cluster.Destinations.Values.Skip(1).First(), DestinationHealth.Healthy, reactivationPeriod), Times.Exactly(2));
        healthUpdater.VerifyNoOtherCalls();

        // Concurrent failed requests.
        // They are 'concurrent' because the timeProvider is not updated.
        for (var i = 0; i < 2; i++)
        {
            policy.RequestProxied(GetFailedRequestContext(ForwarderError.RequestTimedOut), cluster, cluster.Destinations.Values.Skip(1).First());
        }

        healthUpdater.Verify(u => u.SetPassive(cluster, cluster.Destinations.Values.Skip(1).First(), DestinationHealth.Healthy, reactivationPeriod), Times.Exactly(3));
        healthUpdater.Verify(u => u.SetPassive(cluster, cluster.Destinations.Values.Skip(1).First(), DestinationHealth.Unhealthy, reactivationPeriod), Times.Once);
        healthUpdater.VerifyNoOtherCalls();

        // More successful requests
        for (var i = 0; i < 2; i++)
        {
            policy.RequestProxied(new DefaultHttpContext(), cluster, cluster.Destinations.Values.Skip(1).First());
            timeProvider.Advance(TimeSpan.FromMilliseconds(100));
        }

        healthUpdater.Verify(u => u.SetPassive(cluster, cluster.Destinations.Values.Skip(1).First(), DestinationHealth.Healthy, reactivationPeriod), Times.Exactly(5));
        healthUpdater.Verify(u => u.SetPassive(cluster, cluster.Destinations.Values.Skip(1).First(), DestinationHealth.Unhealthy, reactivationPeriod), Times.Once);
        healthUpdater.VerifyNoOtherCalls();

        // More failed requests
        for (var i = 0; i < 2; i++)
        {
            policy.RequestProxied(GetFailedRequestContext(ForwarderError.RequestTimedOut), cluster, cluster.Destinations.Values.Skip(1).First());
            timeProvider.Advance(TimeSpan.FromMilliseconds(100));
        }

        healthUpdater.Verify(u => u.SetPassive(cluster, cluster.Destinations.Values.Skip(1).First(), DestinationHealth.Healthy, reactivationPeriod), Times.Exactly(6));
        healthUpdater.Verify(u => u.SetPassive(cluster, cluster.Destinations.Values.Skip(1).First(), DestinationHealth.Unhealthy, reactivationPeriod), Times.Exactly(2));
        healthUpdater.VerifyNoOtherCalls();

        policy.RequestProxied(new DefaultHttpContext(), cluster, cluster.Destinations.Values.First());

        healthUpdater.Verify(u => u.SetPassive(cluster, cluster.Destinations.Values.First(), DestinationHealth.Healthy, reactivationPeriod), Times.Once);
        healthUpdater.VerifyNoOtherCalls();
    }

    private HttpContext GetFailedRequestContext(ForwarderError error)
    {
        var errorFeature = new ForwarderErrorFeature(error, null);
        var context = new DefaultHttpContext();
        context.Features.Set<IForwarderErrorFeature>(errorFeature);
        return context;
    }

    private ClusterState GetClusterInfo(string id, int destinationCount, double? failureRateLimit = null, TimeSpan? reactivationPeriod = null)
    {
        var metadata = failureRateLimit is not null
            ? new Dictionary<string, string> { { TransportFailureRateHealthPolicyOptions.FailureRateLimitMetadataName, failureRateLimit?.ToString(CultureInfo.InvariantCulture) } }
            : null;
        var clusterModel = new ClusterModel(
            new ClusterConfig
            {
                ClusterId = id,
                HealthCheck = new HealthCheckConfig
                {
                    Passive = new PassiveHealthCheckConfig
                    {
                        Enabled = true,
                        Policy = "policy",
                        ReactivationPeriod = reactivationPeriod,
                    }
                },
                Metadata = metadata,
            },
            new HttpMessageInvoker(new HttpClientHandler()));
        var clusterState = new ClusterState(id);
        clusterState.Model = clusterModel;
        for (var i = 0; i < destinationCount; i++)
        {
            var destinationModel = new DestinationModel(new DestinationConfig { Address = $"https://localhost:1000{i}/{id}/", Health = $"https://localhost:2000{i}/{id}/" });
            var destinationId = $"destination{i}";
            clusterState.Destinations.GetOrAdd(destinationId, id => new DestinationState(id)
            {
                Model = destinationModel
            });
        }

        return clusterState;
    }
}
