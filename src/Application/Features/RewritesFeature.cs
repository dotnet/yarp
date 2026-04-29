// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Rewrite;
using Yarp.Application.Configuration;

namespace Yarp.Application.Features;

public static class RewritesFeature
{
    /// <summary>
    /// Wires <see cref="RewriteOptionsExtensions.AddRewrite"/> rules into the pipeline before
    /// <c>UseRouting</c>, so every downstream stage (route matching, static files, redirects,
    /// fallback, proxy) sees the rewritten path. Uses the standard ASP.NET rewrite-middleware
    /// regex/<c>$n</c> syntax — no custom matching.
    /// </summary>
    public static WebApplication UseRewrites(this WebApplication app, YarpAppConfig config)
    {
        if (config.Rewrites.Count == 0)
        {
            return app;
        }

        var options = new RewriteOptions();
        for (var i = 0; i < config.Rewrites.Count; i++)
        {
            var rule = config.Rewrites[i];
            if (string.IsNullOrWhiteSpace(rule.Regex))
            {
                throw new InvalidOperationException(
                    $"Rewrite rule at index {i} requires a non-empty Regex.");
            }

            if (rule.Replacement is null)
            {
                throw new InvalidOperationException(
                    $"Rewrite rule '{rule.Regex}' requires a Replacement.");
            }

            try
            {
                // Parse the ASP.NET rewrite-middleware regex/replacement pair. Example:
                // Regex "^docs/current/(.*)$" and Replacement "docs/v2/$1" rewrites
                // "/docs/current/intro.html" to "/docs/v2/intro.html".
                options.AddRewrite(rule.Regex, rule.Replacement, rule.SkipRemainingRules);
            }
            catch (ArgumentException ex)
            {
                throw new InvalidOperationException(
                    $"Rewrite rule at index {i} has an invalid Regex pattern '{rule.Regex}': {ex.Message}", ex);
            }
        }

        app.UseRewriter(options);
        return app;
    }
}
