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

    public Task Invoke(HttpContext context)
    {
        // Fast path: when passive health checking isn't engaged for this request's cluster (the default),
        // return the downstream task directly instead of running as an `async` method. Awaiting `_next`
        // here would force a per-request async state-machine allocation on every proxied request even
        // though there is no post-processing to do. Whether passive health is engaged is a per-cluster
        // property read from the feature at entry; like SessionAffinityMiddleware and LimitsMiddleware,
        // this reflects any ReassignProxyRequest that (per the documented pattern) runs earlier in the
        // pipeline. The outcome is still recorded against the post-`_next` cluster/destination below.
        var proxyFeature = context.GetReverseProxyFeature();
        var options = proxyFeature.Cluster.Config.HealthCheck?.Passive;

        if (options is null || !options.Enabled.GetValueOrDefault())
        {
            return _next(context);
        }

        return InvokeAwaited(context);
    }

    private async Task InvokeAwaited(HttpContext context)
    {
        await _next(context);

        // Re-read after _next so the outcome is recorded against the cluster/destination the request was
        // actually forwarded to (honoring ReassignProxyRequest), identical to the original behavior.
        var proxyFeature = context.GetReverseProxyFeature();
        var options = proxyFeature.Cluster.Config.HealthCheck?.Passive;

        // Do nothing if passive health was turned off by a reassignment, or no destination was chosen.
        if (options is null || !options.Enabled.GetValueOrDefault() || proxyFeature.ProxiedDestination is null)
        {
            return;
        }

        var policy = _policies.GetRequiredServiceById(options.Policy, HealthCheckConstants.PassivePolicy.TransportFailureRate);
        var cluster = context.GetRouteModel().Cluster!;
        policy.RequestProxied(context, cluster, proxyFeature.ProxiedDestination);
    }
}
