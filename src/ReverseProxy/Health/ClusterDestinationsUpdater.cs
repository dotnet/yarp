// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Health;

internal sealed class ClusterDestinationsUpdater : IClusterDestinationsUpdater
{
    private readonly ConditionalWeakTable<ClusterState, SemaphoreSlim> _clusterLocks = new ConditionalWeakTable<ClusterState, SemaphoreSlim>();
    private readonly FrozenDictionary<string, IAvailableDestinationsPolicy> _destinationPolicies;

    public ClusterDestinationsUpdater(IEnumerable<IAvailableDestinationsPolicy> destinationPolicies)
    {
        ArgumentNullException.ThrowIfNull(destinationPolicies);
        _destinationPolicies = destinationPolicies.ToDictionaryByUniqueId(p => p.Name);
    }

    public void UpdateAvailableDestinations(ClusterState cluster)
    {
        var allDestinations = cluster.DestinationsState?.AllDestinations;
        if (allDestinations is null)
        {
            throw new InvalidOperationException($"{nameof(UpdateAllDestinations)} must be called first.");
        }

        UpdateInternal(cluster, allDestinations, force: false);
    }

    public void UpdateAllDestinations(ClusterState cluster)
    {
        // Values already makes a copy of the collection, downcast to avoid making a second copy.
        // https://github.com/dotnet/runtime/blob/e164551f1c96138521b4e58f14f8ac1e4369005d/src/libraries/System.Collections.Concurrent/src/System/Collections/Concurrent/ConcurrentDictionary.cs#L2145-L2168
        var allDestinations = (IReadOnlyList<DestinationState>)cluster.Destinations.Values;
        UpdateInternal(cluster, allDestinations, force: true);
    }

    private void UpdateInternal(ClusterState cluster, IReadOnlyList<DestinationState> allDestinations, bool force)
    {
        // Prevent overlapping updates and debounce extra concurrent calls.
        // If there are multiple concurrent calls to rebuild the dynamic state, we want to ensure that
        // updates don't conflict with each other. Additionally, we debounce extra concurrent calls if
        // they arrive in a quick succession to avoid spending too much CPU on frequent state rebuilds.
        // Specifically, only up to two threads are allowed to wait here and actually execute a rebuild,
        // all others will be debounced and the call will return without updating the ClusterState.DestinationsState.
        // However, changes made by those debounced threads (e.g. destination health updates) will be
        // taken into account by one of blocked threads after they get unblocked to run a rebuild.
        var updateLock = _clusterLocks.GetValue(cluster, _ => new SemaphoreSlim(2));
        var lockTaken = false;
        if (force)
        {
            lockTaken = true;
            updateLock.Wait();
        }
        else
        {
            lockTaken = updateLock.Wait(0);
        }

        if (!lockTaken)
        {
            return;
        }

        lock (updateLock)
        {
            try
            {
                var config = cluster.Model.Config;
                var destinationPolicy = _destinationPolicies.GetRequiredServiceById(
                    config.HealthCheck?.AvailableDestinationsPolicy,
                    HealthCheckConstants.AvailableDestinations.HealthyOrPanic);

                var availableDestinations = destinationPolicy.GetAvailableDestinations(config, allDestinations);

                cluster.DestinationsState = new ClusterDestinationsState(allDestinations, availableDestinations);
            }
            finally
            {
                // Semaphore is released while still holding the lock to AVOID the following case.
                // The first thread (T1) finished a rebuild and left the lock while still holding the semaphore. The second thread (T2)
                // waiting on the lock gets awaken, proceeds under the lock and begins the next rebuild. If at this exact moment
                // the third thread (T3) enters this method and tries to acquire the semaphore, it will be debounced because
                // the semaphore's count is still 0. However, T2 could have already made some progress and didn't observe updates made
                // by T3.
                // By releasing the semaphore under the lock, we make sure that in the above situation T3 will proceed till the lock and
                // its updates will be observed anyway.
                updateLock.Release();
            }
        }
    }
}
