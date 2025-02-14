// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Yarp.ReverseProxy.Transforms;

/// <summary>
/// Extensions for adding request header transforms.
/// </summary>
public static class RequestHeadersTransformExtensions
{
    /// <summary>
    /// Clones the route and adds the transform which will enable or suppress copying request headers to the proxy request.
    /// </summary>
    public static RouteConfig WithTransformCopyRequestHeaders(this RouteConfig route, bool copy = true)
    {
        return route.WithTransform(transform =>
        {
            transform[RequestHeadersTransformFactory.RequestHeadersCopyKey] = copy ? bool.TrueString : bool.FalseString;
        });
    }

    /// <summary>
    /// Clones the route and adds the transform which will copy the incoming request Host header to the proxy request.
    /// </summary>
    public static RouteConfig WithTransformUseOriginalHostHeader(this RouteConfig route, bool useOriginal = true)
    {
        return route.WithTransform(transform =>
        {
            transform[RequestHeadersTransformFactory.RequestHeaderOriginalHostKey] = useOriginal ? bool.TrueString : bool.FalseString;
        });
    }

    /// <summary>
    /// Clones the route and adds the transform which will append or set the request header.
    /// </summary>
    public static RouteConfig WithTransformRequestHeader(this RouteConfig route, string headerName, string value, bool append = true)
    {
        var type = append ? RequestHeadersTransformFactory.AppendKey : RequestHeadersTransformFactory.SetKey;
        return route.WithTransform(transform =>
        {
            transform[RequestHeadersTransformFactory.RequestHeaderKey] = headerName;
            transform[type] = value;
        });
    }

    /// <summary>
    /// Clones the route and adds the transform which will append or set the request header from a route value.
    /// </summary>
    public static RouteConfig WithTransformRequestHeaderRouteValue(this RouteConfig route, string headerName, string routeValueKey, bool append = true)
    {
        var type = append ? RequestHeadersTransformFactory.AppendKey : RequestHeadersTransformFactory.SetKey;
        return route.WithTransform(transform =>
        {
            transform[RequestHeadersTransformFactory.RequestHeaderRouteValueKey] = headerName;
            transform[type] = routeValueKey;
        });
    }

    /// <summary>
    /// Clones the route and adds the transform which will remove the request header.
    /// </summary>
    public static RouteConfig WithTransformRequestHeaderRemove(this RouteConfig route, string headerName)
    {
        return route.WithTransform(transform =>
        {
            transform[RequestHeadersTransformFactory.RequestHeaderRemoveKey] = headerName;
        });
    }

    /// <summary>
    /// Clones the route and adds the transform which will only copy the allowed request headers. Other transforms
    /// that modify or append to existing headers may be affected if not included in the allow list.
    /// </summary>
    public static RouteConfig WithTransformRequestHeadersAllowed(this RouteConfig route, params string[] allowedHeaders)
    {
        return route.WithTransform(transform =>
        {
            transform[RequestHeadersTransformFactory.RequestHeadersAllowedKey] = string.Join(';', allowedHeaders);
        });
    }

    /// <summary>
    /// Adds the transform which will append or set the request header.
    /// </summary>
    public static TransformBuilderContext AddRequestHeader(this TransformBuilderContext context, string headerName, string value, bool append = true)
    {
        context.RequestTransforms.Add(new RequestHeaderValueTransform(headerName, value, append));
        return context;
    }

    /// <summary>
    /// Adds the transform which will append or set the request header from a route value.
    /// </summary>
    public static TransformBuilderContext AddRequestHeaderRouteValue(this TransformBuilderContext context, string headerName, string routeValueKey, bool append = true)
    {
        context.RequestTransforms.Add(new RequestHeaderRouteValueTransform(headerName, routeValueKey, append));
        return context;
    }

    /// <summary>
    /// Adds the transform which will remove the request header.
    /// </summary>
    public static TransformBuilderContext AddRequestHeaderRemove(this TransformBuilderContext context, string headerName)
    {
        context.RequestTransforms.Add(new RequestHeaderRemoveTransform(headerName));
        return context;
    }

    /// <summary>
    /// Adds the transform which will only copy the allowed request headers. Other transforms
    /// that modify or append to existing headers may be affected if not included in the allow list.
    /// </summary>
    public static TransformBuilderContext AddRequestHeadersAllowed(this TransformBuilderContext context, params string[] allowedHeaders)
    {
        context.CopyRequestHeaders = false;
        context.RequestTransforms.Add(new RequestHeadersAllowedTransform(allowedHeaders));
        return context;
    }

    /// <summary>
    /// Adds the transform which will copy or remove the original host header.
    /// </summary>
    public static TransformBuilderContext AddOriginalHost(this TransformBuilderContext context, bool useOriginal = true)
    {
        if (useOriginal)
        {
            context.RequestTransforms.Add(RequestHeaderOriginalHostTransform.OriginalHost);
        }
        else
        {
            context.RequestTransforms.Add(RequestHeaderOriginalHostTransform.SuppressHost);
        }
        return context;
    }
}
