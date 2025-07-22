// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Model;

namespace Yarp.ReverseProxy.SessionAffinity;

internal abstract class BaseHashCookieSessionAffinityPolicy : ISessionAffinityPolicy
{
    private static readonly object AffinityKeyId = new();
    private readonly ILogger _logger;
    private readonly TimeProvider _timeProvider;

    public BaseHashCookieSessionAffinityPolicy(TimeProvider timeProvider, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _timeProvider = timeProvider;
        _logger = logger;
    }

    public abstract string Name { get; }

    public void AffinitizeResponse(HttpContext context, ClusterState cluster, SessionAffinityConfig config, DestinationState destination)
    {
        if (!config.Enabled.GetValueOrDefault())
        {
            throw new InvalidOperationException("Session affinity is disabled for cluster.");
        }

        if (context.RequestAborted.IsCancellationRequested)
        {
            // Avoid wasting time if the client is already gone.
            return;
        }

        // Affinity key is set on the response only if it's a new affinity.
        if (!context.Items.ContainsKey(AffinityKeyId))
        {
            var affinityKey = GetDestinationHash(destination);
            var affinityCookieOptions = AffinityHelpers.CreateCookieOptions(config.Cookie, context.Request.IsHttps, _timeProvider);

            // CodeQL [SM02373] - Whether CookieOptions.Secure is used depends on YARP configuration, and session affinity may be used in non-HTTPS setups. Hash-based affinity policies do not intend to provide privacy protection. See https://learn.microsoft.com/aspnet/core/fundamentals/servers/yarp/session-affinity#key-protection.
            context.Response.Cookies.Append(config.AffinityKeyName, affinityKey, affinityCookieOptions);
        }
    }

    public AffinityResult FindAffinitizedDestinations(HttpContext context, ClusterState cluster, SessionAffinityConfig config, IReadOnlyList<DestinationState> destinations)
    {
        if (!config.Enabled.GetValueOrDefault())
        {
            throw new InvalidOperationException($"Session affinity is disabled for cluster {cluster.ClusterId}.");
        }

        var affinityHash = context.Request.Cookies[config.AffinityKeyName];
        if (affinityHash is null)
        {
            return new(null, AffinityStatus.AffinityKeyNotSet);
        }

        foreach (var d in destinations)
        {
            var hashValue = GetDestinationHash(d);

            if (affinityHash == hashValue)
            {
                context.Items[AffinityKeyId] = affinityHash;
                return new(d, AffinityStatus.OK);
            }
        }

        if (destinations.Count == 0)
        {
            Log.AffinityCannotBeEstablishedBecauseNoDestinationsFound(_logger, cluster.ClusterId);
        }
        else
        {
            Log.DestinationMatchingToAffinityKeyNotFound(_logger, cluster.ClusterId);
        }

        return new(null, AffinityStatus.DestinationNotFound);
    }

    protected abstract string GetDestinationHash(DestinationState d);
}
