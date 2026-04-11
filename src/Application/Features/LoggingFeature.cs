// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Yarp.Application.Configuration;

namespace Yarp.Application.Features;

public static class LoggingFeature
{
    /// <summary>
    /// Suppress noisy framework logs on the console provider in default mode.
    /// Other providers (OTEL, etc.) still receive everything.
    /// </summary>
    public static ILoggingBuilder ConfigureDefaultLogging(this ILoggingBuilder logging, IConfiguration configuration)
    {
        // Suppress noisy framework logs on console by default
        logging.AddFilter<ConsoleLoggerProvider>("Microsoft.AspNetCore", LogLevel.Warning);
        logging.AddFilter<ConsoleLoggerProvider>("Microsoft.AspNetCore.DataProtection", LogLevel.None);
        logging.AddFilter<ConsoleLoggerProvider>("Microsoft.Hosting.Lifetime", LogLevel.None);
        logging.AddFilter<ConsoleLoggerProvider>("Yarp.ReverseProxy", LogLevel.Warning);

        // Re-add logging configuration so user overrides in the Logging section
        // take precedence over our defaults above
        logging.AddConfiguration(configuration.GetSection("Logging"));

        return logging;
    }

    /// <summary>
    /// Print startup banner showing what's configured.
    /// Written directly to Console so it always shows regardless of log level.
    /// </summary>
    public static void PrintBanner(YarpAppConfig config, string? configFilePath, WebApplication app)
    {
        Console.WriteLine();
        Console.WriteLine("YARP");
        Console.WriteLine();

        if (configFilePath is not null)
        {
            Console.WriteLine($"  Config:         {configFilePath}");
        }

        if (config.StaticFiles.Enabled)
        {
            var webRoot = app.Environment.WebRootPath ?? "(not found)";
            Console.WriteLine($"  Static files:   {webRoot}");
        }

        if (config.NavigationFallback.Path is not null)
        {
            Console.WriteLine($"  SPA fallback:   {config.NavigationFallback.Path}");
        }

        Console.WriteLine();

        // Print listening URLs once the server has started
        app.Lifetime.ApplicationStarted.Register(() =>
        {
            var addresses = app.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()?.Addresses;

            if (addresses is { Count: > 0 })
            {
                foreach (var address in addresses)
                {
                    Console.WriteLine($"  Listening on:   {address}");
                }
                Console.WriteLine();
            }
        });
    }
}
