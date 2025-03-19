// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;


// Load configuration
if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: yarp.exe <config_file>");
    return 1;
}
var configFile = args[0];
var fileInfo = new FileInfo(configFile);
if (!fileInfo.Exists)
{
    Console.Error.WriteLine($"Could not find '{configFile}'.");
    return 2;
}

var builder = WebApplication.CreateBuilder();
builder.Configuration.AddJsonFile(fileInfo.FullName, optional: false, reloadOnChange: true);

// Configure YARP
builder.AddServiceDefaults();
builder.Services.AddServiceDiscovery();
builder.Services.AddReverseProxy()
                .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
                .AddServiceDiscoveryDestinationResolver();

Console.WriteLine(builder.Configuration.GetSection("ReverseProxy").Value);

var app = builder.Build();
app.MapReverseProxy();

await app.RunAsync();

return 0;
