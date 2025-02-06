// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.Sample;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy2"))
    .AddConfigFilter<CustomConfigFilter>();

var app = builder.Build();

app.MapControllers();
app.MapReverseProxy(proxyPipeline =>
{
    // Custom endpoint selection
    proxyPipeline.Use((context, next) =>
    {
        var someCriteria = false; // MeetsCriteria(context);
        if (someCriteria)
        {
            var availableDestinationsFeature = context.Features.Get<IReverseProxyFeature>();
            var destination = availableDestinationsFeature.AvailableDestinations[0]; // PickDestination(availableDestinationsFeature.Destinations);
                                                                                     // Load balancing will no-op if we've already reduced the list of available destinations to 1.
            availableDestinationsFeature.AvailableDestinations = destination;
        }

        return next();
    });
    proxyPipeline.UseSessionAffinity();
    proxyPipeline.UseLoadBalancing();
    proxyPipeline.UsePassiveHealthChecks();
}).ConfigureEndpoints((builder, route) => builder.WithDisplayName($"ReverseProxy {route.RouteId}-{route.ClusterId}"));

app.Run();
