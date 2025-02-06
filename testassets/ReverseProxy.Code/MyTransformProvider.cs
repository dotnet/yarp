// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net.Http;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Yarp.ReverseProxy.Sample;

internal sealed class MyTransformProvider : ITransformProvider
{
    public void ValidateRoute(TransformRouteValidationContext context)
    {
        // Check all routes for a custom property and validate the associated transform data.
        if (context.Route.Metadata?.TryGetValue("CustomMetadata", out var value) ?? false)
        {
            if (string.IsNullOrEmpty(value))
            {
                context.Errors.Add(new ArgumentException("A non-empty CustomMetadata value is required"));
            }
        }
    }

    public void ValidateCluster(TransformClusterValidationContext context)
    {
        // Check all clusters for a custom property and validate the associated transform data.
        if (context.Cluster.Metadata?.TryGetValue("CustomMetadata", out var value) ?? false)
        {
            if (string.IsNullOrEmpty(value))
            {
                context.Errors.Add(new ArgumentException("A non-empty CustomMetadata value is required"));
            }
        }
    }

    public void Apply(TransformBuilderContext transformBuildContext)
    {
        // Check all routes for a custom property and add the associated transform.
        if ((transformBuildContext.Route.Metadata?.TryGetValue("CustomMetadata", out var value) ?? false)
            || (transformBuildContext.Cluster?.Metadata?.TryGetValue("CustomMetadata", out value) ?? false))
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException("A non-empty CustomMetadata value is required");
            }

            transformBuildContext.AddRequestTransform(transformContext =>
            {
                transformContext.ProxyRequest.Options.Set(new HttpRequestOptionsKey<string>("CustomMetadata"), value);
                return default;
            });
        }
    }
}
