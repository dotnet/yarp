// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.SessionAffinity;

/// <summary>
/// Looks up an affinitized <see cref="DestinationState"/> matching the request's affinity key if any is set
/// </summary>
internal sealed class SessionAffinityMiddleware
{
    private readonly RequestDelegate _next;
    private readonly FrozenDictionary<string, ISessionAffinityPolicy> _sessionAffinityPolicies;
    private readonly FrozenDictionary<string, IAffinityFailurePolicy> _affinityFailurePolicies;
    private readonly ILogger _logger;

    public SessionAffinityMiddleware(
        RequestDelegate next,
        IEnumerable<ISessionAffinityPolicy> sessionAffinityPolicies,
        IEnumerable<IAffinityFailurePolicy> affinityFailurePolicies,
        ILogger<SessionAffinityMiddleware> logger)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(sessionAffinityPolicies);
        ArgumentNullException.ThrowIfNull(affinityFailurePolicies);

        _next = next;
        _logger = logger;
        _sessionAffinityPolicies = sessionAffinityPolicies.ToDictionaryByUniqueId(p => p.Name);
        _affinityFailurePolicies = affinityFailurePolicies.ToDictionaryByUniqueId(p => p.Name);
    }

    public Task Invoke(HttpContext context)
    {
        var proxyFeature = context.GetReverseProxyFeature();

        var config = proxyFeature.Cluster.Config.SessionAffinity;

        if (config is null || !config.Enabled.GetValueOrDefault())
        {
            return _next(context);
        }

        return InvokeInternal(context, proxyFeature, config);
    }

    private async Task InvokeInternal(HttpContext context, IReverseProxyFeature proxyFeature, SessionAffinityConfig config)
    {
        var destinations = proxyFeature.AvailableDestinations;
        var cluster = proxyFeature.Route.Cluster!;

        var policy = _sessionAffinityPolicies.GetRequiredServiceById(config.Policy, SessionAffinityConstants.Policies.HashCookie);
        var affinityResult = await policy.FindAffinitizedDestinationsAsync(context, cluster, config, destinations, context.RequestAborted);

        // Used for Distributed Tracing as part of Open Telemetry, null if there are no listeners
        var activity = context.GetYarpActivity();
        activity?.SetTag("proxy.session_affinity.policy", policy.Name);

        switch (affinityResult.Status)
        {
            case AffinityStatus.OK:
                proxyFeature.AvailableDestinations = affinityResult.Destinations!;
                activity?.SetTag("proxy.session_affinity.status", "success");
                break;
            case AffinityStatus.AffinityKeyNotSet:
                // Nothing to do so just continue processing
                break;
            case AffinityStatus.AffinityKeyExtractionFailed:
            case AffinityStatus.DestinationNotFound:

                var failurePolicy = _affinityFailurePolicies.GetRequiredServiceById(config.FailurePolicy, SessionAffinityConstants.FailurePolicies.Redistribute);
                var keepProcessing = await failurePolicy.Handle(context, proxyFeature.Route.Cluster!, affinityResult.Status);

                if (!keepProcessing)
                {
                    // Policy reported the failure is unrecoverable and took the full responsibility for its handling,
                    // so we simply stop processing.
                    Log.AffinityResolutionFailedForCluster(_logger, cluster.ClusterId);
                    activity?.SetTag("proxy.session_affinity.status", "failed");
                    return;
                }

                Log.AffinityResolutionFailureWasHandledProcessingWillBeContinued(_logger, cluster.ClusterId, failurePolicy.Name);
                activity?.SetTag("proxy.session_affinity.status", "recovered");

                break;
            default:
                throw new NotSupportedException($"Affinity status '{affinityResult.Status}' is not supported.");
        }

        await _next(context);
    }

    private static class Log
    {
        private static readonly Action<ILogger, string, Exception?> _affinityResolutionFailedForCluster = LoggerMessage.Define<string>(
            LogLevel.Warning,
            EventIds.AffinityResolutionFailedForCluster,
            "Affinity resolution failed for cluster '{clusterId}'.");

        private static readonly Action<ILogger, string, string, Exception?> _affinityResolutionFailureWasHandledProcessingWillBeContinued = LoggerMessage.Define<string, string>(
            LogLevel.Debug,
            EventIds.AffinityResolutionFailureWasHandledProcessingWillBeContinued,
            "Affinity resolution failure for cluster '{clusterId}' was handled successfully by the policy '{policyName}'. Request processing will be continued.");

        public static void AffinityResolutionFailedForCluster(ILogger logger, string clusterId)
        {
            _affinityResolutionFailedForCluster(logger, clusterId, null);
        }

        public static void AffinityResolutionFailureWasHandledProcessingWillBeContinued(ILogger logger, string clusterId, string policyName)
        {
            _affinityResolutionFailureWasHandledProcessingWillBeContinued(logger, clusterId, policyName, null);
        }
    }
}
