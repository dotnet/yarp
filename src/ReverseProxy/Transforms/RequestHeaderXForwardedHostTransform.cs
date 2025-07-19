// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;

namespace Yarp.ReverseProxy.Transforms;

/// <summary>
/// Sets or appends the X-Forwarded-Host header with the request's original Host header.
/// </summary>
public class RequestHeaderXForwardedHostTransform : RequestTransform
{
    /// <summary>
    /// Creates a new transform.
    /// </summary>
    /// <param name="headerName">The header name.</param>
    /// <param name="action">Action to applied to the header.</param>
    public RequestHeaderXForwardedHostTransform(string headerName, ForwardedTransformActions action)
    {
        ArgumentException.ThrowIfNullOrEmpty(headerName);

        HeaderName = headerName;
        Debug.Assert(action != ForwardedTransformActions.Off);
        TransformAction = action;
    }

    internal string HeaderName { get; }
    internal ForwardedTransformActions TransformAction { get; }

    /// <inheritdoc/>
    public override ValueTask ApplyAsync(RequestTransformContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var host = context.HttpContext.Request.Host;

        switch (TransformAction)
        {
            case ForwardedTransformActions.Set:
                RemoveHeader(context, HeaderName);
                if (host.HasValue)
                {
                    AddHeader(context, HeaderName, host.ToUriComponent());
                }
                break;
            case ForwardedTransformActions.Append:
                Append(context, host);
                break;
            case ForwardedTransformActions.Remove:
                RemoveHeader(context, HeaderName);
                break;
            default:
                throw new NotImplementedException(TransformAction.ToString());
        }

        return default;
    }

    private void Append(RequestTransformContext context, Microsoft.AspNetCore.Http.HostString host)
    {
        var existingValues = TakeHeader(context, HeaderName);
        if (!host.HasValue)
        {
            if (!string.IsNullOrEmpty(existingValues))
            {
                AddHeader(context, HeaderName, existingValues);
            }
        }
        else
        {
            var values = StringValues.Concat(existingValues, host.ToUriComponent());
            AddHeader(context, HeaderName, values);
        }
    }
}
