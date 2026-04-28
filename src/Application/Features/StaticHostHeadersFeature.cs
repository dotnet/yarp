// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Yarp.Application.Configuration;

namespace Yarp.Application.Features;

public static class StaticHostHeadersFeature
{
    public static WebApplication UseStaticHostHeaders(this WebApplication app, YarpAppConfig config)
    {
        var headerRules = CompileHeaderRules(config);
        if (headerRules.Length == 0)
        {
            return app;
        }

        app.Use((context, next) =>
        {
            // This middleware runs after UseRouting. The SPA fallback endpoint carries explicit
            // metadata, while static files use OnPrepareResponse because they are not endpoints.
            if (context.GetEndpoint()?.Metadata.GetMetadata<NavigationFallbackEndpointMetadata>() is null)
            {
                return next();
            }

            var requestPath = context.Request.Path;

            context.Response.OnStarting(
                static state =>
                {
                    var (httpContext, originalPath, rules) = ((HttpContext, PathString, CompiledHeaderRule[]))state;
                    ApplyHeaders(originalPath, httpContext.Response.Headers, rules);
                    return Task.CompletedTask;
                },
                (context, requestPath, headerRules));

            return next();
        });

        return app;
    }

    internal static Action<StaticFileResponseContext>? CreateStaticFileHeaderCallback(YarpAppConfig config)
    {
        var headerRules = CompileHeaderRules(config);
        if (headerRules.Length == 0)
        {
            return null;
        }

        // StaticFileMiddleware doesn't create endpoints, so apply the same header rules through
        // OnPrepareResponse to keep static files and SPA fallback behavior aligned.
        return context => ApplyHeaders(context.Context.Request.Path, context.Context.Response.Headers, headerRules);
    }

    private static CompiledHeaderRule[] CompileHeaderRules(YarpAppConfig config)
        => config.Headers.Select(rule => new CompiledHeaderRule(rule)).ToArray();

    private static void ApplyHeaders(PathString requestPath, IHeaderDictionary headers, CompiledHeaderRule[] headerRules)
    {
        foreach (var headerRule in headerRules)
        {
            headerRule.Apply(requestPath, headers);
        }
    }

    private sealed class CompiledHeaderRule
    {
        private readonly RequestMatchEvaluator _matcher;
        private readonly KeyValuePair<string, string>[] _headers;

        public CompiledHeaderRule(HeaderRule rule)
        {
            if (rule.Set.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Header rule '{rule.Match.Path ?? "<missing path>"}' must set at least one header.");
            }

            _matcher = new RequestMatchEvaluator(rule.Match, "Header rules");
            _headers = rule.Set
                .Select(pair =>
                {
                    if (string.IsNullOrWhiteSpace(pair.Key))
                    {
                        throw new InvalidOperationException(
                            $"Header rule '{rule.Match.Path ?? "<missing path>"}' contains an empty header name.");
                    }

                    if (pair.Value is null)
                    {
                        throw new InvalidOperationException(
                            $"Header rule '{rule.Match.Path ?? "<missing path>"}' contains a null value for header '{pair.Key}'.");
                    }

                    return new KeyValuePair<string, string>(pair.Key, pair.Value);
                })
                .ToArray();
        }

        public void Apply(PathString requestPath, IHeaderDictionary headers)
        {
            if (!_matcher.TryMatch(requestPath, new()))
            {
                return;
            }

            foreach (var header in _headers)
            {
                // Rules are additive at the config level, but later matches overwrite the same
                // header name so users can layer broad defaults with narrow exceptions.
                headers[header.Key] = header.Value;
            }
        }
    }
}
