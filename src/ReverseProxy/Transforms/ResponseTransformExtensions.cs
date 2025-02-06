// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Yarp.ReverseProxy.Transforms;

/// <summary>
/// Extensions for adding response header and trailer transforms.
/// </summary>
public static class ResponseTransformExtensions
{
    /// <summary>
    /// Clones the route and adds the transform which will enable or suppress copying response headers to the client response.
    /// </summary>
    public static RouteConfig WithTransformCopyResponseHeaders(this RouteConfig route, bool copy = true)
    {
        return route.WithTransform(transform =>
        {
            transform[ResponseTransformFactory.ResponseHeadersCopyKey] = copy ? bool.TrueString : bool.FalseString;
        });
    }

    /// <summary>
    /// Clones the route and adds the transform which will enable or suppress copying response trailers to the client response.
    /// </summary>
    public static RouteConfig WithTransformCopyResponseTrailers(this RouteConfig route, bool copy = true)
    {
        return route.WithTransform(transform =>
        {
            transform[ResponseTransformFactory.ResponseTrailersCopyKey] = copy ? bool.TrueString : bool.FalseString;
        });
    }

    /// <summary>
    /// Clones the route and adds the transform which will append or set the response header.
    /// </summary>
    public static RouteConfig WithTransformResponseHeader(this RouteConfig route, string headerName, string value, bool append = true, ResponseCondition condition = ResponseCondition.Success)
    {
        var type = append ? ResponseTransformFactory.AppendKey : ResponseTransformFactory.SetKey;
        return route.WithTransform(transform =>
        {
            transform[ResponseTransformFactory.ResponseHeaderKey] = headerName;
            transform[type] = value;
            transform[ResponseTransformFactory.WhenKey] = condition.ToString();
        });
    }

    /// <summary>
    /// Clones the route and adds the transform which will remove the response header.
    /// </summary>
    public static RouteConfig WithTransformResponseHeaderRemove(this RouteConfig route, string headerName, ResponseCondition condition = ResponseCondition.Success)
    {
        return route.WithTransform(transform =>
        {
            transform[ResponseTransformFactory.ResponseHeaderRemoveKey] = headerName;
            transform[ResponseTransformFactory.WhenKey] = condition.ToString();
        });
    }

    /// <summary>
    /// Clones the route and adds the transform which will only copy the allowed response headers. Other transforms
    /// that modify or append to existing headers may be affected if not included in the allow list.
    /// </summary>
    public static RouteConfig WithTransformResponseHeadersAllowed(this RouteConfig route, params string[] allowedHeaders)
    {
        return route.WithTransform(transform =>
        {
            transform[ResponseTransformFactory.ResponseHeadersAllowedKey] = string.Join(';', allowedHeaders);
        });
    }

    /// <summary>
    /// Adds the transform which will append or set the response header.
    /// </summary>
    public static TransformBuilderContext AddResponseHeader(this TransformBuilderContext context, string headerName, string value, bool append = true, ResponseCondition condition = ResponseCondition.Success)
    {
        context.ResponseTransforms.Add(new ResponseHeaderValueTransform(headerName, value, append, condition));
        return context;
    }

    /// <summary>
    /// Adds the transform which will remove the response header.
    /// </summary>
    public static TransformBuilderContext AddResponseHeaderRemove(this TransformBuilderContext context, string headerName, ResponseCondition condition = ResponseCondition.Success)
    {
        context.ResponseTransforms.Add(new ResponseHeaderRemoveTransform(headerName, condition));
        return context;
    }

    /// <summary>
    /// Adds the transform which will only copy the allowed response headers. Other transforms
    /// that modify or append to existing headers may be affected if not included in the allow list.
    /// </summary>
    public static TransformBuilderContext AddResponseHeadersAllowed(this TransformBuilderContext context, params string[] allowedHeaders)
    {
        context.CopyResponseHeaders = false;
        context.ResponseTransforms.Add(new ResponseHeadersAllowedTransform(allowedHeaders));
        return context;
    }

    /// <summary>
    /// Clones the route and adds the transform which will append or set the response trailer.
    /// </summary>
    public static RouteConfig WithTransformResponseTrailer(this RouteConfig route, string headerName, string value, bool append = true, ResponseCondition condition = ResponseCondition.Success)
    {
        var type = append ? ResponseTransformFactory.AppendKey : ResponseTransformFactory.SetKey;
        return route.WithTransform(transform =>
        {
            transform[ResponseTransformFactory.ResponseTrailerKey] = headerName;
            transform[type] = value;
            transform[ResponseTransformFactory.WhenKey] = condition.ToString();
        });
    }

    /// <summary>
    /// Adds the transform which will append or set the response trailer.
    /// </summary>
    public static TransformBuilderContext AddResponseTrailer(this TransformBuilderContext context, string headerName, string value, bool append = true, ResponseCondition condition = ResponseCondition.Success)
    {
        context.ResponseTrailersTransforms.Add(new ResponseTrailerValueTransform(headerName, value, append, condition));
        return context;
    }

    /// <summary>
    /// Adds the transform which will remove the response trailer.
    /// </summary>
    public static TransformBuilderContext AddResponseTrailerRemove(this TransformBuilderContext context, string headerName, ResponseCondition condition = ResponseCondition.Success)
    {
        context.ResponseTrailersTransforms.Add(new ResponseTrailerRemoveTransform(headerName, condition));
        return context;
    }

    /// <summary>
    /// Clones the route and adds the transform which will remove the response trailer.
    /// </summary>
    public static RouteConfig WithTransformResponseTrailerRemove(this RouteConfig route, string headerName, ResponseCondition condition = ResponseCondition.Success)
    {
        return route.WithTransform(transform =>
        {
            transform[ResponseTransformFactory.ResponseTrailerRemoveKey] = headerName;
            transform[ResponseTransformFactory.WhenKey] = condition.ToString();
        });
    }

    /// <summary>
    /// Clones the route and adds the transform which will only copy the allowed response trailers. Other transforms
    /// that modify or append to existing trailers may be affected if not included in the allow list.
    /// </summary>
    public static RouteConfig WithTransformResponseTrailersAllowed(this RouteConfig route, params string[] allowedHeaders)
    {
        return route.WithTransform(transform =>
        {
            transform[ResponseTransformFactory.ResponseTrailersAllowedKey] = string.Join(';', allowedHeaders);
        });
    }

    /// <summary>
    /// Adds the transform which will only copy the allowed response trailers. Other transforms
    /// that modify or append to existing trailers may be affected if not included in the allow list.
    /// </summary>
    public static TransformBuilderContext AddResponseTrailersAllowed(this TransformBuilderContext context, params string[] allowedHeaders)
    {
        context.CopyResponseTrailers = false;
        context.ResponseTrailersTransforms.Add(new ResponseTrailersAllowedTransform(allowedHeaders));
        return context;
    }
}
