// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Yarp.Application.Configuration;

namespace Yarp.Application.Features;

public static class ErrorPagesFeature
{
    /// <summary>
    /// Re-executes the request against a configured page when an empty 4xx/5xx response is
    /// produced downstream. The original status code is preserved (the browser still sees
    /// 404, 500, etc.). Keys are 3-digit status codes (<c>"404"</c>) or class wildcards
    /// (<c>"4xx"</c>, <c>"5xx"</c>); exact codes win over wildcards.
    /// </summary>
    public static WebApplication UseErrorPages(this WebApplication app, YarpAppConfig config)
    {
        if (config.ErrorPages.Count == 0)
        {
            return app;
        }

        var rules = ErrorPageRules.Compile(config.ErrorPages);

        app.UseStatusCodePages(async context =>
        {
            var http = context.HttpContext;
            var path = rules.Resolve(http.Response.StatusCode);
            if (path is null)
            {
                return;
            }

            // StatusCodePages runs after the downstream pipeline has produced an error response.
            // Mirror UseStatusCodePagesWithReExecute, but choose the destination from the status
            // code map instead of using one template for every status.
            var originalPath = http.Request.Path;
            var originalQueryString = http.Request.QueryString;
            var originalStatusCode = http.Response.StatusCode;
            var originalEndpoint = http.GetEndpoint();
            var routeValuesFeature = http.Features.Get<IRouteValuesFeature>();
            var originalRouteValues = routeValuesFeature?.RouteValues is { } routeValues
                ? new RouteValueDictionary(routeValues)
                : null;

            // StatusCodeReExecuteFeature has an internal/private setter for OriginalStatusCode
            // in some target frameworks. Use our own feature so custom error endpoints can still
            // inspect the original status if they need it.
            http.Features.Set<IStatusCodeReExecuteFeature>(new ErrorPageReExecuteFeature
            {
                OriginalPathBase = http.Request.PathBase.Value!,
                OriginalPath = originalPath.Value!,
                OriginalQueryString = originalQueryString.HasValue ? originalQueryString.Value : null,
                OriginalStatusCode = originalStatusCode,
                Endpoint = originalEndpoint,
                RouteValues = originalRouteValues,
            });

            // Routing has already selected an endpoint for the original request. Clear it so the
            // re-executed path can be matched as a new request against redirects/static/proxy/
            // fallback endpoints.
            http.SetEndpoint(null);
            http.Features.Get<IRouteValuesFeature>()?.RouteValues?.Clear();

            // The response currently contains the original error status (for example 404). Clear
            // it before re-executing so the target can run normally. This is especially important
            // for YARP proxy targets because the forwarder refuses to start when the response has
            // already been set to a non-200 status.
            http.Response.Clear();

            // Error page targets typically produce a 200 response (static files, proxy backends,
            // etc.). Restore the original status immediately before headers are sent so the client
            // sees the original 404/500 while receiving the custom page body.
            http.Response.OnStarting(static state =>
            {
                var (response, statusCode) = ((HttpResponse Response, int StatusCode))state;
                response.StatusCode = statusCode;
                return Task.CompletedTask;
            }, (http.Response, originalStatusCode));

            http.Request.Path = path;
            http.Request.QueryString = QueryString.Empty;
            try
            {
                await context.Next(http);
            }
            finally
            {
                // If the target did not start the response, OnStarting will not run. Preserve the
                // same status-code guarantee for empty/not-started responses.
                if (!http.Response.HasStarted)
                {
                    http.Response.StatusCode = originalStatusCode;
                }

                // Restore request/routing state for anything later in the pipeline and for logging
                // or diagnostics that observe the context after the re-execute completes.
                http.Request.QueryString = originalQueryString;
                http.Request.Path = originalPath;
                http.SetEndpoint(originalEndpoint);
                if (routeValuesFeature is not null)
                {
                    routeValuesFeature.RouteValues = originalRouteValues ?? new RouteValueDictionary();
                }

                http.Features.Set<IStatusCodeReExecuteFeature?>(null);
            }
        });

        return app;
    }

    private sealed class ErrorPageReExecuteFeature : IStatusCodeReExecuteFeature
    {
        public string OriginalPathBase { get; set; } = string.Empty;
        public string OriginalPath { get; set; } = string.Empty;
        public string? OriginalQueryString { get; set; }
        public int OriginalStatusCode { get; init; }
        public Endpoint? Endpoint { get; set; }
        public RouteValueDictionary? RouteValues { get; set; }
    }

    private sealed class ErrorPageRules
    {
        private readonly Dictionary<int, string> _exact;
        private readonly Dictionary<int, string> _classes;

        private ErrorPageRules(Dictionary<int, string> exact, Dictionary<int, string> classes)
        {
            _exact = exact;
            _classes = classes;
        }

        public string? Resolve(int statusCode)
        {
            if (_exact.TryGetValue(statusCode, out var path))
            {
                return path;
            }

            if (_classes.TryGetValue(statusCode / 100, out path))
            {
                return path;
            }

            return null;
        }

        public static ErrorPageRules Compile(IDictionary<string, string> source)
        {
            var exact = new Dictionary<int, string>();
            var classes = new Dictionary<int, string>();

            foreach (var (key, value) in source)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new InvalidOperationException(
                        $"ErrorPages entry '{key}' must have a non-empty path.");
                }

                if (TryParseExactCode(key, out var code))
                {
                    exact[code] = value;
                }
                else if (TryParseClassWildcard(key, out var hundreds))
                {
                    classes[hundreds] = value;
                }
                else
                {
                    throw new InvalidOperationException(
                        $"ErrorPages key '{key}' must be a 3-digit status code (e.g. '404') or a class wildcard (e.g. '5xx').");
                }
            }

            return new ErrorPageRules(exact, classes);
        }

        private static bool TryParseExactCode(string key, out int code)
        {
            code = 0;
            if (key.Length != 3)
            {
                return false;
            }

            for (var i = 0; i < 3; i++)
            {
                if (!char.IsDigit(key[i]))
                {
                    return false;
                }
            }

            code = int.Parse(key, System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }

        private static bool TryParseClassWildcard(string key, out int hundreds)
        {
            hundreds = 0;
            if (key.Length != 3 || !char.IsDigit(key[0]))
            {
                return false;
            }

            if ((key[1] != 'x' && key[1] != 'X') || (key[2] != 'x' && key[2] != 'X'))
            {
                return false;
            }

            hundreds = key[0] - '0';
            return true;
        }
    }
}
