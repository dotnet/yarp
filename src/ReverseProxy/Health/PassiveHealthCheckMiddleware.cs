// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Health;

public class PassiveHealthCheckMiddleware
{
    private readonly RequestDelegate _next;
    private readonly FrozenDictionary<string, IPassiveHealthCheckPolicy> _policies;

    public PassiveHealthCheckMiddleware(RequestDelegate next, IEnumerable<IPassiveHealthCheckPolicy> policies)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(policies);
        _next = next;
        _policies = policies.ToDictionaryByUniqueId(p => p.Name);
    }

    public async Task Invoke(HttpContext context)
    {
        await _next(context);

        var proxyFeature = context.GetReverseProxyFeature();
        var options = proxyFeature.Cluster.Config.HealthCheck?.Passive;

        // Do nothing if no target destination has been chosen for the request.
        if (options is null || !options.Enabled.GetValueOrDefault() || proxyFeature.ProxiedDestination is null)
        {
            return;
        }

        var policy = _policies.GetRequiredServiceById(options.Policy, HealthCheckConstants.PassivePolicy.TransportFailureRate);
        var cluster = context.GetRouteModel().Cluster!;
        policy.RequestProxied(context, cluster, proxyFeature.ProxiedDestination);
    }
}
