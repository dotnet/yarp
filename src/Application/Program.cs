// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Yarp.Application.Configuration;
using Yarp.Application.Features;

// Parse config file path from args before creating the builder
// so we can set ContentRoot/WebRoot via WebApplicationOptions
string? configFilePath = null;
if (args.Length == 1)
{
    var fileInfo = new FileInfo(args[0]);
    if (!fileInfo.Exists)
    {
        Console.Error.WriteLine($"Could not find '{args[0]}'.");
        return 2;
    }
    configFilePath = fileInfo.FullName;
}

var options = configFilePath is not null
    ? new WebApplicationOptions
    {
        ContentRootPath = Path.GetDirectoryName(configFilePath)!,
        WebRootPath = Path.Combine(Path.GetDirectoryName(configFilePath)!, "wwwroot")
    }
    : new WebApplicationOptions();

var builder = WebApplication.CreateBuilder(options);

// Load configuration from file if passed
if (configFilePath is not null)
{
    builder.Configuration.AddJsonFile(configFilePath, optional: false, reloadOnChange: true);
    builder.Configuration.AddEnvironmentVariables();
}

// Test hook: allows WebApplicationFactory to inject config before binding
// See: https://github.com/dotnet/aspnetcore/issues/37680
builder.Configuration.AddTestConfiguration();

// Bind config into the object model — single conversion point, before Build()
var config = YarpAppConfigBinder.Bind(builder.Configuration);

// Services
builder.AddServiceDefaults(config);
builder.AddReverseProxy(builder.Configuration);

var app = builder.Build();

// Middleware pipeline — order matters
app.UseStaticFiles(config);
app.UseRouting();
app.MapReverseProxy();
app.MapNavigationFallback(config);

await app.RunAsync();

return 0;

// Make the auto-generated Program class accessible for WebApplicationFactory
public partial class Program { }
