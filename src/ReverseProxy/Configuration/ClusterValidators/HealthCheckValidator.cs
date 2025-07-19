// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Threading.Tasks;
using Yarp.ReverseProxy.Health;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Configuration.ClusterValidators;

internal sealed class HealthCheckValidator : IClusterValidator
{
    private readonly FrozenDictionary<string, IAvailableDestinationsPolicy> _availableDestinationsPolicies;
    private readonly FrozenDictionary<string, IActiveHealthCheckPolicy> _activeHealthCheckPolicies;
    private readonly FrozenDictionary<string, IPassiveHealthCheckPolicy> _passiveHealthCheckPolicies;

    public HealthCheckValidator(IEnumerable<IAvailableDestinationsPolicy> availableDestinationsPolicies,
        IEnumerable<IActiveHealthCheckPolicy> activeHealthCheckPolicies,
        IEnumerable<IPassiveHealthCheckPolicy> passiveHealthCheckPolicies)
    {
        ArgumentNullException.ThrowIfNull(availableDestinationsPolicies);
        ArgumentNullException.ThrowIfNull(activeHealthCheckPolicies);
        ArgumentNullException.ThrowIfNull(passiveHealthCheckPolicies);

        _availableDestinationsPolicies = availableDestinationsPolicies.ToDictionaryByUniqueId(p => p.Name);
        _activeHealthCheckPolicies = activeHealthCheckPolicies.ToDictionaryByUniqueId(p => p.Name);
        _passiveHealthCheckPolicies = passiveHealthCheckPolicies.ToDictionaryByUniqueId(p => p.Name);
    }

    public ValueTask ValidateAsync(ClusterConfig cluster, IList<Exception> errors)
    {
        var availableDestinationsPolicy = cluster.HealthCheck?.AvailableDestinationsPolicy;
        if (string.IsNullOrEmpty(availableDestinationsPolicy))
        {
            // The default.
            availableDestinationsPolicy = HealthCheckConstants.AvailableDestinations.HealthyOrPanic;
        }

        if (!_availableDestinationsPolicies.ContainsKey(availableDestinationsPolicy))
        {
            errors.Add(new ArgumentException($"No matching {nameof(IAvailableDestinationsPolicy)} found for the available destinations policy '{availableDestinationsPolicy}' set on the cluster.'{cluster.ClusterId}'."));
        }

        ValidateActiveHealthCheck(cluster, errors);
        ValidatePassiveHealthCheck(cluster, errors);

        return ValueTask.CompletedTask;
    }

    private void ValidateActiveHealthCheck(ClusterConfig cluster, IList<Exception> errors)
    {
        if (!(cluster.HealthCheck?.Active?.Enabled ?? false))
        {
            // Active health check is disabled
            return;
        }

        var activeOptions = cluster.HealthCheck.Active;
        var policy = activeOptions.Policy;
        if (string.IsNullOrEmpty(policy))
        {
            // default policy
            policy = HealthCheckConstants.ActivePolicy.ConsecutiveFailures;
        }
        if (!_activeHealthCheckPolicies.ContainsKey(policy))
        {
            errors.Add(new ArgumentException($"No matching {nameof(IActiveHealthCheckPolicy)} found for the active health check policy name '{policy}' set on the cluster '{cluster.ClusterId}'."));
        }

        if (activeOptions.Interval is not null && activeOptions.Interval <= TimeSpan.Zero)
        {
            errors.Add(new ArgumentException($"Destination probing interval set on the cluster '{cluster.ClusterId}' must be positive."));
        }

        if (activeOptions.Timeout is not null && activeOptions.Timeout <= TimeSpan.Zero)
        {
            errors.Add(new ArgumentException($"Destination probing timeout set on the cluster '{cluster.ClusterId}' must be positive."));
        }
    }

    private void ValidatePassiveHealthCheck(ClusterConfig cluster, IList<Exception> errors)
    {
        if (!(cluster.HealthCheck?.Passive?.Enabled ?? false))
        {
            // Passive health check is disabled
            return;
        }

        var passiveOptions = cluster.HealthCheck.Passive;
        var policy = passiveOptions.Policy;
        if (string.IsNullOrEmpty(policy))
        {
            // default policy
            policy = HealthCheckConstants.PassivePolicy.TransportFailureRate;
        }
        if (!_passiveHealthCheckPolicies.ContainsKey(policy))
        {
            errors.Add(new ArgumentException($"No matching {nameof(IPassiveHealthCheckPolicy)} found for the passive health check policy name '{policy}' set on the cluster '{cluster.ClusterId}'."));
        }

        if (passiveOptions.ReactivationPeriod is not null && passiveOptions.ReactivationPeriod <= TimeSpan.Zero)
        {
            errors.Add(new ArgumentException($"Unhealthy destination reactivation period set on the cluster '{cluster.ClusterId}' must be positive."));
        }
    }
}
