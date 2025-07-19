// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Forwarder;

namespace Yarp.ReverseProxy.Transforms;

/// <summary>
/// The base class for request transforms.
/// </summary>
public abstract class RequestTransform
{
    /// <summary>
    /// Transforms any of the available fields before building the outgoing request.
    /// </summary>
    public abstract ValueTask ApplyAsync(RequestTransformContext context);

    /// <summary>
    /// Removes and returns the current header value by first checking the HttpRequestMessage,
    /// then the HttpContent, and falling back to the HttpContext only if
    /// <see cref="RequestTransformContext.HeadersCopied"/> is not set.
    /// This ordering allows multiple transforms to mutate the same header.
    /// </summary>
    /// <param name="context">The transform context.</param>
    /// <param name="headerName">The name of the header to take.</param>
    /// <returns>The requested header value, or StringValues.Empty if none.</returns>
    public static StringValues TakeHeader(RequestTransformContext context, string headerName)
    {
        if (string.IsNullOrEmpty(headerName))
        {
            throw new ArgumentException($"'{nameof(headerName)}' cannot be null or empty.", nameof(headerName));
        }

        var proxyRequest = context.ProxyRequest;

        if (RequestUtilities.TryGetValues(proxyRequest.Headers, headerName, out var existingValues))
        {
            proxyRequest.Headers.Remove(headerName);
        }
        else if (proxyRequest.Content is { } content && RequestUtilities.TryGetValues(content.Headers, headerName, out existingValues))
        {
            content.Headers.Remove(headerName);
        }
        else if (!context.HeadersCopied)
        {
            existingValues = context.HttpContext.Request.Headers[headerName];
        }

        return existingValues;
    }

    /// <summary>
    /// Adds the given header to the HttpRequestMessage or HttpContent where applicable.
    /// </summary>
    public static void AddHeader(RequestTransformContext context, string headerName, StringValues values)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrEmpty(headerName);

        RequestUtilities.AddHeader(context.ProxyRequest, headerName, values);
    }

    /// <summary>
    /// Removes the given header from the HttpRequestMessage or HttpContent where applicable.
    /// </summary>
    public static void RemoveHeader(RequestTransformContext context, string headerName)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrEmpty(headerName);

        RequestUtilities.RemoveHeader(context.ProxyRequest, headerName);
    }
}
