// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Yarp.ReverseProxy.Transforms;

/// <summary>
/// Extensions for adding path transforms.
/// </summary>
public static class PathTransformExtensions
{
    /// <summary>
    /// Clones the route and adds the transform which sets the request path with the given value.
    /// </summary>
    public static RouteConfig WithTransformPathSet(this RouteConfig route, PathString path)
    {
        ArgumentNullException.ThrowIfNull(path.Value);

        return route.WithTransform(transform =>
        {
            transform[PathTransformFactory.PathSetKey] = path.Value;
        });
    }

    /// <summary>
    /// Adds the transform which sets the request path with the given value.
    /// </summary>
    public static TransformBuilderContext AddPathSet(this TransformBuilderContext context, PathString path)
    {
        context.RequestTransforms.Add(new PathStringTransform(PathStringTransform.PathTransformMode.Set, path));
        return context;
    }

    /// <summary>
    /// Clones the route and adds the transform which will prefix the request path with the given value.
    /// </summary>
    public static RouteConfig WithTransformPathPrefix(this RouteConfig route, PathString prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix.Value);

        return route.WithTransform(transform =>
        {
            transform[PathTransformFactory.PathPrefixKey] = prefix.Value;
        });
    }

    /// <summary>
    /// Adds the transform which will prefix the request path with the given value.
    /// </summary>
    public static TransformBuilderContext AddPathPrefix(this TransformBuilderContext context, PathString prefix)
    {
        context.RequestTransforms.Add(new PathStringTransform(PathStringTransform.PathTransformMode.Prefix, prefix));
        return context;
    }

    /// <summary>
    /// Clones the route and adds the transform which will remove the matching prefix from the request path.
    /// </summary>
    public static RouteConfig WithTransformPathRemovePrefix(this RouteConfig route, PathString prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix.Value);

        return route.WithTransform(transform =>
        {
            transform[PathTransformFactory.PathRemovePrefixKey] = prefix.Value;
        });
    }

    /// <summary>
    /// Adds the transform which will remove the matching prefix from the request path.
    /// </summary>
    public static TransformBuilderContext AddPathRemovePrefix(this TransformBuilderContext context, PathString prefix)
    {
        context.RequestTransforms.Add(new PathStringTransform(PathStringTransform.PathTransformMode.RemovePrefix, prefix));
        return context;
    }

    /// <summary>
    /// Clones the route and adds the transform which will set the request path with the given value.
    /// </summary>
    public static RouteConfig WithTransformPathRouteValues(this RouteConfig route, [StringSyntax("Route")] PathString pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern.Value);

        return route.WithTransform(transform =>
        {
            transform[PathTransformFactory.PathPatternKey] = pattern.Value;
        });
    }

    /// <summary>
    /// Clones the route and adds the transform which will set the request path with the given value.
    /// </summary>
    public static TransformBuilderContext AddPathRouteValues(this TransformBuilderContext context, [StringSyntax("Route")] PathString pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern.Value);

        var binder = context.Services.GetRequiredService<TemplateBinderFactory>();
        context.RequestTransforms.Add(new PathRouteValuesTransform(pattern.Value, binder));
        return context;
    }
}
