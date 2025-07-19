// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Forwarder;

namespace Yarp.ReverseProxy.Transforms;

/// <summary>
/// Transforms for response trailers.
/// </summary>
public abstract class ResponseTrailersTransform
{
    /// <summary>
    /// Transforms the given response trailers. The trailers will have (optionally) already been
    /// copied to the <see cref="HttpResponse"/> and any changes should be made there.
    /// </summary>
    public abstract ValueTask ApplyAsync(ResponseTrailersTransformContext context);

    /// <summary>
    /// Removes and returns the current trailer value by first checking the HttpResponse
    /// and falling back to the value from HttpResponseMessage only if
    /// <see cref="ResponseTrailersTransformContext.HeadersCopied"/> is not set.
    /// This ordering allows multiple transforms to mutate the same header.
    /// </summary>
    /// <param name="context">The transform context.</param>
    /// <param name="headerName">The name of the header to take.</param>
    /// <returns>The response header value, or StringValues.Empty if none.</returns>
    public static StringValues TakeHeader(ResponseTrailersTransformContext context, string headerName)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrEmpty(headerName);

        Debug.Assert(context.ProxyResponse is not null);

        var responseTrailersFeature = context.HttpContext.Features.Get<IHttpResponseTrailersFeature>();
        var responseTrailers = responseTrailersFeature?.Trailers;
        // Support should have already been checked by the caller.
        Debug.Assert(responseTrailers is not null);
        Debug.Assert(!responseTrailers.IsReadOnly);

        if (responseTrailers.TryGetValue(headerName, out var existingValues))
        {
            responseTrailers.Remove(headerName);
        }
        else if (!context.HeadersCopied)
        {
            RequestUtilities.TryGetValues(context.ProxyResponse.TrailingHeaders, headerName, out existingValues);
        }

        return existingValues;
    }

    /// <summary>
    /// Sets the given trailer on the HttpResponse.
    /// </summary>
    public static void SetHeader(ResponseTrailersTransformContext context, string headerName, StringValues values)
    {
        var responseTrailersFeature = context.HttpContext.Features.Get<IHttpResponseTrailersFeature>();
        var responseTrailers = responseTrailersFeature?.Trailers;
        // Support should have already been checked by the caller.
        Debug.Assert(responseTrailers is not null);
        Debug.Assert(!responseTrailers.IsReadOnly);

        responseTrailers[headerName] = values;
    }

    internal static bool Success(ResponseTrailersTransformContext context)
    {
        // TODO: How complex should this get? Compare with http://nginx.org/en/docs/http/ngx_http_headers_module.html#add_header
        return context.HttpContext.Response.StatusCode < 400;
    }
}
