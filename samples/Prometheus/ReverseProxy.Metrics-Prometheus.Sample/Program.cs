// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Prometheus;
using Yarp.Sample;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Enable metric collection for all the underlying event counters used by YARP
builder.Services.AddAllPrometheusMetrics();

var app = builder.Build();

// Add the reverse proxy endpoints based on routes
app.MapReverseProxy();

// Add the /Metrics endpoint for prometheus to query on
app.MapMetrics();

app.Run();
