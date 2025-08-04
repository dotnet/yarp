// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Yarp.ReverseProxy.Forwarder;

/// <summary>
/// Extensions methods for <see cref="IHttpForwarder"/>.
/// </summary>
public static class IHttpForwarderExtensions
{
    /// <summary>
    /// Forwards the incoming request to the destination server, and the response back to the client.
    /// </summary>
    /// <param name="forwarder">The forwarder instance.</param>
    /// <param name="context">The HttpContext to forward.</param>
    /// <param name="destinationPrefix">The url prefix for where to forward the request to.</param>
    /// <param name="requestConfig">Config for the outgoing request.</param>
    /// <param name="requestTransform">Transform function to apply to the forwarded request.</param>
    /// <returns>The status of a forwarding operation.</returns>
    public static ValueTask<ForwarderError> SendAsync(this IHttpForwarder forwarder, HttpContext context, string destinationPrefix,
        ForwarderRequestConfig? requestConfig = null, Func<HttpContext, HttpRequestMessage, ValueTask>? requestTransform = null)
    {
        ArgumentNullException.ThrowIfNull(forwarder);
        ArgumentNullException.ThrowIfNull(context);

        requestConfig ??= ForwarderRequestConfig.Empty;
        var transformer = requestTransform is null ? HttpTransformer.Default : new RequestTransformer(requestTransform);
        var httpClientProvider = context.RequestServices.GetRequiredService<DirectForwardingHttpClientProvider>();

        return forwarder.SendAsync(context, destinationPrefix, httpClientProvider.HttpClient, requestConfig, transformer);
    }

    /// <summary>
    /// Forwards the incoming request to the destination server, and the response back to the client.
    /// </summary>
    /// <param name="forwarder">The forwarder instance.</param>
    /// <param name="context">The HttpContext to forward.</param>
    /// <param name="destinationPrefix">The url prefix for where to forward the request to.</param>
    /// <param name="httpClient">The HTTP client used to forward the request.</param>
    /// <returns>The status of a forwarding operation.</returns>
    public static ValueTask<ForwarderError> SendAsync(this IHttpForwarder forwarder, HttpContext context, string destinationPrefix,
        HttpMessageInvoker httpClient)
    {
        ArgumentNullException.ThrowIfNull(forwarder);

        return forwarder.SendAsync(context, destinationPrefix, httpClient, ForwarderRequestConfig.Empty, HttpTransformer.Default);
    }

    /// <summary>
    /// Forwards the incoming request to the destination server, and the response back to the client.
    /// </summary>
    /// <param name="forwarder">The forwarder instance.</param>
    /// <param name="context">The HttpContext to forward.</param>
    /// <param name="destinationPrefix">The url prefix for where to forward the request to.</param>
    /// <param name="httpClient">The HTTP client used to forward the request.</param>
    /// <param name="requestConfig">Config for the outgoing request.</param>
    /// <returns>The status of a forwarding operation.</returns>
    public static ValueTask<ForwarderError> SendAsync(this IHttpForwarder forwarder, HttpContext context, string destinationPrefix,
        HttpMessageInvoker httpClient, ForwarderRequestConfig requestConfig)
    {
        ArgumentNullException.ThrowIfNull(forwarder);

        return forwarder.SendAsync(context, destinationPrefix, httpClient, requestConfig, HttpTransformer.Default);
    }

    /// <summary>
    /// Forwards the incoming request to the destination server, and the response back to the client.
    /// </summary>
    /// <param name="forwarder">The forwarder instance.</param>
    /// <param name="context">The HttpContext to forward.</param>
    /// <param name="destinationPrefix">The url prefix for where to forward the request to.</param>
    /// <param name="httpClient">The HTTP client used to forward the request.</param>
    /// <param name="requestTransform">Transform function to apply to the forwarded request.</param>
    /// <returns>The status of a forwarding operation.</returns>
    public static ValueTask<ForwarderError> SendAsync(this IHttpForwarder forwarder, HttpContext context, string destinationPrefix,
        HttpMessageInvoker httpClient, Func<HttpContext, HttpRequestMessage, ValueTask> requestTransform)
    {
        return forwarder.SendAsync(context, destinationPrefix, httpClient, ForwarderRequestConfig.Empty, requestTransform);
    }

    /// <summary>
    /// Forwards the incoming request to the destination server, and the response back to the client.
    /// </summary>
    /// <param name="forwarder">The forwarder instance.</param>
    /// <param name="context">The HttpContext to forward.</param>
    /// <param name="destinationPrefix">The url prefix for where to forward the request to.</param>
    /// <param name="httpClient">The HTTP client used to forward the request.</param>
    /// <param name="requestConfig">Config for the outgoing request.</param>
    /// <param name="requestTransform">Transform function to apply to the forwarded request.</param>
    /// <returns>The status of a forwarding operation.</returns>
    public static ValueTask<ForwarderError> SendAsync(this IHttpForwarder forwarder, HttpContext context, string destinationPrefix,
        HttpMessageInvoker httpClient, ForwarderRequestConfig requestConfig, Func<HttpContext, HttpRequestMessage, ValueTask> requestTransform)
    {
        return forwarder.SendAsync(context, destinationPrefix, httpClient, requestConfig, new RequestTransformer(requestTransform));
    }
}
