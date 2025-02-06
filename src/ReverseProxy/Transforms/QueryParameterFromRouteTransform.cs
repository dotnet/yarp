// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Yarp.ReverseProxy.Transforms;

public class QueryParameterRouteTransform : QueryParameterTransform
{
    public QueryParameterRouteTransform(QueryStringTransformMode mode, string key, string routeValueKey)
        : base(mode, key)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException($"'{nameof(key)}' cannot be null or empty.", nameof(key));
        }

        if (string.IsNullOrEmpty(routeValueKey))
        {
            throw new ArgumentException($"'{nameof(routeValueKey)}' cannot be null or empty.", nameof(routeValueKey));
        }

        RouteValueKey = routeValueKey;
    }

    internal string RouteValueKey { get; }

    /// <inheritdoc/>
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
