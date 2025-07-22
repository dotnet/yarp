// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;

namespace Yarp.ReverseProxy.Transforms;

/// <summary>
/// Sets or appends the X-Forwarded-Prefix header with the request's original PathBase.
/// </summary>
public class RequestHeaderXForwardedPrefixTransform : RequestTransform
{
    public RequestHeaderXForwardedPrefixTransform(string headerName, ForwardedTransformActions action)
    {
        if (string.IsNullOrEmpty(headerName))
        {
            throw new ArgumentException($"'{nameof(headerName)}' cannot be null or empty.", nameof(headerName));
        }

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

        var pathBase = context.HttpContext.Request.PathBase;

        switch (TransformAction)
        {
            case ForwardedTransformActions.Set:
                RemoveHeader(context, HeaderName);
                if (pathBase.HasValue)
                {
                    AddHeader(context, HeaderName, pathBase.ToUriComponent());
                }
                break;
            case ForwardedTransformActions.Append:
                Append(context, pathBase);
                break;
            case ForwardedTransformActions.Remove:
                RemoveHeader(context, HeaderName);
                break;
            default:
                throw new NotImplementedException(TransformAction.ToString());
        }

        return default;
    }

    private void Append(RequestTransformContext context, Microsoft.AspNetCore.Http.PathString pathBase)
    {
        var existingValues = TakeHeader(context, HeaderName);
        if (!pathBase.HasValue)
        {
            if (!string.IsNullOrEmpty(existingValues))
            {
                AddHeader(context, HeaderName, existingValues);
            }
        }
        else
        {
            var values = StringValues.Concat(existingValues, pathBase.ToUriComponent());
            AddHeader(context, HeaderName, values);
        }
    }
}
