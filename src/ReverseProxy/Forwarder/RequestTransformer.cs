// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Yarp.ReverseProxy.Forwarder;

internal sealed class RequestTransformer : HttpTransformer
{
    private readonly Func<HttpContext, HttpRequestMessage, ValueTask> _requestTransform;

    public RequestTransformer(Func<HttpContext, HttpRequestMessage, ValueTask> requestTransform)
    {
        _requestTransform = requestTransform;
    }

    public override async ValueTask TransformRequestAsync(HttpContext httpContext, HttpRequestMessage proxyRequest, string destinationPrefix, CancellationToken cancellationToken)
    {
        await base.TransformRequestAsync(httpContext, proxyRequest, destinationPrefix, cancellationToken);
        await _requestTransform(httpContext, proxyRequest);
    }
}
