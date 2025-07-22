// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Forwarder;

namespace Yarp.ReverseProxy.Transforms;

/// <summary>
/// Transforms for responses.
/// </summary>
public abstract class ResponseTransform
{
    /// <summary>
    /// Transforms the given response. The status and headers will have (optionally) already been
    /// copied to the <see cref="HttpResponse"/> and any changes should be made there.
    /// </summary>
    public abstract ValueTask ApplyAsync(ResponseTransformContext context);

    /// <summary>
    /// Removes and returns the current header value by first checking the HttpResponse
    /// and falling back to the value from HttpResponseMessage or HttpContent only if
    /// <see cref="ResponseTransformContext.HeadersCopied"/> is not set.
    /// This ordering allows multiple transforms to mutate the same header.
    /// </summary>
    /// <param name="context">The transform context.</param>
    /// <param name="headerName">The name of the header to take.</param>
    /// <returns>The response header value, or StringValues.Empty if none.</returns>
    public static StringValues TakeHeader(ResponseTransformContext context, string headerName)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrEmpty(headerName))
        {
            throw new ArgumentException($"'{nameof(headerName)}' cannot be null or empty.", nameof(headerName));
        }

        if (context.HttpContext.Response.Headers.TryGetValue(headerName, out var existingValues))
        {
            context.HttpContext.Response.Headers.Remove(headerName);
        }
        else if (context.ProxyResponse is { } proxyResponse && !context.HeadersCopied)
        {
            if (!RequestUtilities.TryGetValues(proxyResponse.Headers, headerName, out existingValues))
            {
                RequestUtilities.TryGetValues(proxyResponse.Content.Headers, headerName, out existingValues);
            }
        }

        return existingValues;
    }

    /// <summary>
    /// Sets the given header on the HttpResponse.
    /// </summary>
    public static void SetHeader(ResponseTransformContext context, string headerName, StringValues values)
    {
        context.HttpContext.Response.Headers[headerName] = values;
    }

    internal static bool Success(ResponseTransformContext context)
    {
        // TODO: How complex should this get? Compare with http://nginx.org/en/docs/http/ngx_http_headers_module.html#add_header
        return context.HttpContext.Response.StatusCode < 400;
    }
}
