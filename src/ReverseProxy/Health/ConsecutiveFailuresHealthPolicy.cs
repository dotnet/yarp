// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Options;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Health;

internal sealed class ConsecutiveFailuresHealthPolicy : IActiveHealthCheckPolicy
{
    private readonly ConsecutiveFailuresHealthPolicyOptions _options;
    private readonly ConditionalWeakTable<ClusterState, ParsedMetadataEntry<double>> _clusterThresholds = new ConditionalWeakTable<ClusterState, ParsedMetadataEntry<double>>();
    private readonly ConditionalWeakTable<DestinationState, AtomicCounter> _failureCounters = new ConditionalWeakTable<DestinationState, AtomicCounter>();
    private readonly IDestinationHealthUpdater _healthUpdater;

    public string Name => HealthCheckConstants.ActivePolicy.ConsecutiveFailures;

    public ConsecutiveFailuresHealthPolicy(IOptions<ConsecutiveFailuresHealthPolicyOptions> options, IDestinationHealthUpdater healthUpdater)
    {
        ArgumentNullException.ThrowIfNull(options?.Value);
        ArgumentNullException.ThrowIfNull(healthUpdater);
        _options = options.Value;
        _healthUpdater = healthUpdater;
    }

    public void ProbingCompleted(ClusterState cluster, IReadOnlyList<DestinationProbingResult> probingResults)
    {
        if (probingResults.Count == 0)
        {
            return;
        }

        var threshold = GetFailureThreshold(cluster);

        var newHealthStates = new NewActiveDestinationHealth[probingResults.Count];
        for (var i = 0; i < probingResults.Count; i++)
        {
            var destination = probingResults[i].Destination;
            var previousState = destination.Health.Active;

            var count = _failureCounters.GetOrCreateValue(destination);
            var newHealth = EvaluateHealthState(threshold, probingResults[i].Response, count, previousState);
            newHealthStates[i] = new NewActiveDestinationHealth(destination, newHealth);
        }

        _healthUpdater.SetActive(cluster, newHealthStates);
    }

    private double GetFailureThreshold(ClusterState cluster)
    {
        var thresholdEntry = _clusterThresholds.GetValue(cluster, c => new ParsedMetadataEntry<double>(TryParse, c, ConsecutiveFailuresHealthPolicyOptions.ThresholdMetadataName));
        return thresholdEntry.GetParsedOrDefault(_options.DefaultThreshold);
    }

    private static DestinationHealth EvaluateHealthState(double threshold, HttpResponseMessage? response, AtomicCounter count, DestinationHealth previousState)
    {
        DestinationHealth newHealth;
        if (response is not null && response.IsSuccessStatusCode)
        {
            // Success
            count.Reset();
            newHealth = DestinationHealth.Healthy;
        }
        else
        {
            // Failure
            var currentFailureCount = count.Increment();
            newHealth = currentFailureCount < threshold ? previousState : DestinationHealth.Unhealthy;
        }

        return newHealth;
    }

    private static bool TryParse(string stringValue, out double parsedValue)
    {
        return double.TryParse(stringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out parsedValue);
    }
}
