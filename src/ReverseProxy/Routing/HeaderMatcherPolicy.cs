// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Matching;
using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;

namespace Yarp.ReverseProxy.Routing;

internal sealed class HeaderMatcherPolicy : MatcherPolicy, IEndpointComparerPolicy, IEndpointSelectorPolicy
{
    /// <inheritdoc/>
    // Run after HttpMethodMatcherPolicy (-1000) and HostMatcherPolicy (-100), but before default (0)
    public override int Order => -50;

    /// <inheritdoc/>
    public IComparer<Endpoint> Comparer => new HeaderMetadataEndpointComparer();

    /// <inheritdoc/>
    bool IEndpointSelectorPolicy.AppliesToEndpoints(IReadOnlyList<Endpoint> endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        // When the node contains dynamic endpoints we can't make any assumptions.
        if (ContainsDynamicEndpoints(endpoints))
        {
            return true;
        }

        return AppliesToEndpointsCore(endpoints);
    }

    private static bool AppliesToEndpointsCore(IReadOnlyList<Endpoint> endpoints)
    {
        return endpoints.Any(e =>
        {
            var metadata = e.Metadata.GetMetadata<IHeaderMetadata>();
            return metadata?.Matchers?.Length > 0;
        });
    }

    /// <inheritdoc/>
    public Task ApplyAsync(HttpContext httpContext, CandidateSet candidates)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(candidates);

        var headers = httpContext.Request.Headers;

        for (var i = 0; i < candidates.Count; i++)
        {
            if (!candidates.IsValidCandidate(i))
            {
                continue;
            }

            var matchers = candidates[i].Endpoint.Metadata.GetMetadata<IHeaderMetadata>()?.Matchers;

            if (matchers is null)
            {
                continue;
            }

            foreach (var matcher in matchers)
            {
                headers.TryGetValue(matcher.Name, out var requestHeaderValues);
                var valueIsEmpty = StringValues.IsNullOrEmpty(requestHeaderValues);

                var matched = matcher.Mode switch
                {
                    HeaderMatchMode.Exists => !valueIsEmpty,
                    HeaderMatchMode.NotExists => valueIsEmpty,
                    HeaderMatchMode.ExactHeader => !valueIsEmpty && TryMatchExactOrPrefix(matcher, requestHeaderValues),
                    HeaderMatchMode.HeaderPrefix => !valueIsEmpty && TryMatchExactOrPrefix(matcher, requestHeaderValues),
                    HeaderMatchMode.Contains => !valueIsEmpty && TryMatchContainsOrNotContains(matcher, requestHeaderValues),
                    HeaderMatchMode.NotContains => valueIsEmpty || TryMatchContainsOrNotContains(matcher, requestHeaderValues),
                    _ => false
                };

                if (!matched)
                {
                    candidates.SetValidity(i, false);
                    break;
                }
            }
        }

        return Task.CompletedTask;
    }

    private static bool TryMatchExactOrPrefix(HeaderMatcher matcher, StringValues requestHeaderValues)
    {
        var requestHeaderCount = requestHeaderValues.Count;

        for (var i = 0; i < requestHeaderCount; i++)
        {
            var requestValue = requestHeaderValues[i].AsSpan();

            while (!requestValue.IsEmpty)
            {
                requestValue = requestValue.TrimStart(' ');

                // Find the end of the next value.
                // Separators inside a quote pair must be ignored as they are a part of the value.
                var separatorOrQuoteIndex = requestValue.IndexOfAny('"', matcher.Separator);
                while (separatorOrQuoteIndex != -1 && requestValue[separatorOrQuoteIndex] == '"')
                {
                    var closingQuoteIndex = requestValue.Slice(separatorOrQuoteIndex + 1).IndexOf('"');
                    if (closingQuoteIndex == -1)
                    {
                        separatorOrQuoteIndex = -1;
                    }
                    else
                    {
                        var offset = separatorOrQuoteIndex + closingQuoteIndex + 2;
                        separatorOrQuoteIndex = requestValue.Slice(offset).IndexOfAny('"', matcher.Separator);
                        if (separatorOrQuoteIndex != -1)
                        {
                            separatorOrQuoteIndex += offset;
                        }
                    }
                }

                ReadOnlySpan<char> value;
                if (separatorOrQuoteIndex == -1)
                {
                    value = requestValue;
                    requestValue = default;
                }
                else
                {
                    value = requestValue.Slice(0, separatorOrQuoteIndex);
                    requestValue = requestValue.Slice(separatorOrQuoteIndex + 1);
                }

                if (value.Length > 1 && value[0] == '"' && value[^1] == '"')
                {
                    value = value.Slice(1, value.Length - 2);
                }

                foreach (var expectedValue in matcher.Values)
                {
                    if (matcher.Mode == HeaderMatchMode.ExactHeader
                        ? value.Equals(expectedValue, matcher.Comparison)
                        : value.StartsWith(expectedValue, matcher.Comparison))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool TryMatchContainsOrNotContains(HeaderMatcher matcher, StringValues requestHeaderValues)
    {
        Debug.Assert(matcher.Mode is HeaderMatchMode.Contains or HeaderMatchMode.NotContains, $"{matcher.Mode}");

        var requestHeaderCount = requestHeaderValues.Count;

        for (var i = 0; i < requestHeaderCount; i++)
        {
            var requestValue = requestHeaderValues[i];
            if (requestValue is null)
            {
                continue;
            }

            foreach (var expectedValue in matcher.Values)
            {
                if (requestValue.Contains(expectedValue, matcher.Comparison))
                {
                    return matcher.Mode != HeaderMatchMode.NotContains;
                }
            }
        }

        return matcher.Mode == HeaderMatchMode.NotContains;
    }

    private sealed class HeaderMetadataEndpointComparer : EndpointMetadataComparer<IHeaderMetadata>
    {
        protected override int CompareMetadata(IHeaderMetadata? x, IHeaderMetadata? y)
        {
            return (y?.Matchers?.Length ?? 0).CompareTo(x?.Matchers?.Length ?? 0);
        }
    }
}
