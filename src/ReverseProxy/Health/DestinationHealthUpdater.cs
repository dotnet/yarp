// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Model;

namespace Yarp.ReverseProxy.Health;

internal sealed class DestinationHealthUpdater : IDestinationHealthUpdater, IDisposable
{
    private readonly EntityActionScheduler<(ClusterState Cluster, DestinationState Destination)> _scheduler;
    private readonly IClusterDestinationsUpdater _clusterUpdater;
    private readonly ILogger<DestinationHealthUpdater> _logger;

    public DestinationHealthUpdater(
        TimeProvider timeProvider,
        IClusterDestinationsUpdater clusterDestinationsUpdater,
        ILogger<DestinationHealthUpdater> logger)
    {
        ArgumentNullException.ThrowIfNull(clusterDestinationsUpdater);
        ArgumentNullException.ThrowIfNull(logger);

        _scheduler = new EntityActionScheduler<(ClusterState Cluster, DestinationState Destination)>(d => Reactivate(d.Cluster, d.Destination), autoStart: true, runOnce: true, timeProvider);
        _clusterUpdater = clusterDestinationsUpdater;
        _logger = logger;
    }

    public void SetActive(ClusterState cluster, IEnumerable<NewActiveDestinationHealth> newHealthPairs)
    {
        var changed = false;
        foreach (var newHealthPair in newHealthPairs)
        {
            var destination = newHealthPair.Destination;
            var newHealth = newHealthPair.NewActiveHealth;

            var healthState = destination.Health;
            if (newHealth != healthState.Active)
            {
                healthState.Active = newHealth;
                changed = true;
                if (newHealth == DestinationHealth.Unhealthy)
                {
                    Log.ActiveDestinationHealthStateIsSetToUnhealthy(_logger, destination.DestinationId, cluster.ClusterId);
                }
                else
                {
                    Log.ActiveDestinationHealthStateIsSet(_logger, destination.DestinationId, cluster.ClusterId, newHealth);
                }
            }
        }

        if (changed)
        {
            _clusterUpdater.UpdateAvailableDestinations(cluster);
        }
    }

    public void SetPassive(ClusterState cluster, DestinationState destination, DestinationHealth newHealth, TimeSpan reactivationPeriod)
    {
        _ = SetPassiveAsync(cluster, destination, newHealth, reactivationPeriod);
    }

    internal Task SetPassiveAsync(ClusterState cluster, DestinationState destination, DestinationHealth newHealth, TimeSpan reactivationPeriod)
    {
        var healthState = destination.Health;
        if (newHealth != healthState.Passive)
        {
            healthState.Passive = newHealth;
            ScheduleReactivation(cluster, destination, newHealth, reactivationPeriod);
            return Task.Factory.StartNew(c => UpdateDestinations(c!), cluster, CancellationToken.None, TaskCreationOptions.RunContinuationsAsynchronously, TaskScheduler.Default);
        }
        return Task.CompletedTask;
    }

    private void UpdateDestinations(object cluster)
    {
        _clusterUpdater.UpdateAvailableDestinations((ClusterState)cluster);
    }

    private void ScheduleReactivation(ClusterState cluster, DestinationState destination, DestinationHealth newHealth, TimeSpan reactivationPeriod)
    {
        if (newHealth == DestinationHealth.Unhealthy)
        {
            _scheduler.ScheduleEntity((cluster, destination), reactivationPeriod);
            Log.UnhealthyDestinationIsScheduledForReactivation(_logger, destination.DestinationId, reactivationPeriod);
        }
    }

    public void Dispose()
    {
        _scheduler.Dispose();
    }

    private Task Reactivate(ClusterState cluster, DestinationState destination)
    {
        var healthState = destination.Health;
        if (healthState.Passive == DestinationHealth.Unhealthy)
        {
            healthState.Passive = DestinationHealth.Unknown;
            Log.PassiveDestinationHealthResetToUnknownState(_logger, destination.DestinationId);
            _clusterUpdater.UpdateAvailableDestinations(cluster);
        }

        return Task.CompletedTask;
    }

    private static class Log
    {
        private static readonly Action<ILogger, string, TimeSpan, Exception?> _unhealthyDestinationIsScheduledForReactivation = LoggerMessage.Define<string, TimeSpan>(
            LogLevel.Warning,
            EventIds.UnhealthyDestinationIsScheduledForReactivation,
            "Destination `{destinationId}` marked as 'Unhealthy` by the passive health check is scheduled for a reactivation in `{reactivationPeriod}`.");

        private static readonly Action<ILogger, string, Exception?> _passiveDestinationHealthResetToUnknownState = LoggerMessage.Define<string>(
            LogLevel.Information,
            EventIds.PassiveDestinationHealthResetToUnknownState,
            "Passive health state of the destination `{destinationId}` is reset to 'Unknown`.");

        private static readonly Action<ILogger, string, string, Exception?> _activeDestinationHealthStateIsSetToUnhealthy = LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            EventIds.ActiveDestinationHealthStateIsSetToUnhealthy,
            "Active health state of destination `{destinationId}` on cluster `{clusterId}` is set to 'Unhealthy'.");

        private static readonly Action<ILogger, string, string, DestinationHealth, Exception?> _activeDestinationHealthStateIsSet = LoggerMessage.Define<string, string, DestinationHealth>(
            LogLevel.Information,
            EventIds.ActiveDestinationHealthStateIsSet,
            "Active health state of destination `{destinationId}` on cluster `{clusterId}` is set to '{newHealthState}'.");

        public static void ActiveDestinationHealthStateIsSetToUnhealthy(ILogger logger, string destinationId, string clusterId)
        {
            _activeDestinationHealthStateIsSetToUnhealthy(logger, destinationId, clusterId, null);
        }

        public static void ActiveDestinationHealthStateIsSet(ILogger logger, string destinationId, string clusterId, DestinationHealth newHealthState)
        {
            _activeDestinationHealthStateIsSet(logger, destinationId, clusterId, newHealthState, null);
        }

        public static void UnhealthyDestinationIsScheduledForReactivation(ILogger logger, string destinationId, TimeSpan reactivationPeriod)
        {
            _unhealthyDestinationIsScheduledForReactivation(logger, destinationId, reactivationPeriod, null);
        }

        public static void PassiveDestinationHealthResetToUnknownState(ILogger logger, string destinationId)
        {
            _passiveDestinationHealthResetToUnknownState(logger, destinationId, null);
        }
    }
}
