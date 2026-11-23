// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Yarp.ReverseProxy.Transforms;

public class RequestHeaderRouteValueTransform : RequestHeaderTransform
{
    public RequestHeaderRouteValueTransform(string headerName, string routeValueKey, bool append)
        : base(headerName, append)
    {
        if (string.IsNullOrEmpty(headerName))
        {
            throw new ArgumentException($"'{nameof(headerName)}' cannot be null or empty.", nameof(headerName));
        }

        if (string.IsNullOrEmpty(routeValueKey))
        {
            throw new ArgumentException($"'{nameof(routeValueKey)}' cannot be null or empty.", nameof(routeValueKey));
        }

        RouteValueKey = routeValueKey;
    }

    internal string RouteValueKey { get; }

    protected override string? GetValue(RequestTransformContext context)
    {
        var routeValues = context.HttpContext.Request.RouteValues;
        if (!routeValues.TryGetValue(RouteValueKey, out var value))
        {
            return null;
        }

        return value?.ToString();
    }

    internal override void ApplyFast(HttpContext httpContext, HttpRequestMessage proxyRequest, ref bool headersCopied)
    {
        if (!httpContext.Request.RouteValues.TryGetValue(RouteValueKey, out var routeValue)
            || routeValue?.ToString() is not { } value)
        {
            return;
        }

        if (Append)
        {
            var existingValues = TakeHeader(httpContext, proxyRequest, headersCopied, HeaderName);
            AddHeader(proxyRequest, HeaderName, StringValues.Concat(existingValues, value));
        }
        else
        {
            RemoveHeader(proxyRequest, HeaderName);
            AddHeader(proxyRequest, HeaderName, value);
        }
    }
}
