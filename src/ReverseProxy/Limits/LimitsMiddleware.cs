// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;

namespace Yarp.ReverseProxy.Limits;

/// <summary>
/// Updates request limits based on route config. This is implemented as middleware at the end of the proxy
/// pipeline so that apps can call ReassignProxyRequest to move the request to a different route before limits are applied. While similar to a proposed aspnetcore feature (https://github.com/dotnet/aspnetcore/issues/40452),
/// the possibility of reassigning routes means we need to apply this limit very late. Trying to apply it twice could
/// result in unexpected behavior like being unable to set it back to the server default.
/// </summary>
internal sealed class LimitsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger _logger;

    public LimitsMiddleware(RequestDelegate next, ILogger<LimitsMiddleware> logger)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(logger);

        _next = next;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task Invoke(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var config = context.GetRouteModel().Config;

        if (config.MaxRequestBodySize.HasValue)
        {
            var sizeFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (sizeFeature != null && !sizeFeature.IsReadOnly)
            {
                // -1 for disabled
                var limit = config.MaxRequestBodySize.Value;
                long? newValue = limit == -1 ? null : limit;
                sizeFeature.MaxRequestBodySize = newValue;
                Log.MaxRequestBodySizeSet(_logger, limit);
            }
        }

        return _next(context);
    }

    private static class Log
    {
        private static readonly Action<ILogger, long?, Exception?> _maxRequestBodySizeSet = LoggerMessage.Define<long?>(
            LogLevel.Debug,
            EventIds.MaxRequestBodySizeSet,
            "The MaxRequestBodySize has been set to '{limit}'.");

        public static void MaxRequestBodySizeSet(ILogger logger, long? limit)
        {
            _maxRequestBodySizeSet(logger, limit, null);
        }
    }
}
