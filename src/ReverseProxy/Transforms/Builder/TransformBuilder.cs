// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;

namespace Yarp.ReverseProxy.Transforms.Builder;

/// <summary>
/// Validates and builds request and response transforms for a given route.
/// </summary>
internal sealed class TransformBuilder : ITransformBuilder
{
    private readonly IServiceProvider _services;
    private readonly List<ITransformFactory> _factories;
    private readonly List<ITransformProvider> _providers;

    /// <summary>
    /// Creates a new <see cref="TransformBuilder"/>
    /// </summary>
    public TransformBuilder(IServiceProvider services, IEnumerable<ITransformFactory> factories, IEnumerable<ITransformProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(factories);
        ArgumentNullException.ThrowIfNull(providers);

        _services = services;
        _factories = factories.ToList();
        _providers = providers.ToList();
    }

    /// <inheritdoc/>
    public IReadOnlyList<Exception> ValidateRoute(RouteConfig route)
    {
        var context = new TransformRouteValidationContext()
        {
            Services = _services,
            Route = route,
        };

        var rawTransforms = route?.Transforms;
        if (rawTransforms?.Count > 0)
        {
            foreach (var rawTransform in rawTransforms)
            {
                var handled = false;
                foreach (var factory in _factories)
                {
                    if (factory.Validate(context, rawTransform))
                    {
                        handled = true;
                        break;
                    }
                }

                if (!handled)
                {
                    context.Errors.Add(new ArgumentException($"Unknown transform: {string.Join(';', rawTransform.Keys)}"));
                }
            }
        }

        // Let the app add any more validation it wants.
        foreach (var transformProvider in _providers)
        {
            transformProvider.ValidateRoute(context);
        }

        // We promise not to modify the list after we return it.
        return (IReadOnlyList<Exception>)context.Errors;
    }

    /// <inheritdoc/>
    public IReadOnlyList<Exception> ValidateCluster(ClusterConfig cluster)
    {
        var context = new TransformClusterValidationContext()
        {
            Services = _services,
            Cluster = cluster,
        };

        // Let the app add any more validation it wants.
        foreach (var transformProvider in _providers)
        {
            transformProvider.ValidateCluster(context);
        }

        // We promise not to modify the list after we return it.
        return (IReadOnlyList<Exception>)context.Errors;
    }

    /// <inheritdoc/>
    public HttpTransformer Build(RouteConfig route, ClusterConfig? cluster)
    {
        return BuildInternal(route, cluster);
    }

    // This is separate from Build for testing purposes.
    internal StructuredTransformer BuildInternal(RouteConfig route, ClusterConfig? cluster)
    {
        var rawTransforms = route.Transforms;

        var context = new TransformBuilderContext
        {
            Services = _services,
            Route = route,
            Cluster = cluster,
        };

        if (rawTransforms?.Count > 0)
        {
            foreach (var rawTransform in rawTransforms)
            {
                var handled = false;
                foreach (var factory in _factories)
                {
                    if (factory.Build(context, rawTransform))
                    {
                        handled = true;
                        break;
                    }
                }

                if (!handled)
                {
                    throw new ArgumentException($"Unknown transform: {string.Join(';', rawTransform.Keys)}");
                }
            }
        }

        // Let the app add any more transforms it wants.
        foreach (var transformProvider in _providers)
        {
            transformProvider.Apply(context);
        }

        return CreateTransformer(context);
    }

    public HttpTransformer Create(Action<TransformBuilderContext> action)
    {
        return CreateInternal(action);
    }

    internal StructuredTransformer CreateInternal(Action<TransformBuilderContext> action)
    {
        var context = new TransformBuilderContext
        {
            Services = _services,
        };

        action(context);

        return CreateTransformer(context);
    }

    internal static StructuredTransformer CreateTransformer(TransformBuilderContext context)
    {
        // RequestHeaderOriginalHostKey defaults to false, and CopyRequestHeaders defaults to true.
        // If RequestHeaderOriginalHostKey was not specified then we need to make sure the transform gets
        // added anyway to remove the original host and to observe hosts specified in DestinationConfig.
        if (!context.RequestTransforms.Any(item => item is RequestHeaderOriginalHostTransform))
        {
            context.AddOriginalHost(false);
        }

        // Add default forwarders only if they haven't already been added or disabled.
        if (context.UseDefaultForwarders.GetValueOrDefault(true))
        {
            context.AddXForwarded();
        }

        return new StructuredTransformer(
            context.CopyRequestHeaders,
            context.CopyResponseHeaders,
            context.CopyResponseTrailers,
            context.RequestTransforms,
            context.ResponseTransforms,
            context.ResponseTrailersTransforms);
    }
}
