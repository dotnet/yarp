// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Yarp.Tests.Common;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Model;

namespace Yarp.ReverseProxy.Health.Tests;

public class ActiveHealthCheckMonitorTests
{
    private readonly TimeSpan Interval0 = TimeSpan.FromSeconds(10);
    private readonly TimeSpan Interval1 = TimeSpan.FromSeconds(20);

    [Fact]
    public async Task CheckHealthAsync_ActiveHealthCheckIsEnabledForCluster_SendProbe()
    {
        var policy0 = new Mock<IActiveHealthCheckPolicy>();
        policy0.SetupGet(p => p.Name).Returns("policy0");
        var policy1 = new Mock<IActiveHealthCheckPolicy>();
        policy1.SetupGet(p => p.Name).Returns("policy1");
        var options = Options.Create(new ActiveHealthCheckMonitorOptions { DefaultInterval = TimeSpan.FromSeconds(60), DefaultTimeout = TimeSpan.FromSeconds(5) });
        var clusters = new List<ClusterState>();
        var monitor = new ActiveHealthCheckMonitor(options, new[] { policy0.Object, policy1.Object }, new DefaultProbingRequestFactory(), new Mock<TimeProvider>().Object, GetLogger());

        var httpClient0 = GetHttpClient();
        var cluster0 = GetClusterInfo("cluster0", "policy0", true, httpClient0.Object);
        clusters.Add(cluster0);
        var httpClient1 = GetHttpClient();
        var cluster1 = GetClusterInfo("cluster1", "policy0", false, httpClient1.Object);
        clusters.Add(cluster1);
        var httpClient2 = GetHttpClient();
        var cluster2 = GetClusterInfo("cluster2", "policy1", true, httpClient2.Object);
        clusters.Add(cluster2);

        Assert.False(monitor.InitialProbeCompleted);

        await monitor.CheckHealthAsync(clusters);

        Assert.True(monitor.InitialProbeCompleted);

        VerifySentProbeAndResult(cluster0, httpClient0, policy0, new[] { ("https://localhost:20000/cluster0/api/health/", 1), ("https://localhost:20001/cluster0/api/health/", 1) });

        httpClient1.Verify(c => c.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()), Times.Never);

        VerifySentProbeAndResult(cluster2, httpClient2, policy1, new[] { ("https://localhost:20000/cluster2/api/health/", 1), ("https://localhost:20001/cluster2/api/health/", 1) });
    }

    [Theory]
    [InlineData(false)] // Test old API (CreateRequest with Models) via default implementation
    [InlineData(true)]  // Test new API (CreateRequestAsync with State)
    public async Task CheckHealthAsync_CustomUserAgentSpecified_UserAgentUnchanged(bool overrideAsyncMethod)
    {
        var policy = new Mock<IActiveHealthCheckPolicy>();
        policy.SetupGet(p => p.Name).Returns("policy");

        var requestFactory = new Mock<IProbingRequestFactory>();

        HttpRequestMessage CreateCustomRequest()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "https://localhost:20000/cluster/api/health/");
            request.Headers.UserAgent.ParseAdd("FooBar/9001");
            return request;
        }

        if (overrideAsyncMethod)
        {
            requestFactory.Setup(p => p.CreateRequestAsync(It.IsAny<ClusterState>(), It.IsAny<DestinationState>(), It.IsAny<CancellationToken>()))
                .Returns(() => ValueTask.FromResult(CreateCustomRequest()));
        }
        else
        {
            // Test the old API - the default implementation of CreateRequestAsync should call this
            requestFactory.Setup(p => p.CreateRequest(It.IsAny<ClusterModel>(), It.IsAny<DestinationModel>()))
                .Returns(CreateCustomRequest);

            // Use default interface implementation for CreateRequestAsync
            requestFactory.CallBase = true;
        }

        var options = Options.Create(new ActiveHealthCheckMonitorOptions());
        var monitor = new ActiveHealthCheckMonitor(options, new[] { policy.Object }, requestFactory.Object, new Mock<TimeProvider>().Object, GetLogger());

        var httpClient = GetHttpClient();
        var cluster = GetClusterInfo("cluster", "policy", true, httpClient.Object, destinationCount: 1);

        await monitor.CheckHealthAsync(new[] { cluster });

        VerifySentProbeAndResult(cluster, httpClient, policy, new[] { ("https://localhost:20000/cluster/api/health/", 1) }, userAgent: @"^FooBar\/9001$");
    }

    [Fact]
    public async Task CheckHealthAsync_FactoryCancelledExternally_ProbePassedToPolicyWithException()
    {
        var policy = new Mock<IActiveHealthCheckPolicy>();
        policy.SetupGet(p => p.Name).Returns("policy");

        var externalCts = new CancellationTokenSource();
        var requestFactory = new Mock<IProbingRequestFactory>();

        // First destination: factory throws OperationCanceledException (external cancellation)
        // Second destination: succeeds normally
        var callCount = 0;
        requestFactory.Setup(p => p.CreateRequestAsync(It.IsAny<ClusterState>(), It.IsAny<DestinationState>(), It.IsAny<CancellationToken>()))
            .Returns<ClusterState, DestinationState, CancellationToken>((cluster, destination, ct) =>
            {
                callCount++;
                if (callCount == 1)
                {
                    // Simulate external cancellation (not timeout) - throw without the timeout CTS being cancelled
                    throw new OperationCanceledException(externalCts.Token);
                }

                return ValueTask.FromResult(new HttpRequestMessage(HttpMethod.Get, $"https://localhost:20000/{destination.DestinationId}/health/"));
            });

        var options = Options.Create(new ActiveHealthCheckMonitorOptions { DefaultTimeout = TimeSpan.FromSeconds(30) });
        var monitor = new ActiveHealthCheckMonitor(options, new[] { policy.Object }, requestFactory.Object, new Mock<TimeProvider>().Object, GetLogger());

        var httpClient = GetHttpClient();
        var cluster = GetClusterInfo("cluster", "policy", true, httpClient.Object, destinationCount: 2);

        await monitor.CheckHealthAsync(new[] { cluster });

        // Policy should receive 2 results: one with exception (cancelled), one successful
        policy.Verify(
            p => p.ProbingCompleted(
                cluster,
                It.Is<IReadOnlyList<DestinationProbingResult>>(r =>
                    r.Count == 2 &&
                    r.Any(x => x.Exception is OperationCanceledException) &&
                    r.Any(x => x.Response != null && x.Response.StatusCode == HttpStatusCode.OK)))
            , Times.Once);
        policy.Verify(p => p.Name);
        policy.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CheckHealthAsync_SendAsyncCancelledExternally_ProbePassedToPolicyWithException()
    {
        var policy = new Mock<IActiveHealthCheckPolicy>();
        policy.SetupGet(p => p.Name).Returns("policy");

        var externalCts = new CancellationTokenSource();
        var options = Options.Create(new ActiveHealthCheckMonitorOptions { DefaultTimeout = TimeSpan.FromSeconds(30) });
        var monitor = new ActiveHealthCheckMonitor(options, new[] { policy.Object }, new DefaultProbingRequestFactory(), new Mock<TimeProvider>().Object, GetLogger());

        // First destination: SendAsync throws OperationCanceledException (external cancellation)
        // Second destination: succeeds normally
        var callCount = 0;
        var httpClient = new Mock<HttpMessageInvoker>(() => new HttpMessageInvoker(new Mock<HttpMessageHandler>().Object));
        httpClient.Setup(c => c.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
            .Returns<HttpRequestMessage, CancellationToken>((request, ct) =>
            {
                callCount++;
                if (callCount == 1)
                {
                    // Simulate external cancellation (not timeout) - throw without the timeout CTS being cancelled
                    throw new OperationCanceledException(externalCts.Token);
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Version = request.Version });
            });

        var cluster = GetClusterInfo("cluster", "policy", true, httpClient.Object, destinationCount: 2);

        await monitor.CheckHealthAsync(new[] { cluster });

        // Policy should receive 2 results: one with exception (cancelled), one successful
        policy.Verify(
            p => p.ProbingCompleted(
                cluster,
                It.Is<IReadOnlyList<DestinationProbingResult>>(r =>
                    r.Count == 2 &&
                    r.Any(x => x.Exception is OperationCanceledException) &&
                    r.Any(x => x.Response != null && x.Response.StatusCode == HttpStatusCode.OK)))
            , Times.Once);
        policy.Verify(p => p.Name);
        policy.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CheckHealthAsync_TimeoutCancellation_TreatedAsError()
    {
        var policy = new Mock<IActiveHealthCheckPolicy>();
        policy.SetupGet(p => p.Name).Returns("policy");

        var options = Options.Create(new ActiveHealthCheckMonitorOptions { DefaultTimeout = TimeSpan.FromMilliseconds(1) });
        var monitor = new ActiveHealthCheckMonitor(options, new[] { policy.Object }, new DefaultProbingRequestFactory(), new Mock<TimeProvider>().Object, GetLogger());

        var tcs = new TaskCompletionSource<HttpResponseMessage>();
        var httpClient = new Mock<HttpMessageInvoker>(() => new HttpMessageInvoker(new Mock<HttpMessageHandler>().Object));
        httpClient.Setup(c => c.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
            .Returns<HttpRequestMessage, CancellationToken>((request, ct) =>
            {
                // Register callback to cancel the TCS when timeout occurs
                ct.Register(() => tcs.TrySetCanceled(ct));
                return tcs.Task;
            });

        var cluster = GetClusterInfo("cluster", "policy", true, httpClient.Object, destinationCount: 1);

        await monitor.CheckHealthAsync(new[] { cluster });

        // Policy should receive 1 result with an exception (timeout is an error, not skipped)
        policy.Verify(
            p => p.ProbingCompleted(
                cluster,
                It.Is<IReadOnlyList<DestinationProbingResult>>(r =>
                    r.Count == 1 &&
                    r[0].Response == null &&
                    r[0].Exception is OperationCanceledException)),
            Times.Once);
        policy.Verify(p => p.Name);
        policy.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ProbeCluster_ProbingTimerFired_SendProbesAndReceiveResponses()
    {
        var policy0 = new Mock<IActiveHealthCheckPolicy>();
        policy0.SetupGet(p => p.Name).Returns("policy0");
        var policy1 = new Mock<IActiveHealthCheckPolicy>();
        policy1.SetupGet(p => p.Name).Returns("policy1");
        var options = Options.Create(new ActiveHealthCheckMonitorOptions { DefaultInterval = TimeSpan.FromSeconds(60), DefaultTimeout = TimeSpan.FromSeconds(5) });
        var timeProvider = new TestTimeProvider();
        var monitor = new ActiveHealthCheckMonitor(options, new[] { policy0.Object, policy1.Object }, new DefaultProbingRequestFactory(), timeProvider, GetLogger());

        var httpClient0 = GetHttpClient();
        var cluster0 = GetClusterInfo("cluster0", "policy0", true, httpClient0.Object, Interval0);
        monitor.OnClusterAdded(cluster0);
        var httpClient2 = GetHttpClient();
        var cluster2 = GetClusterInfo("cluster2", "policy1", true, httpClient2.Object, Interval1);
        monitor.OnClusterAdded(cluster2);

        Assert.False(monitor.InitialProbeCompleted);

        await monitor.CheckHealthAsync(Array.Empty<ClusterState>());

        Assert.True(monitor.InitialProbeCompleted);

        timeProvider.FireAllTimers();

        Assert.Equal(2, timeProvider.TimerCount);
        timeProvider.VerifyTimer(0, Interval0);
        timeProvider.VerifyTimer(1, Interval1);
        VerifySentProbeAndResult(cluster0, httpClient0, policy0, new[] { ("https://localhost:20000/cluster0/api/health/", 1), ("https://localhost:20001/cluster0/api/health/", 1) }, policyCallTimes: 1);
        VerifySentProbeAndResult(cluster2, httpClient2, policy1, new[] { ("https://localhost:20000/cluster2/api/health/", 1), ("https://localhost:20001/cluster2/api/health/", 1) }, policyCallTimes: 1);

        GC.KeepAlive(monitor); // The timer does not keep a strong reference to the scheduler
    }

    [Fact]
    public async Task ProbeCluster_ClusterRemoved_StopSendingProbes()
    {
        var policy0 = new Mock<IActiveHealthCheckPolicy>();
        policy0.SetupGet(p => p.Name).Returns("policy0");
        var policy1 = new Mock<IActiveHealthCheckPolicy>();
        policy1.SetupGet(p => p.Name).Returns("policy1");
        var options = Options.Create(new ActiveHealthCheckMonitorOptions { DefaultInterval = TimeSpan.FromSeconds(60), DefaultTimeout = TimeSpan.FromSeconds(5) });
        var timeProvider = new TestTimeProvider();
        var monitor = new ActiveHealthCheckMonitor(options, new[] { policy0.Object, policy1.Object }, new DefaultProbingRequestFactory(), timeProvider, GetLogger());

        var httpClient0 = GetHttpClient();
        var cluster0 = GetClusterInfo("cluster0", "policy0", true, httpClient0.Object, interval: Interval0);
        monitor.OnClusterAdded(cluster0);
        var httpClient2 = GetHttpClient();
        var cluster2 = GetClusterInfo("cluster2", "policy1", true, httpClient2.Object, interval: Interval1);
        monitor.OnClusterAdded(cluster2);

        Assert.False(monitor.InitialProbeCompleted);

        await monitor.CheckHealthAsync(Array.Empty<ClusterState>());

        Assert.True(monitor.InitialProbeCompleted);

        timeProvider.FireAllTimers();

        Assert.Equal(2, timeProvider.TimerCount);
        timeProvider.VerifyTimer(0, Interval0);
        timeProvider.VerifyTimer(1, Interval1);
        VerifySentProbeAndResult(cluster0, httpClient0, policy0, new[] { ("https://localhost:20000/cluster0/api/health/", 1), ("https://localhost:20001/cluster0/api/health/", 1) }, policyCallTimes: 1);
        VerifySentProbeAndResult(cluster2, httpClient2, policy1, new[] { ("https://localhost:20000/cluster2/api/health/", 1), ("https://localhost:20001/cluster2/api/health/", 1) }, policyCallTimes: 1);

        monitor.OnClusterRemoved(cluster2);

        timeProvider.FireTimer(0);

        timeProvider.AssertTimerDisposed(1);

        VerifySentProbeAndResult(cluster0, httpClient0, policy0, new[] { ("https://localhost:20000/cluster0/api/health/", 2), ("https://localhost:20001/cluster0/api/health/", 2) }, policyCallTimes: 2);
        VerifySentProbeAndResult(cluster2, httpClient2, policy1, new[] { ("https://localhost:20000/cluster2/api/health/", 1), ("https://localhost:20001/cluster2/api/health/", 1) }, policyCallTimes: 1);

        GC.KeepAlive(monitor); // The timer does not keep a strong reference to the scheduler
    }

    [Fact]
    public async Task ProbeCluster_ClusterAdded_StartSendingProbes()
    {
        var policy0 = new Mock<IActiveHealthCheckPolicy>();
        policy0.SetupGet(p => p.Name).Returns("policy0");
        var policy1 = new Mock<IActiveHealthCheckPolicy>();
        policy1.SetupGet(p => p.Name).Returns("policy1");
        var options = Options.Create(new ActiveHealthCheckMonitorOptions { DefaultInterval = TimeSpan.FromSeconds(60), DefaultTimeout = TimeSpan.FromSeconds(5) });
        var timeProvider = new TestTimeProvider();
        var monitor = new ActiveHealthCheckMonitor(options, new[] { policy0.Object, policy1.Object }, new DefaultProbingRequestFactory(), timeProvider, GetLogger());

        var httpClient0 = GetHttpClient();
        var cluster0 = GetClusterInfo("cluster0", "policy0", true, httpClient0.Object, interval: Interval0);
        monitor.OnClusterAdded(cluster0);

        Assert.False(monitor.InitialProbeCompleted);

        await monitor.CheckHealthAsync(Array.Empty<ClusterState>());

        Assert.True(monitor.InitialProbeCompleted);

        timeProvider.FireAllTimers();

        Assert.Equal(1, timeProvider.TimerCount);
        timeProvider.VerifyTimer(0, Interval0);
        VerifySentProbeAndResult(cluster0, httpClient0, policy0, new[] { ("https://localhost:20000/cluster0/api/health/", 1), ("https://localhost:20001/cluster0/api/health/", 1) }, policyCallTimes: 1);

        var httpClient2 = GetHttpClient();
        var cluster2 = GetClusterInfo("cluster2", "policy1", true, httpClient2.Object, interval: Interval1);
        monitor.OnClusterAdded(cluster2);

        timeProvider.FireAllTimers();

        Assert.Equal(2, timeProvider.TimerCount);
        timeProvider.VerifyTimer(0, Interval0);
        timeProvider.VerifyTimer(1, Interval1);
        VerifySentProbeAndResult(cluster0, httpClient0, policy0, new[] { ("https://localhost:20000/cluster0/api/health/", 2), ("https://localhost:20001/cluster0/api/health/", 2) }, policyCallTimes: 2);
        VerifySentProbeAndResult(cluster2, httpClient2, policy1, new[] { ("https://localhost:20000/cluster2/api/health/", 1), ("https://localhost:20001/cluster2/api/health/", 1) }, policyCallTimes: 1);

        GC.KeepAlive(monitor); // The timer does not keep a strong reference to the scheduler
    }

    [Fact]
    public async Task ProbeCluster_ClusterChanged_SendProbesToNewHealthEndpoint()
    {
        var policy0 = new Mock<IActiveHealthCheckPolicy>();
        policy0.SetupGet(p => p.Name).Returns("policy0");
        var policy1 = new Mock<IActiveHealthCheckPolicy>();
        policy1.SetupGet(p => p.Name).Returns("policy1");
        var options = Options.Create(new ActiveHealthCheckMonitorOptions { DefaultInterval = TimeSpan.FromSeconds(60), DefaultTimeout = TimeSpan.FromSeconds(5) });
        var timeProvider = new TestTimeProvider();
        var monitor = new ActiveHealthCheckMonitor(options, new[] { policy0.Object, policy1.Object }, new DefaultProbingRequestFactory(), timeProvider, GetLogger());

        var httpClient0 = GetHttpClient();
        var cluster0 = GetClusterInfo("cluster0", "policy0", true, httpClient0.Object, interval: Interval0);
        monitor.OnClusterAdded(cluster0);
        var httpClient2 = GetHttpClient();
        var cluster2 = GetClusterInfo("cluster2", "policy1", true, httpClient2.Object, interval: Interval1);
        monitor.OnClusterAdded(cluster2);

        Assert.False(monitor.InitialProbeCompleted);

        await monitor.CheckHealthAsync(Array.Empty<ClusterState>());

        Assert.True(monitor.InitialProbeCompleted);

        timeProvider.FireAllTimers();

        Assert.Equal(2, timeProvider.TimerCount);
        timeProvider.VerifyTimer(0, Interval0);
        timeProvider.VerifyTimer(1, Interval1);
        VerifySentProbeAndResult(cluster0, httpClient0, policy0, new[] { ("https://localhost:20000/cluster0/api/health/", 1), ("https://localhost:20001/cluster0/api/health/", 1) }, policyCallTimes: 1);
        VerifySentProbeAndResult(cluster2, httpClient2, policy1, new[] { ("https://localhost:20000/cluster2/api/health/", 1), ("https://localhost:20001/cluster2/api/health/", 1) }, policyCallTimes: 1);

        foreach (var destination in cluster2.Destinations.Values)
        {
            var d = cluster2.Destinations.GetOrAdd(destination.DestinationId, id => new DestinationState(id));
            d.Model = new DestinationModel(new DestinationConfig { Address = destination.Model.Config.Address });
        }

        monitor.OnClusterChanged(cluster2);

        timeProvider.FireAllTimers();

        Assert.Equal(2, timeProvider.TimerCount);
        timeProvider.VerifyTimer(0, Interval0);
        timeProvider.VerifyTimer(1, Interval1);
        VerifySentProbeAndResult(cluster0, httpClient0, policy0, new[] { ("https://localhost:20000/cluster0/api/health/", 2), ("https://localhost:20001/cluster0/api/health/", 2) }, policyCallTimes: 2);
        VerifySentProbeAndResult(cluster2, httpClient2, policy1, new[] { ("https://localhost:10000/cluster2/api/health/", 1), ("https://localhost:10001/cluster2/api/health/", 1) }, policyCallTimes: 2);

        GC.KeepAlive(monitor); // The timer does not keep a strong reference to the scheduler
    }

    [Fact]
    public async Task ProbeCluster_ClusterChanged_StopSendingProbes()
    {
        var policy0 = new Mock<IActiveHealthCheckPolicy>();
        policy0.SetupGet(p => p.Name).Returns("policy0");
        var policy1 = new Mock<IActiveHealthCheckPolicy>();
        policy1.SetupGet(p => p.Name).Returns("policy1");
        var options = Options.Create(new ActiveHealthCheckMonitorOptions { DefaultInterval = TimeSpan.FromSeconds(60), DefaultTimeout = TimeSpan.FromSeconds(5) });
        var timeProvider = new TestTimeProvider();
        var monitor = new ActiveHealthCheckMonitor(options, new[] { policy0.Object, policy1.Object }, new DefaultProbingRequestFactory(), timeProvider, GetLogger());

        var httpClient0 = GetHttpClient();
        var cluster0 = GetClusterInfo("cluster0", "policy0", true, httpClient0.Object, interval: Interval0);
        monitor.OnClusterAdded(cluster0);
        var httpClient2 = GetHttpClient();
        var cluster2 = GetClusterInfo("cluster2", "policy1", true, httpClient2.Object, interval: Interval1);
        monitor.OnClusterAdded(cluster2);

        Assert.False(monitor.InitialProbeCompleted);

        await monitor.CheckHealthAsync(Array.Empty<ClusterState>());

        Assert.True(monitor.InitialProbeCompleted);

        timeProvider.FireAllTimers();

        Assert.Equal(2, timeProvider.TimerCount);
        timeProvider.VerifyTimer(0, Interval0);
        timeProvider.VerifyTimer(1, Interval1);
        VerifySentProbeAndResult(cluster0, httpClient0, policy0, new[] { ("https://localhost:20000/cluster0/api/health/", 1), ("https://localhost:20001/cluster0/api/health/", 1) }, policyCallTimes: 1);
        VerifySentProbeAndResult(cluster2, httpClient2, policy1, new[] { ("https://localhost:20000/cluster2/api/health/", 1), ("https://localhost:20001/cluster2/api/health/", 1) }, policyCallTimes: 1);

        var healthCheckConfig = new HealthCheckConfig
        {
            Passive = new PassiveHealthCheckConfig
            {
                Enabled = true,
                Policy = "passive0",
            },
            Active = new ActiveHealthCheckConfig
            {
                Policy = cluster2.Model.Config.HealthCheck.Active.Policy,
            }
        };
        cluster2.Model = new ClusterModel(new ClusterConfig { ClusterId = cluster2.ClusterId, HealthCheck = healthCheckConfig },
            cluster2.Model.HttpClient);

        monitor.OnClusterChanged(cluster2);

        timeProvider.FireTimer(0);

        timeProvider.AssertTimerDisposed(1);
        VerifySentProbeAndResult(cluster0, httpClient0, policy0, new[] { ("https://localhost:20000/cluster0/api/health/", 2), ("https://localhost:20001/cluster0/api/health/", 2) }, policyCallTimes: 2);

        GC.KeepAlive(monitor); // The timer does not keep a strong reference to the scheduler
    }

    [Fact]
    public async Task ProbeCluster_UnsuccessfulResponseReceivedOrExceptionThrown_ReportItToPolicy()
    {
        var policy = new Mock<IActiveHealthCheckPolicy>();
        policy.SetupGet(p => p.Name).Returns("policy0");
        var options = Options.Create(new ActiveHealthCheckMonitorOptions { DefaultInterval = TimeSpan.FromSeconds(60), DefaultTimeout = TimeSpan.FromSeconds(5) });
        var clusters = new List<ClusterState>();
        var monitor = new ActiveHealthCheckMonitor(options, new[] { policy.Object }, new DefaultProbingRequestFactory(), new Mock<TimeProvider>().Object, GetLogger());

        var httpClient = new Mock<HttpMessageInvoker>(() => new HttpMessageInvoker(new Mock<HttpMessageHandler>().Object));
        httpClient.Setup(c => c.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
            .Returns((HttpRequestMessage m, CancellationToken t) => GetResponse(m, t));
        var cluster = GetClusterInfo("cluster0", "policy0", true, httpClient.Object, destinationCount: 3);
        clusters.Add(cluster);

        Assert.False(monitor.InitialProbeCompleted);

        await monitor.CheckHealthAsync(clusters);

        Assert.True(monitor.InitialProbeCompleted);

        policy.Verify(
            p => p.ProbingCompleted(
                cluster,
                It.Is<IReadOnlyList<DestinationProbingResult>>(
                    r => r.Count == 3
                    && r.Single(i => i.Destination.DestinationId == "destination0").Response.StatusCode == HttpStatusCode.InternalServerError
                    && r.Single(i => i.Destination.DestinationId == "destination0").Exception == null
                    && r.Single(i => i.Destination.DestinationId == "destination1").Response == null
                    && r.Single(i => i.Destination.DestinationId == "destination1").Exception.GetType() == typeof(InvalidOperationException)
                    && r.Single(i => i.Destination.DestinationId == "destination2").Response.StatusCode == HttpStatusCode.OK
                    && r.Single(i => i.Destination.DestinationId == "destination2").Exception == null)),
            Times.Once);
        policy.Verify(p => p.Name);
        policy.VerifyNoOtherCalls();

        GC.KeepAlive(monitor); // The timer does not keep a strong reference to the scheduler

        async Task<HttpResponseMessage> GetResponse(HttpRequestMessage m, CancellationToken t)
        {
            return await Task.Run(() =>
            {
                switch (m.RequestUri.AbsoluteUri)
                {
                    case "https://localhost:20000/cluster0/api/health/":
                        return new HttpResponseMessage(HttpStatusCode.InternalServerError) { Version = m.Version };
                    case "https://localhost:20001/cluster0/api/health/":
                        throw new InvalidOperationException();
                    default:
                        return new HttpResponseMessage(HttpStatusCode.OK) { Version = m.Version };
                }
            });
        }
    }

    [Fact]
    public async Task ForceCheckAll_PolicyThrowsException_SkipItAndSetIsFullyInitializedFlag()
    {
        var policy = new Mock<IActiveHealthCheckPolicy>();
        policy.SetupGet(p => p.Name).Returns("policy0");
        policy.Setup(p => p.ProbingCompleted(It.IsAny<ClusterState>(), It.IsAny<IReadOnlyList<DestinationProbingResult>>())).Throws<InvalidOperationException>();
        var options = Options.Create(new ActiveHealthCheckMonitorOptions { DefaultInterval = TimeSpan.FromSeconds(60), DefaultTimeout = TimeSpan.FromSeconds(5) });
        var clusters = new List<ClusterState>();
        var monitor = new ActiveHealthCheckMonitor(options, new[] { policy.Object }, new DefaultProbingRequestFactory(), new Mock<TimeProvider>().Object, GetLogger());

        var httpClient = GetHttpClient();
        var cluster = GetClusterInfo("cluster0", "policy0", true, httpClient.Object);
        clusters.Add(cluster);

        Assert.False(monitor.InitialProbeCompleted);

        await monitor.CheckHealthAsync(clusters);

        Assert.True(monitor.InitialProbeCompleted);

        policy.Verify(p => p.ProbingCompleted(It.IsAny<ClusterState>(), It.IsAny<IReadOnlyList<DestinationProbingResult>>()), Times.Once);
        policy.Verify(p => p.Name);
        policy.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.InternalServerError, HttpStatusCode.OK, HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.InternalServerError, HttpStatusCode.InternalServerError, HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.OK, HttpStatusCode.InternalServerError, HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.BadRequest, HttpStatusCode.OK, HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.BadRequest)]
    public async Task InitialDestinationsProbed_TrueAfterTheFirstProbe_AllReturns(HttpStatusCode firstResult, HttpStatusCode secondResult, HttpStatusCode thirdResult)
    {
        var policy = new Mock<IActiveHealthCheckPolicy>();
        policy.SetupGet(p => p.Name).Returns("policy0");
        var options = Options.Create(new ActiveHealthCheckMonitorOptions { DefaultInterval = TimeSpan.FromSeconds(60), DefaultTimeout = Timeout.InfiniteTimeSpan });
        var clusters = new List<ClusterState>();
        var monitor = new ActiveHealthCheckMonitor(options, new[] { policy.Object }, new DefaultProbingRequestFactory(), new Mock<TimeProvider>().Object, GetLogger());

        var tcs0 = new TaskCompletionSource<HttpResponseMessage>();
        var httpClient0 = GetHttpClient(tcs0.Task);
        var cluster0 = GetClusterInfo("cluster0", "policy0", true, httpClient0.Object, destinationCount: 1);
        clusters.Add(cluster0);
        var tcs1 = new TaskCompletionSource<HttpResponseMessage>();
        var httpClient1 = GetHttpClient(tcs1.Task);
        var cluster1 = GetClusterInfo("cluster1", "policy0", true, httpClient1.Object, destinationCount: 1);
        clusters.Add(cluster1);
        var tcs2 = new TaskCompletionSource<HttpResponseMessage>();
        var httpClient2 = GetHttpClient(tcs2.Task);
        var cluster2 = GetClusterInfo("cluster2", "policy0", true, httpClient2.Object, destinationCount: 1);
        clusters.Add(cluster2);

        Assert.False(monitor.InitialProbeCompleted);

        var healthCheckTask = monitor.CheckHealthAsync(clusters);

        Assert.False(healthCheckTask.IsCompleted);
        Assert.False(monitor.InitialProbeCompleted);

        tcs0.SetResult(new HttpResponseMessage(firstResult));

        Assert.False(healthCheckTask.IsCompleted);
        Assert.False(monitor.InitialProbeCompleted);

        tcs1.SetResult(new HttpResponseMessage(secondResult));

        Assert.False(healthCheckTask.IsCompleted);
        Assert.False(monitor.InitialProbeCompleted);

        tcs2.SetResult(new HttpResponseMessage(thirdResult));

        await healthCheckTask;

        Assert.True(monitor.InitialProbeCompleted);

        policy.Verify(p => p.ProbingCompleted(It.IsAny<ClusterState>(), It.IsAny<IReadOnlyList<DestinationProbingResult>>()), Times.Exactly(3));
        policy.Verify(p => p.Name);
        policy.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task InitialDestinationsProbed_TrueAfterTheFirstProbe_OneTimesOut()
    {
        var policy = new Mock<IActiveHealthCheckPolicy>();
        policy.SetupGet(p => p.Name).Returns("policy0");
        var options = Options.Create(new ActiveHealthCheckMonitorOptions { DefaultInterval = TimeSpan.FromSeconds(60), DefaultTimeout = TimeSpan.FromMilliseconds(1) });
        var clusters = new List<ClusterState>();
        var monitor = new ActiveHealthCheckMonitor(options, new[] { policy.Object }, new DefaultProbingRequestFactory(), new Mock<TimeProvider>().Object, GetLogger());

        var assertsCompletedMre = new ManualResetEventSlim(false);

        var tcs0 = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var httpClient0 = GetHttpClient(tcs0.Task);
        var cluster0 = GetClusterInfo("cluster0", "policy0", true, httpClient0.Object, destinationCount: 1);
        clusters.Add(cluster0);
        var tcs1 = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var httpClient1 = GetHttpClient(tcs1.Task, () =>
        {
            assertsCompletedMre.Wait();
            tcs1.SetCanceled();
        });
        var cluster1 = GetClusterInfo("cluster1", "policy0", true, httpClient1.Object, destinationCount: 1);
        clusters.Add(cluster1);

        Assert.False(monitor.InitialProbeCompleted);

        var healthCheckTask = monitor.CheckHealthAsync(clusters);

        Assert.False(healthCheckTask.IsCompleted);
        Assert.False(monitor.InitialProbeCompleted);

        tcs0.SetResult(new HttpResponseMessage(HttpStatusCode.OK));

        Assert.False(healthCheckTask.IsCompleted);
        Assert.False(monitor.InitialProbeCompleted);

        assertsCompletedMre.Set();

        // Never set result to the second destination for it to time out.

        await healthCheckTask;

        Assert.True(monitor.InitialProbeCompleted);

        policy.Verify(p => p.ProbingCompleted(It.IsAny<ClusterState>(), It.IsAny<IReadOnlyList<DestinationProbingResult>>()), Times.Exactly(2));
        policy.Verify(p => p.Name);
        policy.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task InitialDestinationsProbed_TrueAfterTheFirstProbe_AllTimeOut()
    {
        var policy = new Mock<IActiveHealthCheckPolicy>();
        policy.SetupGet(p => p.Name).Returns("policy0");
        var options = Options.Create(new ActiveHealthCheckMonitorOptions { DefaultInterval = TimeSpan.FromSeconds(60), DefaultTimeout = TimeSpan.FromMilliseconds(1) });
        var clusters = new List<ClusterState>();
        var monitor = new ActiveHealthCheckMonitor(options, new[] { policy.Object }, new DefaultProbingRequestFactory(), new Mock<TimeProvider>().Object, GetLogger());

        var tcs0 = new TaskCompletionSource<HttpResponseMessage>();
        var httpClient0 = GetHttpClient(tcs0.Task, () => tcs0.SetCanceled());
        var cluster0 = GetClusterInfo("cluster0", "policy0", true, httpClient0.Object, destinationCount: 1);
        clusters.Add(cluster0);
        var tcs1 = new TaskCompletionSource<HttpResponseMessage>();
        var httpClient1 = GetHttpClient(tcs1.Task, () => tcs1.SetCanceled());
        var cluster1 = GetClusterInfo("cluster1", "policy0", true, httpClient1.Object, destinationCount: 1);
        clusters.Add(cluster1);

        Assert.False(monitor.InitialProbeCompleted);

        var healthCheckTask = monitor.CheckHealthAsync(clusters);

        // Never set results to the either of the destination for them to time out.

        await healthCheckTask;

        Assert.True(monitor.InitialProbeCompleted);

        policy.Verify(p => p.ProbingCompleted(It.IsAny<ClusterState>(), It.IsAny<IReadOnlyList<DestinationProbingResult>>()), Times.Exactly(2));
        policy.Verify(p => p.Name);
        policy.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task InitialDestinationsProbed_TrueAfterTheFirstProbe_OneThrows()
    {
        var policy = new Mock<IActiveHealthCheckPolicy>();
        policy.SetupGet(p => p.Name).Returns("policy0");
        var options = Options.Create(new ActiveHealthCheckMonitorOptions { DefaultInterval = TimeSpan.FromSeconds(60), DefaultTimeout = Timeout.InfiniteTimeSpan });
        var clusters = new List<ClusterState>();
        var monitor = new ActiveHealthCheckMonitor(options, new[] { policy.Object }, new DefaultProbingRequestFactory(), new Mock<TimeProvider>().Object, GetLogger());

        var tcs0 = new TaskCompletionSource<HttpResponseMessage>();
        var httpClient0 = GetHttpClient(tcs0.Task);
        var cluster0 = GetClusterInfo("cluster0", "policy0", true, httpClient0.Object, destinationCount: 1);
        clusters.Add(cluster0);
        var tcs1 = new TaskCompletionSource<HttpResponseMessage>();
        var httpClient1 = GetHttpClient(tcs1.Task);
        var cluster1 = GetClusterInfo("cluster1", "policy0", true, httpClient1.Object, destinationCount: 1);
        clusters.Add(cluster1);

        Assert.False(monitor.InitialProbeCompleted);

        var healthCheckTask = monitor.CheckHealthAsync(clusters);

        Assert.False(healthCheckTask.IsCompleted);
        Assert.False(monitor.InitialProbeCompleted);

        tcs0.SetException(new Exception());

        Assert.False(healthCheckTask.IsCompleted);
        Assert.False(monitor.InitialProbeCompleted);

        tcs1.SetResult(new HttpResponseMessage(HttpStatusCode.OK));

        await healthCheckTask;

        Assert.True(monitor.InitialProbeCompleted);

        policy.Verify(p => p.ProbingCompleted(It.IsAny<ClusterState>(), It.IsAny<IReadOnlyList<DestinationProbingResult>>()), Times.Exactly(2));
        policy.Verify(p => p.Name);
        policy.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task InitialDestinationsProbed_TrueAfterTheFirstProbe_AllThrow()
    {
        var policy = new Mock<IActiveHealthCheckPolicy>();
        policy.SetupGet(p => p.Name).Returns("policy0");
        var options = Options.Create(new ActiveHealthCheckMonitorOptions { DefaultInterval = TimeSpan.FromSeconds(60), DefaultTimeout = Timeout.InfiniteTimeSpan });
        var clusters = new List<ClusterState>();
        var monitor = new ActiveHealthCheckMonitor(options, new[] { policy.Object }, new DefaultProbingRequestFactory(), new Mock<TimeProvider>().Object, GetLogger());

        var tcs0 = new TaskCompletionSource<HttpResponseMessage>();
        var httpClient0 = GetHttpClient(tcs0.Task);
        var cluster0 = GetClusterInfo("cluster0", "policy0", true, httpClient0.Object, destinationCount: 1);
        clusters.Add(cluster0);
        var tcs1 = new TaskCompletionSource<HttpResponseMessage>();
        var httpClient1 = GetHttpClient(tcs1.Task);
        var cluster1 = GetClusterInfo("cluster1", "policy0", true, httpClient1.Object, destinationCount: 1);
        clusters.Add(cluster1);

        Assert.False(monitor.InitialProbeCompleted);

        var healthCheckTask = monitor.CheckHealthAsync(clusters);

        Assert.False(healthCheckTask.IsCompleted);
        Assert.False(monitor.InitialProbeCompleted);

        tcs0.SetException(new Exception());

        Assert.False(healthCheckTask.IsCompleted);
        Assert.False(monitor.InitialProbeCompleted);

        tcs1.SetException(new Exception());

        await healthCheckTask;

        Assert.True(monitor.InitialProbeCompleted);

        policy.Verify(p => p.ProbingCompleted(It.IsAny<ClusterState>(), It.IsAny<IReadOnlyList<DestinationProbingResult>>()), Times.Exactly(2));
        policy.Verify(p => p.Name);
        policy.VerifyNoOtherCalls();
    }

    private static void VerifySentProbeAndResult(ClusterState cluster, Mock<HttpMessageInvoker> httpClient, Mock<IActiveHealthCheckPolicy> policy, (string RequestUri, int Times)[] probes, int policyCallTimes = 1, string userAgent = @"^YARP\/\S+? \([^\)]+?\)$")
    {
        foreach (var probe in probes)
        {
            httpClient.Verify(
                c => c.SendAsync(
                    It.Is<HttpRequestMessage>(m => m.RequestUri.AbsoluteUri == probe.RequestUri && Regex.IsMatch(m.Headers.UserAgent.ToString(), userAgent)),
                    It.IsAny<CancellationToken>()),
                Times.Exactly(probe.Times));
        }
        httpClient.VerifyNoOtherCalls();
        policy.Verify(
            p => p.ProbingCompleted(
                cluster,
                It.Is<IReadOnlyList<DestinationProbingResult>>(r => cluster.Destinations.Values.All(d => r.Any(i => i.Destination == d && i.Response.StatusCode == HttpStatusCode.OK)))),
            Times.Exactly(policyCallTimes));
        policy.Verify(p => p.Name);
        policy.VerifyNoOtherCalls();
    }

    private ClusterState GetClusterInfo(string id, string policy, bool activeCheckEnabled, HttpMessageInvoker httpClient, TimeSpan? interval = null, TimeSpan? timeout = null, int destinationCount = 2)
    {
        var clusterModel = new ClusterModel(
            new ClusterConfig
            {
                ClusterId = id,
                HealthCheck = new HealthCheckConfig
                {
                    Active = new ActiveHealthCheckConfig
                    {
                        Enabled = activeCheckEnabled,
                        Interval = interval,
                        Timeout = timeout,
                        Policy = policy,
                        Path = "/api/health/",
                    }
                }
            },
            httpClient);
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

    private Mock<HttpMessageInvoker> GetHttpClient(Task<HttpResponseMessage> task = null, Action cancellation = null)
    {
        var httpClient = new Mock<HttpMessageInvoker>(() => new HttpMessageInvoker(new Mock<HttpMessageHandler>().Object));
        httpClient.Setup(c => c.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
            .Returns((HttpRequestMessage m, CancellationToken c) =>
            {
                if (cancellation is not null)
                {
                    c.Register(_ => cancellation(), null);
                }

                return task ?? Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Version = m.Version });
            });
        return httpClient;
    }

    private static ILogger<ActiveHealthCheckMonitor> GetLogger()
    {
        return new Mock<ILogger<ActiveHealthCheckMonitor>>().Object;
    }
}
