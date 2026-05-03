// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;
using Yarp.Application.Configuration;

namespace Yarp.Application.Features;

internal sealed class RequestMatchEvaluator
{
    private readonly TemplateMatcher _pathMatcher;

    public RequestMatchEvaluator(RequestMatch match, string ruleDisplayName)
    {
        var path = ValidatePath(match, ruleDisplayName);
        Path = path;
        // Parse ASP.NET route-template syntax such as "/api/{**catch-all}" or
        // "/docs/{slug}" so static-host header rules use the same path semantics as endpoints.
        _pathMatcher = new TemplateMatcher(TemplateParser.Parse(path), new RouteValueDictionary());
    }

    public string Path { get; }

    /// <summary>
    /// Validates that <paramref name="match"/> has a non-empty <see cref="RequestMatch.Path"/>
    /// and returns it. Use this when only the path string is needed (e.g. when matching is
    /// delegated to ASP.NET endpoint routing) and a full <see cref="TemplateMatcher"/> is
    /// unnecessary.
    /// </summary>
    public static string ValidatePath(RequestMatch match, string ruleDisplayName)
    {
        if (match is null)
        {
            throw new InvalidOperationException($"{ruleDisplayName} requires a Match object.");
        }

        if (string.IsNullOrWhiteSpace(match.Path))
        {
            throw new InvalidOperationException($"{ruleDisplayName} requires Match.Path to be set.");
        }

        return match.Path;
    }

    public bool TryMatch(HttpContext context, RouteValueDictionary values)
    {
        ArgumentNullException.ThrowIfNull(context);
        return TryMatch(context.Request.Path, values);
    }

    public bool TryMatch(PathString path, RouteValueDictionary values)
    {
        ArgumentNullException.ThrowIfNull(values);

        // Route matching expects a rooted request path. Normal requests already have one, but
        // normalize the empty-path case so "/" behaves consistently in tests and callbacks.
        path = string.IsNullOrEmpty(path.Value) ? new PathString("/") : path;
        return _pathMatcher.TryMatch(path, values);
    }

    /// <summary>
    /// Substitutes <c>{name}</c> placeholders in <paramref name="template"/> with values from
    /// <paramref name="values"/>. For example, <c>"/docs/{slug}"</c> with slug
    /// <c>"intro"</c> becomes <c>"/docs/intro"</c>. Missing or null values resolve to an
    /// empty string.
    /// </summary>
    public static string ExpandTemplate(string template, RouteValueDictionary values)
    {
        if (values.Count == 0 || template.IndexOf('{') < 0)
        {
            return template;
        }

        var builder = new System.Text.StringBuilder(template);
        foreach (var value in values)
        {
            builder.Replace("{" + value.Key + "}", value.Value?.ToString() ?? string.Empty);
        }

        return builder.ToString();
    }
}
