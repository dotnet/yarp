// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Options;
using Xunit;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Model;

namespace Yarp.ReverseProxy.Health.Tests;

public class ConsecutiveFailuresHealthPolicyTests
{
    [Fact]
    public void ProbingCompleted_FailureThresholdExceeded_MarkDestinationUnhealthy()
    {
        var options = Options.Create(new ConsecutiveFailuresHealthPolicyOptions { DefaultThreshold = 2 });
        var policy = new ConsecutiveFailuresHealthPolicy(options, new DestinationHealthUpdaterStub());
        var cluster0 = GetClusterInfo("cluster0", destinationCount: 2);
        var cluster1 = GetClusterInfo("cluster1", destinationCount: 2, failureThreshold: 3);

        var probingResults0 = new[] {
            new DestinationProbingResult(cluster0.Destinations.Values.First(), new HttpResponseMessage(HttpStatusCode.InternalServerError), null),
            new DestinationProbingResult(cluster0.Destinations.Values.Skip(1).First(), new HttpResponseMessage(HttpStatusCode.OK), null)
        };
        var probingResults1 = new[] {
            new DestinationProbingResult(cluster1.Destinations.Values.First(), new HttpResponseMessage(HttpStatusCode.OK), null),
            new DestinationProbingResult(cluster1.Destinations.Values.Skip(1).First(), null, new InvalidOperationException())
        };

        Assert.Equal(HealthCheckConstants.ActivePolicy.ConsecutiveFailures, policy.Name);

        // Initial state
        Assert.All(cluster0.Destinations.Values, d => Assert.Equal(DestinationHealth.Unknown, d.Health.Active));
        Assert.All(cluster1.Destinations.Values, d => Assert.Equal(DestinationHealth.Unknown, d.Health.Active));

        // First probing attempt
        policy.ProbingCompleted(cluster0, probingResults0);
        Assert.Equal(DestinationHealth.Unknown, cluster0.Destinations.Values.First().Health.Active);
        Assert.Equal(DestinationHealth.Healthy, cluster0.Destinations.Values.Skip(1).First().Health.Active);
        policy.ProbingCompleted(cluster1, probingResults1);
        Assert.Equal(DestinationHealth.Healthy, cluster1.Destinations.Values.First().Health.Active);
        Assert.Equal(DestinationHealth.Unknown, cluster1.Destinations.Values.Skip(1).First().Health.Active);

        // Second probing attempt
        policy.ProbingCompleted(cluster0, probingResults0);
        Assert.Equal(DestinationHealth.Unhealthy, cluster0.Destinations.Values.First().Health.Active);
        Assert.Equal(DestinationHealth.Healthy, cluster0.Destinations.Values.Skip(1).First().Health.Active);
        policy.ProbingCompleted(cluster1, probingResults1);
        Assert.Equal(DestinationHealth.Healthy, cluster1.Destinations.Values.First().Health.Active);
        Assert.Equal(DestinationHealth.Unknown, cluster1.Destinations.Values.Skip(1).First().Health.Active);

        // Third probing attempt
        policy.ProbingCompleted(cluster0, probingResults0);
        Assert.Equal(DestinationHealth.Unhealthy, cluster0.Destinations.Values.First().Health.Active);
        Assert.Equal(DestinationHealth.Healthy, cluster0.Destinations.Values.Skip(1).First().Health.Active);
        policy.ProbingCompleted(cluster1, probingResults1);
        Assert.Equal(DestinationHealth.Healthy, cluster1.Destinations.Values.First().Health.Active);
        Assert.Equal(DestinationHealth.Unhealthy, cluster1.Destinations.Values.Skip(1).First().Health.Active);

        Assert.All(cluster0.Destinations.Values, d => Assert.Equal(DestinationHealth.Unknown, d.Health.Passive));
        Assert.All(cluster1.Destinations.Values, d => Assert.Equal(DestinationHealth.Unknown, d.Health.Passive));
    }

    [Fact]
    public void ProbingCompleted_SuccessfulResponse_MarkDestinationHealthy()
    {
        var options = Options.Create(new ConsecutiveFailuresHealthPolicyOptions { DefaultThreshold = 2 });
        var policy = new ConsecutiveFailuresHealthPolicy(options, new DestinationHealthUpdaterStub());
        var cluster = GetClusterInfo("cluster0", destinationCount: 2);

        var probingResults = new[] {
            new DestinationProbingResult(cluster.Destinations.Values.First(), new HttpResponseMessage(HttpStatusCode.InternalServerError), null),
            new DestinationProbingResult(cluster.Destinations.Values.Skip(1).First(), new HttpResponseMessage(HttpStatusCode.OK), null)
        };

        for (var i = 0; i < 2; i++)
        {
            policy.ProbingCompleted(cluster, probingResults);
        }

        Assert.Equal(DestinationHealth.Unhealthy, cluster.Destinations.Values.First().Health.Active);
        Assert.Equal(DestinationHealth.Healthy, cluster.Destinations.Values.Skip(1).First().Health.Active);

        policy.ProbingCompleted(cluster, new[] { new DestinationProbingResult(cluster.Destinations.Values.First(), new HttpResponseMessage(HttpStatusCode.OK), null) });

        Assert.Equal(DestinationHealth.Healthy, cluster.Destinations.Values.First().Health.Active);
        Assert.Equal(DestinationHealth.Healthy, cluster.Destinations.Values.Skip(1).First().Health.Active);

        Assert.All(cluster.Destinations.Values, d => Assert.Equal(DestinationHealth.Unknown, d.Health.Passive));
    }

    [Fact]
    public void ProbingCompleted_EmptyProbingResultList_DoNothing()
    {
        var options = Options.Create(new ConsecutiveFailuresHealthPolicyOptions { DefaultThreshold = 2 });
        var policy = new ConsecutiveFailuresHealthPolicy(options, new DestinationHealthUpdaterStub());
        var cluster = GetClusterInfo("cluster0", destinationCount: 2);

        var probingResults = new[] {
            new DestinationProbingResult(cluster.Destinations.Values.First(), new HttpResponseMessage(HttpStatusCode.InternalServerError), null),
            new DestinationProbingResult(cluster.Destinations.Values.Skip(1).First(), new HttpResponseMessage(HttpStatusCode.OK), null)
        };

        for (var i = 0; i < 2; i++)
        {
            policy.ProbingCompleted(cluster, probingResults);
        }

        Assert.Equal(DestinationHealth.Unhealthy, cluster.Destinations.Values.First().Health.Active);
        Assert.Equal(DestinationHealth.Healthy, cluster.Destinations.Values.Skip(1).First().Health.Active);

        policy.ProbingCompleted(cluster, Array.Empty<DestinationProbingResult>());

        Assert.Equal(DestinationHealth.Unhealthy, cluster.Destinations.Values.First().Health.Active);
        Assert.Equal(DestinationHealth.Healthy, cluster.Destinations.Values.Skip(1).First().Health.Active);
    }

    private ClusterState GetClusterInfo(string id, int destinationCount, int? failureThreshold = null)
    {
        var metadata = failureThreshold is not null
            ? new Dictionary<string, string> { { ConsecutiveFailuresHealthPolicyOptions.ThresholdMetadataName, failureThreshold.ToString() } }
            : null;
        var clusterModel = new ClusterModel(
            new ClusterConfig
            {
                ClusterId = id,
                HealthCheck = new HealthCheckConfig()
                {
                    Active = new ActiveHealthCheckConfig
                    {
                        Enabled = true,
                        Policy = "policy",
                        Path = "/api/health/",
                    },
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

        clusterState.DestinationsState = new ClusterDestinationsState(clusterState.Destinations.Values.ToList(), clusterState.Destinations.Values.ToList());

        return clusterState;
    }

    private class DestinationHealthUpdaterStub : IDestinationHealthUpdater
    {
        public void SetActive(ClusterState cluster, IEnumerable<NewActiveDestinationHealth> newHealthStates)
        {
            foreach (var newHealthState in newHealthStates)
            {
                newHealthState.Destination.Health.Active = newHealthState.NewActiveHealth;
            }

            var destinations = cluster.Destinations.Values.ToList();
            cluster.DestinationsState = new ClusterDestinationsState(destinations, destinations);
        }

        public void SetPassive(ClusterState cluster, DestinationState destination, DestinationHealth newHealth, TimeSpan reactivationPeriod)
        {
            throw new NotImplementedException();
        }
    }
}
