// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Matching;
using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;

namespace Yarp.Application.Features;

internal sealed class RequestQueryParameterMatcher
{
    public RequestQueryParameterMatcher(RouteQueryParameter queryParameter, string ruleDisplayName)
    {
        if (string.IsNullOrEmpty(queryParameter.Name))
        {
            throw new InvalidOperationException($"{ruleDisplayName} query parameter match requires a non-empty Name.");
        }

        if (queryParameter.Mode != QueryParameterMatchMode.Exists
            && (queryParameter.Values is null || queryParameter.Values.Count == 0))
        {
            throw new InvalidOperationException($"{ruleDisplayName} query parameter match '{queryParameter.Name}' requires at least one value.");
        }

        if (queryParameter.Mode == QueryParameterMatchMode.Exists && queryParameter.Values?.Count > 0)
        {
            throw new InvalidOperationException($"{ruleDisplayName} query parameter match '{queryParameter.Name}' must not specify values when Mode is Exists.");
        }

        if (queryParameter.Values is not null && queryParameter.Values.Any(string.IsNullOrEmpty))
        {
            throw new InvalidOperationException($"{ruleDisplayName} query parameter match '{queryParameter.Name}' contains an empty value.");
        }

        Name = queryParameter.Name;
        Values = queryParameter.Values?.ToArray() ?? [];
        Mode = queryParameter.Mode;
        Comparison = queryParameter.IsCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
    }

    public string Name { get; }

    public string[] Values { get; }

    public QueryParameterMatchMode Mode { get; }

    private StringComparison Comparison { get; }

    public bool Match(IQueryCollection query)
    {
        query.TryGetValue(Name, out var requestQueryParameterValues);
        var valueIsEmpty = StringValues.IsNullOrEmpty(requestQueryParameterValues);

        return Mode switch
        {
            QueryParameterMatchMode.Exists => !valueIsEmpty,
            QueryParameterMatchMode.Exact => !valueIsEmpty && TryMatch(requestQueryParameterValues),
            QueryParameterMatchMode.Prefix => !valueIsEmpty && TryMatch(requestQueryParameterValues),
            QueryParameterMatchMode.Contains => !valueIsEmpty && TryMatch(requestQueryParameterValues),
            QueryParameterMatchMode.NotContains => valueIsEmpty || TryMatch(requestQueryParameterValues),
            _ => false
        };
    }

    private bool TryMatch(StringValues requestQueryParameterValues)
    {
        for (var i = 0; i < requestQueryParameterValues.Count; i++)
        {
            var requestValue = requestQueryParameterValues[i];
            if (requestValue is null)
            {
                continue;
            }

            foreach (var expectedValue in Values)
            {
                if (TryMatch(requestValue, expectedValue))
                {
                    return Mode != QueryParameterMatchMode.NotContains;
                }
            }
        }

        return Mode == QueryParameterMatchMode.NotContains;
    }

    private bool TryMatch(string queryValue, string expectedValue)
        => Mode switch
        {
            QueryParameterMatchMode.Exact => queryValue.Equals(expectedValue, Comparison),
            QueryParameterMatchMode.Prefix => queryValue.StartsWith(expectedValue, Comparison),
            _ => queryValue.Contains(expectedValue, Comparison)
        };
}

internal sealed class RequestQueryParameterMetadata
{
    public RequestQueryParameterMetadata(RequestQueryParameterMatcher[] matchers)
    {
        Matchers = matchers;
    }

    public RequestQueryParameterMatcher[] Matchers { get; }
}

internal sealed class RequestQueryParameterMatcherPolicy : MatcherPolicy, IEndpointComparerPolicy, IEndpointSelectorPolicy
{
    public override int Order => -25;

    public IComparer<Endpoint> Comparer { get; } = Comparer<Endpoint>.Create(static (x, y) =>
        (y.Metadata.GetMetadata<RequestQueryParameterMetadata>()?.Matchers.Length ?? 0)
            .CompareTo(x.Metadata.GetMetadata<RequestQueryParameterMetadata>()?.Matchers.Length ?? 0));

    bool IEndpointSelectorPolicy.AppliesToEndpoints(IReadOnlyList<Endpoint> endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        if (ContainsDynamicEndpoints(endpoints))
        {
            return true;
        }

        return endpoints.Any(static endpoint =>
            endpoint.Metadata.GetMetadata<RequestQueryParameterMetadata>()?.Matchers.Length > 0);
    }

    public Task ApplyAsync(HttpContext httpContext, CandidateSet candidates)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(candidates);

        for (var i = 0; i < candidates.Count; i++)
        {
            if (!candidates.IsValidCandidate(i))
            {
                continue;
            }

            var matchers = candidates[i].Endpoint.Metadata.GetMetadata<RequestQueryParameterMetadata>()?.Matchers;
            if (matchers is null)
            {
                continue;
            }

            foreach (var matcher in matchers)
            {
                if (!matcher.Match(httpContext.Request.Query))
                {
                    candidates.SetValidity(i, false);
                    break;
                }
            }
        }

        return Task.CompletedTask;
    }
}
