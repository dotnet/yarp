// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Http;
using System.Threading;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Forwarder;

namespace Yarp.ReverseProxy.Transforms;

/// <summary>
/// Transform state for use with <see cref="ResponseTransform"/>
/// </summary>
public class ResponseTransformContext
{
    /// <summary>
    /// The current request context.
    /// </summary>
    public HttpContext HttpContext { get; init; } = default!;

    /// <summary>
    /// The proxy response. This can be null if the destination did not respond.
    /// When null, check <see cref="HttpContext.Features"/> Get&lt;IForwarderErrorFeature&gt;() method 
    /// or <see cref="HttpContextFeaturesExtensions.GetForwarderErrorFeature"/> method
    /// for details about the error via the <see cref="IForwarderErrorFeature"/>.
    /// </summary>
    public HttpResponseMessage? ProxyResponse { get; init; }

    /// <summary>
    /// Gets or sets if the response headers have been copied from the HttpResponseMessage and HttpContent
    /// to the HttpResponse. Transforms use this when searching for the current value of a header they
    /// should operate on.
    /// </summary>
    public bool HeadersCopied { get; set; }

    /// <summary>
    /// Set to true if the proxy should exclude the body and trailing headers when proxying this response.
    /// Defaults to false.
    /// </summary>
    public bool SuppressResponseBody { get; set; }

    /// <summary>
    /// A <see cref="CancellationToken"/> indicating that the request is being aborted.
    /// </summary>
    public CancellationToken CancellationToken { get; set; }
}
