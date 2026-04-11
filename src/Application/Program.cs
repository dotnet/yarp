// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Yarp.Application.Configuration;
using Yarp.Application.Features;

var builder = WebApplication.CreateBuilder();

// Load configuration from file if passed
if (args.Length == 1)
{
    var configFile = args[0];
    var fileInfo = new FileInfo(configFile);
    if (!fileInfo.Exists)
    {
        Console.Error.WriteLine($"Could not find '{configFile}'.");
        return 2;
    }
    builder.Configuration.AddJsonFile(fileInfo.FullName, optional: false, reloadOnChange: true);
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
