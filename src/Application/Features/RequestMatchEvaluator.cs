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
}
