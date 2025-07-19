// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.Routing.Template;

namespace Yarp.ReverseProxy.Transforms;

/// <summary>
/// Generates a new request path by plugging matched route parameters into the given pattern.
/// </summary>
public class PathRouteValuesTransform : RequestTransform
{
    private readonly TemplateBinderFactory _binderFactory;

    /// <summary>
    /// Creates a new transform.
    /// </summary>
    /// <param name="pattern">The pattern used to create the new request path.</param>
    /// <param name="binderFactory">The factory used to bind route parameters to the given path pattern.</param>
    public PathRouteValuesTransform(
        [StringSyntax("Route")] string pattern, TemplateBinderFactory binderFactory)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(binderFactory);
        _binderFactory = binderFactory;
        Pattern = RoutePatternFactory.Parse(pattern);
    }

    internal RoutePattern Pattern { get; }

    /// <inheritdoc/>
    public override ValueTask ApplyAsync(RequestTransformContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // TemplateBinder.BindValues will modify the RouteValueDictionary
        // We make a copy so that the original request is not modified by the transform
        var routeValues = context.HttpContext.Request.RouteValues;
        var routeValuesCopy = new RouteValueDictionary();

        // Only copy route values used in the pattern, otherwise they'll be added as query parameters.
        foreach (var pattern in Pattern.Parameters)
        {
            if (routeValues.TryGetValue(pattern.Name, out var value))
            {
                routeValuesCopy[pattern.Name] = value;
            }
        }

        var binder = _binderFactory.Create(Pattern);
        context.Path = binder.BindValues(acceptedValues: routeValuesCopy);

        return default;
    }
}
