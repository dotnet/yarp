// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
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
    /// Configure console logging for the selected <see cref="LogMode"/>.
    /// Default/Requests: suppress noisy framework logs and the verbose forwarding error on the console only.
    /// Debug: leave the framework defaults untouched.
    /// Other providers (OTEL, etc.) always receive everything.
    /// </summary>
    public static ILoggingBuilder ConfigureLogging(this ILoggingBuilder logging, LogMode mode, IConfiguration configuration)
    {
        if (mode == LogMode.Debug)
        {
            return logging;
        }

        // Evaluated whenever the logger filter options are (re)built, so it follows configuration reloads.
        logging.Services.Configure<LoggerFilterOptions>(options => AddConsoleDefaultRules(options, configuration));

        // The forwarding error is reported as a single line by request logging instead.
        // Only replace the formatter when the user hasn't chosen one in Logging:Console:FormatterName.
        logging.Services.AddSingleton<ConsoleFormatter>(sp => new CondensedConsoleFormatter(sp));
        logging.Services.PostConfigure<ConsoleLoggerOptions>(options =>
        {
            if (string.IsNullOrEmpty(options.FormatterName)
                && string.IsNullOrEmpty(configuration["Logging:Console:FormatterName"]))
            {
                options.FormatterName = CondensedConsoleFormatter.FormatterName;
            }
        });

        // Re-add logging configuration so user overrides in the Logging section
        // take precedence over our defaults above
        logging.AddConfiguration(configuration.GetSection("Logging"));

        return logging;
    }

    private static readonly (string Category, LogLevel Level)[] ConsoleDefaults =
    [
        ("Microsoft.AspNetCore", LogLevel.Warning),
        ("Microsoft.AspNetCore.DataProtection", LogLevel.None),
        ("Microsoft.Hosting.Lifetime", LogLevel.None),
        ("Yarp.ReverseProxy", LogLevel.Warning),
    ];

    /// <summary>
    /// Adds the quiet console defaults as console-only rules.
    /// The logging system always ranks provider-specific rules above provider-neutral ones, so a plain
    /// Logging:LogLevel entry could never override them. To keep that escape hatch, Logging:LogLevel entries for
    /// the same category as a default (or a more specific one) are mirrored as console rules after the defaults.
    /// The usual longest-category-wins matching then lets them take over. Entries for a parent category
    /// (e.g. "Microsoft") or "Default" do not override a more specific default, as with ordinary logging config.
    /// A Logging:Console:LogLevel entry still outranks a Logging:LogLevel one, so no mirror is added under it.
    /// Logging:Console:LogLevel itself is applied by AddConfiguration, which is registered after this.
    /// </summary>
    private static void AddConsoleDefaultRules(LoggerFilterOptions options, IConfiguration configuration)
    {
        var provider = typeof(ConsoleLoggerProvider).FullName;

        foreach (var (category, level) in ConsoleDefaults)
        {
            options.Rules.Add(new LoggerFilterRule(provider, category, level, filter: null));
        }

        var consoleCategories = configuration.GetSection("Logging:Console:LogLevel").GetChildren().Select(c => c.Key).ToArray();

        foreach (var entry in configuration.GetSection("Logging:LogLevel").GetChildren())
        {
            if (Enum.TryParse<LogLevel>(entry.Value, ignoreCase: true, out var level)
                && ConsoleDefaults.Any(d => IsSameOrChild(entry.Key, d.Category))
                && !consoleCategories.Any(c => c.Equals("Default", StringComparison.OrdinalIgnoreCase) || IsSameOrChild(entry.Key, c)))
            {
                options.Rules.Add(new LoggerFilterRule(provider, entry.Key, level, filter: null));
            }
        }

        static bool IsSameOrChild(string category, string parent) =>
            category.Equals(parent, StringComparison.OrdinalIgnoreCase)
            || category.StartsWith(parent + ".", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Print the resolved application config (only <see cref="YarpAppConfig"/>, never raw IConfiguration) as JSON.
    /// </summary>
    public static void PrintResolvedConfig(YarpAppConfig config)
    {
        Console.WriteLine("  Resolved configuration:");
        Console.WriteLine(JsonSerializer.Serialize(config, YarpAppConfigJsonContext.Default.YarpAppConfig));
        Console.WriteLine();
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
