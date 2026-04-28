// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Yarp.Application.Configuration;

namespace Yarp.Application.Features;

public static class RedirectsFeature
{
    public static WebApplication MapRedirects(this WebApplication app, YarpAppConfig config)
    {
        if (config.Redirects.Count == 0)
        {
            return app;
        }

        for (var i = 0; i < config.Redirects.Count; i++)
        {
            var rule = new CompiledRedirectRule(config.Redirects[i]);
            app.Map(
                    rule.Path,
                    context =>
                    {
                        context.Response.StatusCode = rule.StatusCode;
                        context.Response.Headers.Location = rule.BuildDestination(context.Request.RouteValues);
                        return Task.CompletedTask;
                    })
                // Redirects need to run ahead of static files, so execute them directly from
                // endpoint routing instead of waiting for the normal endpoint middleware.
                .ShortCircuit()
                .Add(endpointBuilder =>
                {
                    endpointBuilder.DisplayName = $"Redirect {rule.Path}";
                    ((RouteEndpointBuilder)endpointBuilder).Order = -1000 + i;
                });
        }

        return app;
    }

    private sealed class CompiledRedirectRule
    {
        private static readonly HashSet<int> AllowedStatusCodes = [301, 302, 307, 308];

        public CompiledRedirectRule(RedirectRule rule)
        {
            Path = RequestMatchEvaluator.ValidatePath(rule.Match, "Redirect rules");

            if (string.IsNullOrWhiteSpace(rule.Destination))
            {
                throw new InvalidOperationException(
                    $"Redirect rule '{Path}' requires a non-empty Destination.");
            }

            if (!AllowedStatusCodes.Contains(rule.StatusCode))
            {
                throw new InvalidOperationException(
                    $"Redirect rule '{Path}' has unsupported status code '{rule.StatusCode}'. Expected one of: 301, 302, 307, 308.");
            }

            Destination = rule.Destination;
            StatusCode = rule.StatusCode;
        }

        public string Destination { get; }

        public int StatusCode { get; }

        public string Path { get; }

        public string BuildDestination(RouteValueDictionary values)
            => RequestMatchEvaluator.ExpandTemplate(Destination, values);
    }
}
