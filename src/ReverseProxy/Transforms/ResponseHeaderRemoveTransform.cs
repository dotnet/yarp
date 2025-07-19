// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;

namespace Yarp.ReverseProxy.Transforms;

/// <summary>
/// Removes a response header.
/// </summary>
public class ResponseHeaderRemoveTransform : ResponseTransform
{
    public ResponseHeaderRemoveTransform(string headerName, ResponseCondition condition)
    {
        if (string.IsNullOrEmpty(headerName))
        {
            throw new ArgumentException($"'{nameof(headerName)}' cannot be null or empty.", nameof(headerName));
        }

        HeaderName = headerName;
        Condition = condition;
    }

    internal string HeaderName { get; }

    internal ResponseCondition Condition { get; }

    // Assumes the response status code has been set on the HttpContext already.
    /// <inheritdoc/>
    public override ValueTask ApplyAsync(ResponseTransformContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (Condition == ResponseCondition.Always
            || Success(context) == (Condition == ResponseCondition.Success))
        {
            context.HttpContext.Response.Headers.Remove(HeaderName);
        }

        return default;
    }
}
