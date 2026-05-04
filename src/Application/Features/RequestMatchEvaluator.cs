// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.Primitives;
using Yarp.Application.Configuration;
using Yarp.ReverseProxy.Configuration;

namespace Yarp.Application.Features;

internal sealed class RequestMatchEvaluator
{
    private const string CatchAllPathPattern = "/{**catch-all}";

    private readonly TemplateMatcher _pathMatcher;
    private readonly StringSegment[] _hosts;
    private readonly string[] _methods;
    private readonly RequestQueryParameterMatcher[] _queryMatchers;

    public RequestMatchEvaluator(RequestMatch match, string ruleDisplayName)
    {
        var path = GetPathPattern(match, ruleDisplayName);
        Path = path;
        // Parse ASP.NET route-template syntax such as "/api/{**catch-all}" or
        // "/docs/{slug}" so static-host header rules use the same path semantics as endpoints.
        _pathMatcher = new TemplateMatcher(TemplateParser.Parse(path), new RouteValueDictionary());
        _hosts = CompileHosts(match.Hosts, ruleDisplayName);
        _methods = CompileMethods(match.Methods, ruleDisplayName);
        _queryMatchers = CompileQueryParameters(match.QueryParameters, ruleDisplayName);
    }

    public string Path { get; }

    /// <summary>
    /// Returns the route pattern for <paramref name="match"/>. Use this when path matching is
    /// delegated to ASP.NET endpoint routing and a full <see cref="TemplateMatcher"/> is
    /// unnecessary. Empty paths follow YARP route-match behavior and become a catch-all pattern.
    /// </summary>
    public static string GetPathPattern(RequestMatch match, string ruleDisplayName)
    {
        if (match is null)
        {
            throw new InvalidOperationException($"{ruleDisplayName} requires a Match object.");
        }

        if (string.IsNullOrWhiteSpace(match.Path))
        {
            return CatchAllPathPattern;
        }

        return match.Path;
    }

    public static void AddEndpointMetadata(EndpointBuilder endpointBuilder, RequestMatch match, string ruleDisplayName)
    {
        if (match.Hosts.Count > 0)
        {
            endpointBuilder.Metadata.Add(new HostAttribute(
                CompileHosts(match.Hosts, ruleDisplayName).Select(static host => host.Value!).ToArray()));
        }

        if (match.Methods.Count > 0)
        {
            endpointBuilder.Metadata.Add(new HttpMethodMetadata(CompileMethods(match.Methods, ruleDisplayName)));
        }

        if (match.QueryParameters.Count > 0)
        {
            endpointBuilder.Metadata.Add(new RequestQueryParameterMetadata(
                CompileQueryParameters(match.QueryParameters, ruleDisplayName)));
        }
    }

    public bool TryMatch(HttpContext context, RouteValueDictionary values)
    {
        ArgumentNullException.ThrowIfNull(context);
        return TryMatch(context, context.Request.Path, values);
    }

    public bool TryMatch(HttpContext context, PathString path, RouteValueDictionary values)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(values);

        // Route matching expects a rooted request path. Normal requests already have one, but
        // normalize the empty-path case so "/" behaves consistently in tests and callbacks.
        path = string.IsNullOrEmpty(path.Value) ? new PathString("/") : path;
        return _pathMatcher.TryMatch(path, values)
            && MatchHost(context.Request.Host)
            && MatchMethod(context.Request.Method)
            && MatchQuery(context.Request.Query);
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

    private static StringSegment[] CompileHosts(List<string> hosts, string ruleDisplayName)
    {
        var compiled = new StringSegment[hosts.Count];
        for (var i = 0; i < hosts.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(hosts[i]))
            {
                throw new InvalidOperationException($"{ruleDisplayName} contains an empty Match.Hosts entry.");
            }

            // Parse host patterns using endpoint routing's HostString matcher format. Examples:
            // "example.com", "*.example.com", and "example.com:8443".
            compiled[i] = new StringSegment(hosts[i]);
        }

        return compiled;
    }

    private static string[] CompileMethods(List<string> methods, string ruleDisplayName)
    {
        var compiled = new string[methods.Count];
        for (var i = 0; i < methods.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(methods[i]))
            {
                throw new InvalidOperationException($"{ruleDisplayName} contains an empty Match.Methods entry.");
            }

            compiled[i] = methods[i];
        }

        return compiled;
    }

    private static RequestQueryParameterMatcher[] CompileQueryParameters(
        List<RouteQueryParameter> queryParameters,
        string ruleDisplayName)
    {
        var compiled = new RequestQueryParameterMatcher[queryParameters.Count];
        for (var i = 0; i < queryParameters.Count; i++)
        {
            // Parse YARP-style query match entries. Example:
            // { Name: "preview", Values: [ "true" ], Mode: "Exact" } matches "?preview=true".
            compiled[i] = new RequestQueryParameterMatcher(queryParameters[i], ruleDisplayName);
        }

        return compiled;
    }

    private bool MatchHost(HostString host)
        => _hosts.Length == 0 || (host.HasValue && HostString.MatchesAny(new StringSegment(host.Value), _hosts));

    private bool MatchMethod(string method)
        => _methods.Length == 0 || _methods.Any(candidate => string.Equals(candidate, method, StringComparison.OrdinalIgnoreCase));

    private bool MatchQuery(IQueryCollection query)
    {
        foreach (var matcher in _queryMatchers)
        {
            if (!matcher.Match(query))
            {
                return false;
            }
        }

        return true;
    }
}
