// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Xunit;
using Yarp.Application.Features;

namespace Yarp.Application.Tests;

// Request/error lines and console logs are written to the process-wide Console,
// so these tests must not run in parallel with other tests.
[CollectionDefinition(nameof(ConsoleCollection), DisableParallelization = true)]
public sealed class ConsoleCollection;

[Collection(nameof(ConsoleCollection))]
public class LoggingTests
{
    private sealed class Backend : IAsyncDisposable
    {
        private readonly WebApplication? _app;

        public string Address { get; }

        private Backend(WebApplication? app, string address)
        {
            _app = app;
            Address = address;
        }

        // A running backend that answers every request with 200.
        public static async Task<Backend> StartAsync()
        {
            var builder = WebApplication.CreateSlimBuilder();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            var app = builder.Build();
            app.Run(context => context.Response.WriteAsync("backend"));
            await app.StartAsync();
            return new Backend(app, app.Urls.First() + "/");
        }

        // An address nothing listens on, so connecting fails.
        public static Backend Unreachable()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return new Backend(null, $"http://127.0.0.1:{port}/");
        }

        public ValueTask DisposeAsync() => _app?.DisposeAsync() ?? ValueTask.CompletedTask;
    }

    private static Dictionary<string, string?> Config(string? level, string? backendAddress = null)
    {
        var config = new Dictionary<string, string?>
        {
            ["StaticFiles:Enabled"] = "true",
            ["NavigationFallback:Path"] = "/index.html",
        };

        if (level is not null)
        {
            config["Log:Level"] = level;
        }

        if (backendAddress is not null)
        {
            config["ReverseProxy:Routes:api:ClusterId"] = "backend";
            config["ReverseProxy:Routes:api:Match:Path"] = "/api/{**catch-all}";
            config["ReverseProxy:Clusters:backend:Destinations:d1:Address"] = backendAddress;
        }

        return config;
    }

    /// <summary>
    /// Runs the app, performs the requests and returns everything written to the console.
    /// The host is disposed before returning so the console logger has flushed.
    /// </summary>
    private static async Task<string> RunAsync(
        Dictionary<string, string?> config,
        Func<HttpClient, Task> requests,
        Action<IWebHostBuilder>? configureHost = null)
    {
        var original = Console.Out;
        var captured = new StringWriter();
        Console.SetOut(TextWriter.Synchronized(captured));
        try
        {
            var factory = new YarpTestApp();
            factory.Configure(config);
            await using (factory)
            {
                var host = configureHost is null ? factory : factory.WithWebHostBuilder(configureHost);
                using var client = host.CreateClient();
                await requests(client);
            }
        }
        finally
        {
            Console.SetOut(original);
        }

        return captured.ToString();
    }

    private static string[] RequestLines(string output) =>
        output.Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Contains(" → ")).ToArray();

    private static string SingleRequestLine(string output)
    {
        var line = Assert.Single(RequestLines(output));
        return line;
    }

    // requests mode

    [Fact]
    public async Task Requests_Static()
    {
        var output = await RunAsync(Config("requests"), c => c.GetAsync("/style.css"));

        var line = SingleRequestLine(output);
        Assert.StartsWith("GET  /style.css", line);
        Assert.Contains("→ static", line);
        Assert.Contains(" 200 ", line);
        Assert.Matches(@"\d+b\s+\d+ms$", line);
    }

    [Fact]
    public async Task Requests_DefaultDocument_LogsOriginalPath()
    {
        var output = await RunAsync(Config("requests"), c => c.GetAsync("/"));

        var line = SingleRequestLine(output);
        Assert.StartsWith("GET  / ", line);
        Assert.DoesNotContain("index.html", line);
        Assert.Contains("→ static", line);
    }

    [Fact]
    public async Task Requests_Fallback()
    {
        var output = await RunAsync(Config("requests"), c => c.GetAsync("/some/spa/route"));

        var line = SingleRequestLine(output);
        Assert.Contains("/some/spa/route", line);
        Assert.Contains("→ fallback", line);
        Assert.Contains(" 200 ", line);
    }

    [Fact]
    public async Task Requests_Unhandled404_HasBlankHandler()
    {
        var output = await RunAsync(Config("requests"), c => c.GetAsync("/missing.js"));

        var line = SingleRequestLine(output);
        Assert.Matches(@"/missing\.js\s+→\s+404 ", line);
    }

    [Fact]
    public async Task Requests_Proxy_Success_DoesNotLogQueryString()
    {
        await using var backend = await Backend.StartAsync();

        var output = await RunAsync(Config("requests", backend.Address),
            c => c.GetAsync("/api/users?token=secret-value"));

        var line = SingleRequestLine(output);
        Assert.Contains("GET  /api/users", line);
        Assert.Contains("→ proxy", line);
        Assert.Contains(" 200 ", line);
        Assert.DoesNotContain("secret-value", output);
        Assert.DoesNotContain("route=", line);
    }

    [Fact]
    public async Task Requests_Proxy_Failure_IsOneCondensedLine()
    {
        await using var backend = Backend.Unreachable();

        var output = await RunAsync(Config("requests", backend.Address), async c =>
        {
            var response = await c.GetAsync("/api/users?token=secret-value");
            Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        });

        AssertCondensedProxyFailure(output, backend.Address);
        Assert.DoesNotContain("secret-value", output);
    }

    // default mode

    [Fact]
    public async Task Default_SuccessfulRequests_AreSilent()
    {
        await using var backend = await Backend.StartAsync();

        var output = await RunAsync(Config(null, backend.Address), async c =>
        {
            await c.GetAsync("/style.css");
            await c.GetAsync("/some/spa/route");
            await c.GetAsync("/missing.js");
            await c.GetAsync("/api/users");
        });

        Assert.Empty(RequestLines(output));
        Assert.Contains("YARP", output); // banner still printed
    }

    [Fact]
    public async Task Default_ProxyFailure_IsOneCondensedLine()
    {
        await using var backend = Backend.Unreachable();

        var output = await RunAsync(Config(null, backend.Address), c => c.GetAsync("/api/users"));

        AssertCondensedProxyFailure(output, backend.Address);
    }

    [Fact]
    public async Task ProxyFailure_NonConsoleProviders_StillReceiveForwardingError()
    {
        await using var backend = Backend.Unreachable();
        var provider = new CapturingLoggerProvider();

        await RunAsync(Config(null, backend.Address), c => c.GetAsync("/api/users"),
            host => host.ConfigureLogging(l => l.AddProvider(provider)));

        var entry = Assert.Single(provider.Entries, e => e.EventId.Id == 48);
        Assert.Equal("Yarp.ReverseProxy.Forwarder.HttpForwarder", entry.Category);
        Assert.NotNull(entry.Exception);
    }

    // debug mode

    [Fact]
    public async Task Debug_DumpsResolvedConfig_AndKeepsFrameworkLogs()
    {
        await using var backend = await Backend.StartAsync();

        var output = await RunAsync(Config("debug", backend.Address), c => c.GetAsync("/api/users"));

        Assert.Contains("\"Level\": \"Debug\"", output);
        Assert.Contains("\"StaticFiles\"", output);
        Assert.Contains("\"Path\": \"/index.html\"", output);
        // Only the app config is dumped, never raw configuration such as the ReverseProxy section.
        Assert.DoesNotContain("\"ReverseProxy\"", output);
        // Framework logs are not suppressed
        Assert.Contains("Microsoft.AspNetCore.Hosting.Diagnostics", output);
        Assert.Contains("→ proxy", output);
    }

    [Fact]
    public async Task Debug_DoesNotSuppressForwardingError()
    {
        await using var backend = Backend.Unreachable();

        var output = await RunAsync(Config("debug", backend.Address), c => c.GetAsync("/api/users"));

        // The regular framework log for the forwarding error, with its exception and stack trace
        Assert.Contains("Yarp.ReverseProxy.Forwarder.HttpForwarder[48]", output);
        Assert.Contains("Exception", output);
        Assert.Matches(@"(?m)^\s+at \S", output);
        // ... in addition to the condensed request line
        Assert.Contains("→ proxy", output);
    }

    // Logging overrides

    [Fact]
    public async Task ConsoleLogLevelOverride_IsHonored_InDefaultMode()
    {
        var config = Config(null);
        config["Logging:Console:LogLevel:Microsoft.AspNetCore"] = "Information";

        var output = await RunAsync(config, c => c.GetAsync("/style.css"));

        Assert.Contains("Microsoft.AspNetCore.Hosting.Diagnostics", output);
    }

    [Fact]
    public async Task GeneralLogLevelOverride_IsHonored_InDefaultMode()
    {
        var config = Config(null);
        config["Logging:LogLevel:Microsoft.AspNetCore"] = "Information";

        var output = await RunAsync(config, c => c.GetAsync("/style.css"));

        Assert.Contains("Microsoft.AspNetCore.Hosting.Diagnostics", output);
    }

    [Fact]
    public async Task GeneralLogLevelOverride_ForMoreSpecificCategory_IsHonored_InDefaultMode()
    {
        var config = Config(null);
        config["Logging:LogLevel:Microsoft.AspNetCore.Hosting.Diagnostics"] = "Information";

        var output = await RunAsync(config, c => c.GetAsync("/style.css"));

        Assert.Contains("Microsoft.AspNetCore.Hosting.Diagnostics", output);
    }

    [Fact]
    public async Task ConsoleLogLevel_TakesPrecedenceOverGeneralLogLevel()
    {
        var config = Config(null);
        config["Logging:LogLevel:Microsoft.AspNetCore.Hosting"] = "Information";
        config["Logging:Console:LogLevel:Microsoft.AspNetCore"] = "Warning";

        var output = await RunAsync(config, c => c.GetAsync("/style.css"));

        Assert.DoesNotContain("Microsoft.AspNetCore.Hosting.Diagnostics", output);
    }

    [Theory]
    [InlineData("Logging:LogLevel:Default")]
    [InlineData("Logging:LogLevel:Microsoft")]
    public async Task GeneralParentOrDefaultLevel_DoesNotOverrideMoreSpecificConsoleDefault(string key)
    {
        // As with ordinary logging config, the more specific category (Microsoft.AspNetCore) wins.
        var config = Config(null);
        config[key] = "Information";

        var output = await RunAsync(config, c => c.GetAsync("/style.css"));

        Assert.DoesNotContain("Microsoft.AspNetCore.Hosting.Diagnostics", output);
    }

    [Fact]
    public async Task ExplicitConsoleFormatter_IsNotReplaced()
    {
        await using var backend = Backend.Unreachable();
        var config = Config(null, backend.Address);
        config["Logging:Console:FormatterName"] = "simple";

        var output = await RunAsync(config, c => c.GetAsync("/api/users"));

        // Explicitly choosing the standard formatter keeps the standard forwarding error log
        Assert.Contains("Yarp.ReverseProxy.Forwarder.HttpForwarder[48]", output);
    }

    [Fact]
    public async Task Default_SuppressesFrameworkLogs_WithoutOverride()
    {
        var output = await RunAsync(Config(null), c => c.GetAsync("/style.css"));

        Assert.DoesNotContain("Microsoft.AspNetCore.Hosting.Diagnostics", output);
    }

    // formatter

    [Fact]
    public void Formatter_OnlyDropsForwardingError()
    {
        var inner = new RecordingFormatter();
        var services = new ServiceCollection().AddSingleton<ConsoleFormatter>(inner).BuildServiceProvider();
        var formatter = new CondensedConsoleFormatter(services);

        Write(formatter, "Yarp.ReverseProxy.Forwarder.HttpForwarder", 48);
        Assert.Equal(0, inner.Count);

        // Other HttpForwarder events and the same id from other categories still pass through
        Write(formatter, "Yarp.ReverseProxy.Forwarder.HttpForwarder", 49);
        Write(formatter, "Some.Other.Category", 48);
        Assert.Equal(2, inner.Count);
    }

    private static void Write(ConsoleFormatter formatter, string category, int eventId)
    {
        var entry = new LogEntry<string>(LogLevel.Warning, category, new EventId(eventId), "message", null, (s, _) => s);
        formatter.Write(in entry, null, new StringWriter());
    }

    private static void AssertCondensedProxyFailure(string output, string backendAddress)
    {
        var line = SingleRequestLine(output);
        Assert.Contains("GET  /api/users", line);
        Assert.Contains("→ proxy", line);
        Assert.Contains(" 502 ", line);
        Assert.Contains("route=api", line);
        Assert.Contains($"dest={backendAddress}", line);
        // ForwarderError name, then the HttpRequestException message which includes host:port
        // (the wording of the reason itself is platform specific).
        Assert.Contains("Request: ", line);
        Assert.Contains(new Uri(backendAddress).Authority, line.Substring(line.IndexOf("Request: ", StringComparison.Ordinal)));

        // The verbose forwarder warning and its stack trace are not shown
        Assert.DoesNotContain("HttpForwarder", output);
        Assert.DoesNotContain("SocketException", output);
        Assert.DoesNotContain("   at ", output);
    }

    private sealed class RecordingFormatter : ConsoleFormatter
    {
        public int Count { get; private set; }

        public RecordingFormatter() : base(ConsoleFormatterNames.Simple) { }

        public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter) => Count++;
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public List<(string Category, EventId EventId, Exception? Exception)> Entries { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, Entries);

        public void Dispose() { }

        private sealed class CapturingLogger(string category, List<(string, EventId, Exception?)> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                lock (entries)
                {
                    entries.Add((category, eventId, exception));
                }
            }
        }
    }
}
