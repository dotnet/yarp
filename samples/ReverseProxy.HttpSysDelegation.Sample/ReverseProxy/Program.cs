// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

Debug.Assert(OperatingSystem.IsWindows());
builder.WebHost.UseHttpSys();
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.MapReverseProxy(proxyPipeline =>
{
    proxyPipeline.UseSessionAffinity(); // Has no effect on delegation destinations because the response doesn't go through YARP
    proxyPipeline.UseLoadBalancing();
    proxyPipeline.UsePassiveHealthChecks();
    proxyPipeline.UseHttpSysDelegation();
});

app.Run();
