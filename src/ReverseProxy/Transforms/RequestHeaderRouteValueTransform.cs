// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

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
}

