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

            // Mirror the re-execute pattern from
            // Microsoft.AspNetCore.Diagnostics.StatusCodePagesExtensions.UseStatusCodePagesWithReExecute,
            // adapted to look up the destination per status code instead of using a single template.
            var originalPath = http.Request.Path;
            var originalQueryString = http.Request.QueryString;

            http.Features.Set<IStatusCodeReExecuteFeature>(new StatusCodeReExecuteFeature
            {
                OriginalPathBase = http.Request.PathBase.Value!,
                OriginalPath = originalPath.Value!,
                OriginalQueryString = originalQueryString.HasValue ? originalQueryString.Value : null,
            });

            // Clear the chosen endpoint and route values so the re-executed request can be
            // matched fresh against routing/static-files.
            http.SetEndpoint(null);
            http.Features.Get<IRouteValuesFeature>()?.RouteValues?.Clear();

            http.Request.Path = path;
            http.Request.QueryString = QueryString.Empty;
            try
            {
                await context.Next(http);
            }
            finally
            {
                http.Request.QueryString = originalQueryString;
                http.Request.Path = originalPath;
                http.Features.Set<IStatusCodeReExecuteFeature?>(null);
            }
        });

        return app;
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
