// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System.Threading;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Model;

/// <summary>
/// Initializes the proxy processing pipeline with the available healthy destinations.
/// </summary>
internal sealed class ProxyPipelineInitializerMiddleware
{
    private readonly ILogger _logger;
    private readonly RequestDelegate _next;
    private readonly IOptionsMonitor<RequestTimeoutOptions> _timeoutOptions;

    public ProxyPipelineInitializerMiddleware(RequestDelegate next, ILogger<ProxyPipelineInitializerMiddleware> logger, IOptionsMonitor<RequestTimeoutOptions> timeoutOptions)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(timeoutOptions);

        _logger = logger;
        _next = next;
        _timeoutOptions = timeoutOptions;
    }

    public Task Invoke(HttpContext context)
    {
        var endpoint = context.GetEndpoint()
           ?? throw new InvalidOperationException($"Routing Endpoint wasn't set for the current request.");

        var route = endpoint.Metadata.GetMetadata<RouteModel>()
            ?? throw new InvalidOperationException($"Routing Endpoint is missing {typeof(RouteModel).FullName} metadata.");

        var cluster = route.Cluster;
        // TODO: Validate on load https://github.com/dotnet/yarp/issues/797
        if (cluster is null)
        {
            Log.NoClusterFound(_logger, route.Config.RouteId);
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return Task.CompletedTask;
        }

        EnsureRequestTimeoutPolicyIsAppliedCorrectly(context, endpoint, route);

        var destinationsState = cluster.DestinationsState;
        context.Features.Set<IReverseProxyFeature>(new ReverseProxyFeature
        {
            Route = route,
            Cluster = cluster.Model,
            AllDestinations = destinationsState.AllDestinations,
            AvailableDestinations = destinationsState.AvailableDestinations,
        });

        var activity = Observability.YarpActivitySource.CreateActivity("proxy.forwarder", ActivityKind.Server);

        return activity is null
            ? _next(context)
            : AwaitWithActivity(context, activity);
    }

    private async Task AwaitWithActivity(HttpContext context, Activity activity)
    {
        context.SetYarpActivity(activity);

        activity.Start();
        try
        {
            await _next(context);
        }
        finally
        {
            activity.Dispose();
        }
    }

    private void EnsureRequestTimeoutPolicyIsAppliedCorrectly(HttpContext context, Endpoint endpoint, RouteModel route)
    {
        // There's no way to detect the presence of the timeout middleware before this, only the options.
        if (endpoint.Metadata.GetMetadata<RequestTimeoutAttribute>() is { } requestTimeout &&
            context.Features.Get<IHttpRequestTimeoutFeature>() is null &&
            // The feature is skipped if the request is already canceled. We'll handle canceled requests later for consistency.
            !context.RequestAborted.IsCancellationRequested &&
            // The policy may set the timeout to null / infinite.
            TimeoutPolicyRequestedATimeoutBeSet(requestTimeout))
        {
            // A timeout should have been set.
            // Out of an abundance of caution, refuse the request rather than allowing it to proceed without the configured timeout.
            ThrowIfDebuggerNotAttached(route);
        }

        void ThrowIfDebuggerNotAttached(RouteModel route)
        {
            // The feature is skipped if the debugger is attached.
            if (!Debugger.IsAttached)
            {
                Log.TimeoutNotApplied(_logger, route.Config.RouteId);

                throw new InvalidOperationException(
                    $"The timeout was not applied for route '{route.Config.RouteId}', " +
                    "ensure `IApplicationBuilder.UseRequestTimeouts()` is called between " +
                    "`IApplicationBuilder.UseRouting()` and `IApplicationBuilder.UseEndpoints()`.");
            }
        }
    }

    private bool TimeoutPolicyRequestedATimeoutBeSet(RequestTimeoutAttribute requestTimeout)
    {
        if (requestTimeout.Timeout is not TimeSpan timeout)
        {
            if (requestTimeout.PolicyName is not string policyName)
            {
                Debug.Fail("Either Timeout or PolicyName should have been set.");
                return false;
            }

            if (!_timeoutOptions.CurrentValue.Policies.TryGetValue(policyName, out var policy))
            {
                // This should only happen if the policy existed at some point, but the options were updated to remove it.
                return false;
            }

            if (policy.Timeout is null)
            {
                // The policy requested no timeout.
                return false;
            }

            timeout = policy.Timeout.Value;
        }

        return timeout != Timeout.InfiniteTimeSpan;
    }

    private static class Log
    {
        private static readonly Action<ILogger, string, Exception?> _noClusterFound = LoggerMessage.Define<string>(
            LogLevel.Information,
            EventIds.NoClusterFound,
            "Route '{routeId}' has no cluster information.");

        private static readonly Action<ILogger, string, Exception?> _timeoutNotApplied = LoggerMessage.Define<string>(
            LogLevel.Error,
            EventIds.TimeoutNotApplied,
            "The timeout was not applied for route '{routeId}', ensure `IApplicationBuilder.UseRequestTimeouts()` is called between `IApplicationBuilder.UseRouting()` and `IApplicationBuilder.UseEndpoints()`.");

        public static void NoClusterFound(ILogger logger, string routeId)
        {
            _noClusterFound(logger, routeId, null);
        }

        public static void TimeoutNotApplied(ILogger logger, string routeId)
        {
            _timeoutNotApplied(logger, routeId, null);
        }
    }
}
