// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Yarp.Application.Configuration;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Model;

namespace Yarp.Application.Features;

public static class RequestLoggingFeature
{
    /// <summary>
    /// HttpContext.Items key under which the static file and navigation fallback handlers record themselves.
    /// Proxy requests are identified by <see cref="IReverseProxyFeature"/> instead.
    /// </summary>
    internal static readonly object HandlerKey = new();

    internal const string StaticHandler = "static";
    internal const string FallbackHandler = "fallback";

    /// <summary>
    /// Adds request logging. Must be called before the static file middleware so static requests are included.
    /// In default mode it only reports proxy forwarding failures.
    /// </summary>
    /// <remarks>
    /// Output is written directly to the console (like the startup banner), not through ILogger,
    /// so it stays single-line and independent of the Logging configuration.
    /// </remarks>
    public static WebApplication UseRequestLogging(this WebApplication app, YarpAppConfig config)
    {
        var logAllRequests = config.Log.Level != LogMode.Default;

        app.Use(async (context, next) =>
        {
            // Capture before next(): default-file middleware rewrites Path (/ -> /index.html).
            // ToUriComponent re-escapes the decoded path so it can't inject newlines. The query string is never logged.
            var path = (context.Request.PathBase + context.Request.Path).ToUriComponent();
            var method = context.Request.Method;
            var start = Stopwatch.GetTimestamp();
            var failed = false;

            try
            {
                await next(context);
            }
            catch
            {
                failed = true;
                throw;
            }
            finally
            {
                var errorFeature = context.Features.Get<IForwarderErrorFeature>();
                if (logAllRequests || errorFeature is not null)
                {
                    var status = failed && !context.Response.HasStarted ? 500 : context.Response.StatusCode;
                    WriteLine(FormatLine(context, method, path, status, Stopwatch.GetElapsedTime(start), errorFeature));
                }
            }
        });

        return app;
    }

    private static void WriteLine(string line)
    {
        // Console.Out is synchronized, so a single WriteLine keeps concurrent requests from interleaving.
        Console.Out.WriteLine(line);
    }

    internal static string FormatLine(HttpContext context, string method, string path, int status, TimeSpan elapsed, IForwarderErrorFeature? error)
    {
        var proxy = context.Features.Get<IReverseProxyFeature>();
        var handler = proxy is not null ? "proxy" : context.Items[HandlerKey] as string ?? "";

        var size = context.Response.ContentLength is { } length ? $"{length}b" : "";

        var line = $"{method,-4} {path,-24} → {handler,-8} {status}  {size,-6} {(long)elapsed.TotalMilliseconds}ms";

        if (error is not null)
        {
            line += $"  route={proxy?.Route.Config.RouteId} dest={proxy?.ProxiedDestination?.Model.Config.Address} {error.Error}: {Summarize(error.Exception)}";
        }

        return line;
    }

    internal static string Summarize(Exception? exception)
    {
        if (exception is null)
        {
            return "";
        }

        // HttpRequestException messages keep useful context, e.g. "Connection refused (localhost:5100)".
        var message = exception is HttpRequestException ? exception.Message : exception.GetBaseException().Message;

        // Keep to one physical line.
        var end = message.AsSpan().IndexOfAny('\r', '\n');
        if (end >= 0)
        {
            message = message[..end];
        }

        return message.Length > 200 ? message[..200] + "..." : message;
    }
}
