// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

var builder = WebApplication.CreateBuilder(args);

using var serilog = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .Enrich.FromLogContext()
    .WriteTo.Console(theme: AnsiConsoleTheme.Code)
    .CreateLogger();
builder.Logging.ClearProviders();
builder.Logging.AddSerilog(serilog, dispose: false);

builder.Configuration.AddJsonFile("/app/config/yarp.json", optional: true);
builder.WebHost.UseKubernetesReverseProxyCertificateSelector();
builder.Services.AddKubernetesReverseProxy(builder.Configuration);

var app = builder.Build();

app.MapReverseProxy();

app.Run();
