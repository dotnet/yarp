// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Health;

internal partial class ActiveHealthCheckMonitor : IActiveHealthCheckMonitor, IClusterChangeListener, IDisposable
{
    private readonly ActiveHealthCheckMonitorOptions _monitorOptions;
    private readonly FrozenDictionary<string, IActiveHealthCheckPolicy> _policies;
    private readonly IProbingRequestFactory _probingRequestFactory;
    private readonly ILogger<ActiveHealthCheckMonitor> _logger;

    public ActiveHealthCheckMonitor(
        IOptions<ActiveHealthCheckMonitorOptions> monitorOptions,
        IEnumerable<IActiveHealthCheckPolicy> policies,
        IProbingRequestFactory probingRequestFactory,
        TimeProvider timeProvider,
        ILogger<ActiveHealthCheckMonitor> logger)
    {
        ArgumentNullException.ThrowIfNull(monitorOptions?.Value);
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(probingRequestFactory);
        ArgumentNullException.ThrowIfNull(logger);

        _monitorOptions = monitorOptions.Value;
        _policies = policies.ToDictionaryByUniqueId(p => p.Name);
        _probingRequestFactory = probingRequestFactory;
        _logger = logger;
        Scheduler = new EntityActionScheduler<ClusterState>(cluster => ProbeCluster(cluster), autoStart: false, runOnce: false, timeProvider);
    }

    public bool InitialProbeCompleted { get; private set; }

    internal EntityActionScheduler<ClusterState> Scheduler { get; }

    public Task CheckHealthAsync(IEnumerable<ClusterState> clusters)
    {
        return Task.Run(async () =>
        {
            try
            {
                var probeClusterTasks = new List<Task>();
                foreach (var cluster in clusters)
                {
                    if ((cluster.Model.Config.HealthCheck?.Active?.Enabled).GetValueOrDefault())
                    {
                        probeClusterTasks.Add(ProbeCluster(cluster));
                    }
                }

                await Task.WhenAll(probeClusterTasks);
            }
            catch (Exception ex)
            {
                Log.ExplicitActiveCheckOfAllClustersHealthFailed(_logger, ex);
            }
            finally
            {
                InitialProbeCompleted = true;
            }

            Scheduler.Start();
        });
    }

    public void OnClusterAdded(ClusterState cluster)
    {
        var config = cluster.Model.Config.HealthCheck?.Active;
        if (config is not null && config.Enabled.GetValueOrDefault())
        {
            Scheduler.ScheduleEntity(cluster, config.Interval ?? _monitorOptions.DefaultInterval);
        }
    }

    public void OnClusterChanged(ClusterState cluster)
    {
        var config = cluster.Model.Config.HealthCheck?.Active;
        if (config is not null && config.Enabled.GetValueOrDefault())
        {
            Scheduler.ChangePeriod(cluster, config.Interval ?? _monitorOptions.DefaultInterval);
        }
        else
        {
            Scheduler.UnscheduleEntity(cluster);
        }
    }

    public void OnClusterRemoved(ClusterState cluster)
    {
        Scheduler.UnscheduleEntity(cluster);
    }

    public void Dispose()
    {
        Scheduler.Dispose();
    }

    private async Task ProbeCluster(ClusterState cluster)
    {
        var config = cluster.Model.Config.HealthCheck?.Active;
        if (config is null || !config.Enabled.GetValueOrDefault())
        {
            return;
        }

        // Creates an Activity to trace the active health checks
        using var activity = Observability.YarpActivitySource.StartActivity("proxy.cluster_health_checks", ActivityKind.Consumer);
        activity?.AddTag("proxy.cluster_id", cluster.ClusterId);

        Log.StartingActiveHealthProbingOnCluster(_logger, cluster.ClusterId);

        var allDestinations = cluster.DestinationsState.AllDestinations;
        var probeTasks = new Task<DestinationProbingResult>[allDestinations.Count];
        var probeResults = new DestinationProbingResult[probeTasks.Length];

        var timeout = config.Timeout ?? _monitorOptions.DefaultTimeout;

        for (var i = 0; i < probeTasks.Length; i++)
        {
            probeTasks[i] = ProbeDestinationAsync(cluster, allDestinations[i], timeout);
        }

        for (var i = 0; i < probeResults.Length; i++)
        {
            probeResults[i] = await probeTasks[i];
        }

        try
        {
            var policy = _policies.GetRequiredServiceById(config.Policy, HealthCheckConstants.ActivePolicy.ConsecutiveFailures);
            policy.ProbingCompleted(cluster, probeResults);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            Log.ActiveHealthProbingFailedOnCluster(_logger, cluster.ClusterId, ex);
            activity?.SetStatus(ActivityStatusCode.Error);
        }
        finally
        {
            try
            {
                foreach (var probeResult in probeResults)
                {
                    probeResult.Response?.Dispose();
                }
            }
            catch (Exception ex)
            {
                Log.ErrorOccurredDuringActiveHealthProbingShutdownOnCluster(_logger, cluster.ClusterId, ex);
            }

            Log.StoppedActiveHealthProbingOnCluster(_logger, cluster.ClusterId);
        }
    }

    private async Task<DestinationProbingResult> ProbeDestinationAsync(ClusterState cluster, DestinationState destination, TimeSpan timeout)
    {
        using var probeActivity = Observability.YarpActivitySource.StartActivity("proxy.destination_health_check", ActivityKind.Client);
        probeActivity?.AddTag("proxy.cluster_id", cluster.ClusterId);
        probeActivity?.AddTag("proxy.destination_id", destination.DestinationId);

        HttpRequestMessage request;
        try
        {
            request = _probingRequestFactory.CreateRequest(cluster.Model, destination.Model);
        }
        catch (Exception ex)
        {
            Log.ActiveHealthProbeConstructionFailedOnCluster(_logger, destination.DestinationId, cluster.ClusterId, ex);

            probeActivity?.SetStatus(ActivityStatusCode.Error);

            return new DestinationProbingResult(destination, null, ex);
        }

        using var cts = new CancellationTokenSource(timeout);

        try
        {
            Log.SendingHealthProbeToEndpointOfDestination(_logger, request.RequestUri, destination.DestinationId, cluster.ClusterId);
            var response = await cluster.Model.HttpClient.SendAsync(request, cts.Token);
            Log.DestinationProbingCompleted(_logger, destination.DestinationId, cluster.ClusterId, (int)response.StatusCode);

            probeActivity?.SetStatus(ActivityStatusCode.Ok);

            return new DestinationProbingResult(destination, response, null);
        }
        catch (Exception ex)
        {
            Log.DestinationProbingFailed(_logger, destination.DestinationId, cluster.ClusterId, ex);

            probeActivity?.SetStatus(ActivityStatusCode.Error);

            return new DestinationProbingResult(destination, null, ex);
        }
    }
}
