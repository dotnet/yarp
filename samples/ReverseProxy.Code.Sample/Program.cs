// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Configuration;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Model;

const string DEBUG_HEADER = "Debug";
const string DEBUG_METADATA_KEY = "debug";
const string DEBUG_VALUE = "true";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddReverseProxy()
    .LoadFromMemory(GetRoutes(), GetClusters());

var app = builder.Build();

app.Map("/update", context =>
{
    context.RequestServices.GetRequiredService<InMemoryConfigProvider>().Update(GetRoutes(), GetClusters());
    return Task.CompletedTask;
});
// We can customize the proxy pipeline and add/remove/replace steps
app.MapReverseProxy(proxyPipeline =>
{
    // Use a custom proxy middleware, defined below
    proxyPipeline.Use(MyCustomProxyStep);
    // Don't forget to include these two middleware when you make a custom proxy pipeline (if you need them).
    proxyPipeline.UseSessionAffinity();
    proxyPipeline.UseLoadBalancing();
});

app.Run();

RouteConfig[] GetRoutes()
{
    return
    [
        new RouteConfig()
        {
            RouteId = "route" + Random.Shared.Next(), // Forces a new route id each time GetRoutes is called.
            ClusterId = "cluster1",
            Match = new RouteMatch
            {
                // Path or Hosts are required for each route. This catch-all pattern matches all request paths.
                Path = "{**catch-all}"
            }
        }
    ];
}

ClusterConfig[] GetClusters()
{
    var debugMetadata = new Dictionary<string, string>
    {
        { DEBUG_METADATA_KEY, DEBUG_VALUE }
    };

    return
    [
        new ClusterConfig()
        {
            ClusterId = "cluster1",
            SessionAffinity = new SessionAffinityConfig { Enabled = true, Policy = "Cookie", AffinityKeyName = ".Yarp.ReverseProxy.Affinity" },
            Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
            {
                { "destination1", new DestinationConfig() { Address = "https://example.com" } },
                { "debugdestination1", new DestinationConfig() {
                    Address = "https://bing.com",
                    Metadata = debugMetadata  }
                },
            }
        }
    ];
}

/// <summary>
/// Custom proxy step that filters destinations based on a header in the inbound request
/// Looks at each destination metadata, and filters in/out based on their debug flag and the inbound header
/// </summary>
Task MyCustomProxyStep(HttpContext context, Func<Task> next)
{
    // Can read data from the request via the context
    var useDebugDestinations = context.Request.Headers.TryGetValue(DEBUG_HEADER, out var headerValues) && headerValues.Count == 1 && headerValues[0] == DEBUG_VALUE;

    // The context also stores a ReverseProxyFeature which holds proxy specific data such as the cluster, route and destinations
    var availableDestinationsFeature = context.Features.Get<IReverseProxyFeature>();
    var filteredDestinations = new List<DestinationState>();

    // Filter destinations based on criteria
    foreach (var d in availableDestinationsFeature.AvailableDestinations)
    {
        //Todo: Replace with a lookup of metadata - but not currently exposed correctly here
        if (d.DestinationId.Contains("debug") == useDebugDestinations) { filteredDestinations.Add(d); }
    }
    availableDestinationsFeature.AvailableDestinations = filteredDestinations;

    // Important - required to move to the next step in the proxy pipeline
    return next();
}
