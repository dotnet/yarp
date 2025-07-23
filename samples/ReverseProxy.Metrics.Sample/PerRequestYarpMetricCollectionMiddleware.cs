// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Yarp.Sample
{
    /// <summary>
    ///  Middleware that collects YARP metrics and logs them at the end of each request
    /// </summary>
    public class PerRequestYarpMetricCollectionMiddleware
    {
        // Required for middleware
        private readonly RequestDelegate _next;
        // Supplied via DI
        private readonly ILogger<PerRequestYarpMetricCollectionMiddleware> _logger;

        public PerRequestYarpMetricCollectionMiddleware(RequestDelegate next, ILogger<PerRequestYarpMetricCollectionMiddleware> logger)
        {
            _logger = logger;
            _next = next;
        }

        /// <summary>
        /// Entrypoint for being called as part of the request pipeline
        /// </summary>
        public async Task InvokeAsync(HttpContext context)
        {
            var metrics = PerRequestMetrics.Current;
            metrics.StartTime = DateTime.UtcNow;

            // Call the next steps in the middleware, including the proxy
            await _next(context);

            // Called after the other middleware steps have completed
            // Write the info to the console via ILogger. In a production scenario you probably want
            // to write the results to your telemetry systems directly.
            _logger.LogInformation("PerRequestMetrics: " + metrics.ToJson());
        }
    }

    /// <summary>
    /// Helper to aid with registration of the middleware
    /// </summary>
    public static class YarpMetricCollectionMiddlewareHelper
    {
        public static IApplicationBuilder UsePerRequestMetricCollection(
          this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<PerRequestYarpMetricCollectionMiddleware>();
        }
    }
}

