// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Yarp.ReverseProxy.Transforms;

/// <summary>
/// Extensions for modifying the request method.
/// </summary>
public static class HttpMethodTransformExtensions
{
    /// <summary>
    /// Clones the route and adds the transform that will replace the HTTP method if it matches.
    /// </summary>
    public static RouteConfig WithTransformHttpMethodChange(this RouteConfig route, string fromHttpMethod, string toHttpMethod)
    {
        return route.WithTransform(transform =>
        {
            transform[HttpMethodTransformFactory.HttpMethodChangeKey] = fromHttpMethod;
            transform[HttpMethodTransformFactory.SetKey] = toHttpMethod;
        });
    }

    /// <summary>
    /// Adds the transform that will replace the HTTP method if it matches.
    /// </summary>
    public static TransformBuilderContext AddHttpMethodChange(this TransformBuilderContext context, string fromHttpMethod, string toHttpMethod)
    {
        context.RequestTransforms.Add(new HttpMethodChangeTransform(fromHttpMethod, toHttpMethod));
        return context;
    }
}
