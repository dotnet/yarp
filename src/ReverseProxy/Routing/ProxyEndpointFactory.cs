// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Yarp.ReverseProxy.Model;
using CorsConstants = Yarp.ReverseProxy.Configuration.CorsConstants;
using AuthorizationConstants = Yarp.ReverseProxy.Configuration.AuthorizationConstants;
using RateLimitingConstants = Yarp.ReverseProxy.Configuration.RateLimitingConstants;
using TimeoutPolicyConstants = Yarp.ReverseProxy.Configuration.TimeoutPolicyConstants;

namespace Yarp.ReverseProxy.Routing;

internal sealed class ProxyEndpointFactory
{
    private static readonly IAuthorizeData _defaultAuthorization = new AuthorizeAttribute();
    private static readonly DisableRateLimitingAttribute _disableRateLimit = new();
    private static readonly DisableRequestTimeoutAttribute _disableRequestTimeout = new();
    private static readonly IEnableCorsAttribute _defaultCors = new EnableCorsAttribute();
    private static readonly IDisableCorsAttribute _disableCors = new DisableCorsAttribute();
    private static readonly IAllowAnonymous _allowAnonymous = new AllowAnonymousAttribute();

    private RequestDelegate? _pipeline;

    public Endpoint CreateEndpoint(RouteModel route, IReadOnlyList<Action<EndpointBuilder>> conventions)
    {
        var config = route.Config;
        var match = config.Match;

        // Catch-all pattern when no path was specified
        var pathPattern = string.IsNullOrEmpty(match.Path) ? "/{**catchall}" : match.Path;

        var endpointBuilder = new RouteEndpointBuilder(
            requestDelegate: _pipeline ?? throw new InvalidOperationException("The pipeline hasn't been provided yet."),
            routePattern: RoutePatternFactory.Parse(pathPattern),
            order: config.Order.GetValueOrDefault())
        {
            DisplayName = config.RouteId
        };

        endpointBuilder.Metadata.Add(route);

        if (match.Hosts is not null && match.Hosts.Count != 0)
        {
            endpointBuilder.Metadata.Add(new HostAttribute(match.Hosts.ToArray()));
        }

        if (match.Headers is not null && match.Headers.Count > 0)
        {
            var matchers = new List<HeaderMatcher>(match.Headers.Count);
            foreach (var header in match.Headers)
            {
                matchers.Add(new HeaderMatcher(header.Name, header.Values, header.Mode, header.IsCaseSensitive));
            }

            endpointBuilder.Metadata.Add(new HeaderMetadata(matchers));
        }

        if (match.QueryParameters is not null && match.QueryParameters.Count > 0)
        {
            var matchers = new List<QueryParameterMatcher>(match.QueryParameters.Count);
            foreach (var queryparam in match.QueryParameters)
            {
                matchers.Add(new QueryParameterMatcher(queryparam.Name, queryparam.Values, queryparam.Mode, queryparam.IsCaseSensitive));
            }

            endpointBuilder.Metadata.Add(new QueryParameterMetadata(matchers));
        }

        bool acceptCorsPreflight;
        if (string.Equals(CorsConstants.Default, config.CorsPolicy, StringComparison.OrdinalIgnoreCase))
        {
            endpointBuilder.Metadata.Add(_defaultCors);
            acceptCorsPreflight = true;
        }
        else if (string.Equals(CorsConstants.Disable, config.CorsPolicy, StringComparison.OrdinalIgnoreCase))
        {
            endpointBuilder.Metadata.Add(_disableCors);
            acceptCorsPreflight = true;
        }
        else if (!string.IsNullOrEmpty(config.CorsPolicy))
        {
            endpointBuilder.Metadata.Add(new EnableCorsAttribute(config.CorsPolicy));
            acceptCorsPreflight = true;
        }
        else
        {
            acceptCorsPreflight = false;
        }

        if (match.Methods is not null && match.Methods.Count > 0)
        {
            endpointBuilder.Metadata.Add(new HttpMethodMetadata(match.Methods, acceptCorsPreflight));
        }

        if (string.Equals(AuthorizationConstants.Default, config.AuthorizationPolicy, StringComparison.OrdinalIgnoreCase))
        {
            endpointBuilder.Metadata.Add(_defaultAuthorization);
        }
        else if (string.Equals(AuthorizationConstants.Anonymous, config.AuthorizationPolicy, StringComparison.OrdinalIgnoreCase))
        {
            endpointBuilder.Metadata.Add(_allowAnonymous);
        }
        else if (!string.IsNullOrEmpty(config.AuthorizationPolicy))
        {
            endpointBuilder.Metadata.Add(new AuthorizeAttribute(config.AuthorizationPolicy));
        }

        if (string.Equals(RateLimitingConstants.Default, config.RateLimiterPolicy, StringComparison.OrdinalIgnoreCase))
        {
            // No-op (middleware applies the default)
        }
        else if (string.Equals(RateLimitingConstants.Disable, config.RateLimiterPolicy, StringComparison.OrdinalIgnoreCase))
        {
            endpointBuilder.Metadata.Add(_disableRateLimit);
        }
        else if (!string.IsNullOrEmpty(config.RateLimiterPolicy))
        {
            endpointBuilder.Metadata.Add(new EnableRateLimitingAttribute(config.RateLimiterPolicy));
        }

        if (!string.IsNullOrEmpty(config.OutputCachePolicy))
        {
            endpointBuilder.Metadata.Add(new OutputCacheAttribute { PolicyName = config.OutputCachePolicy });
        }

        if (string.Equals(TimeoutPolicyConstants.Disable, config.TimeoutPolicy, StringComparison.OrdinalIgnoreCase))
        {
            endpointBuilder.Metadata.Add(_disableRequestTimeout);
        }
        // The config validator shouldn't allow both TimeoutPolicy and Timeout, so we don't have to consider priority.
        else if (!string.IsNullOrEmpty(config.TimeoutPolicy))
        {
            endpointBuilder.Metadata.Add(new RequestTimeoutAttribute(config.TimeoutPolicy));
        }
        else if (config.Timeout.HasValue)
        {
            endpointBuilder.Metadata.Add(new RequestTimeoutAttribute((int)config.Timeout.Value.TotalMilliseconds));
        }

        for (var i = 0; i < conventions.Count; i++)
        {
            conventions[i](endpointBuilder);
        }

        return endpointBuilder.Build();
    }

    public void SetProxyPipeline(RequestDelegate pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        _pipeline = pipeline;
    }
}
