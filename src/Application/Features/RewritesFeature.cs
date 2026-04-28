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

            options.AddRewrite(rule.Regex, rule.Replacement, rule.SkipRemainingRules);
        }

        app.UseRewriter(options);
        return app;
    }
}
