// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Yarp.ReverseProxy.Forwarder;

/// <summary>
/// Forward an HTTP request to a chosen destination.
/// </summary>
public interface IHttpForwarder
{
    /// <summary>
    /// Forwards the incoming request to the destination server, and the response back to the client.
    /// </summary>
    /// <param name="context">The HttpContext to forward.</param>
    /// <param name="destinationPrefix">The url prefix for where to forward the request to.</param>
    /// <param name="httpClient">The HTTP client used to forward the request.</param>
    /// <param name="requestConfig">Config for the outgoing request.</param>
    /// <param name="transformer">Request and response transforms. Use <see cref="HttpTransformer.Default"/> if
    /// custom transformations are not needed.</param>
    /// <returns>The result of forwarding the request and response.</returns>
    ValueTask<ForwarderError> SendAsync(HttpContext context, string destinationPrefix, HttpMessageInvoker httpClient,
        ForwarderRequestConfig requestConfig, HttpTransformer transformer);

    /// <summary>
    /// Forwards the incoming request to the destination server, and the response back to the client.
    /// </summary>
    /// <param name="context">The HttpContext to forward.</param>
    /// <param name="destinationPrefix">The url prefix for where to forward the request to.</param>
    /// <param name="httpClient">The HTTP client used to forward the request.</param>
    /// <param name="requestConfig">Config for the outgoing request.</param>
    /// <param name="transformer">Request and response transforms. Use <see cref="HttpTransformer.Default"/> if
    /// custom transformations are not needed.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to abort the request.</param>
    /// <returns>The result of forwarding the request and response.</returns>
    ValueTask<ForwarderError> SendAsync(HttpContext context, string destinationPrefix, HttpMessageInvoker httpClient,
        ForwarderRequestConfig requestConfig, HttpTransformer transformer, CancellationToken cancellationToken)
        => SendAsync(context, destinationPrefix, httpClient, requestConfig, transformer);
}
