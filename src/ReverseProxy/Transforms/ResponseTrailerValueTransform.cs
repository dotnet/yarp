// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;

namespace Yarp.ReverseProxy.Transforms;

/// <summary>
/// Sets or appends simple response trailer values.
/// </summary>
public class ResponseTrailerValueTransform : ResponseTrailersTransform
{
    public ResponseTrailerValueTransform(string headerName, string value, bool append, ResponseCondition condition)
    {
        if (string.IsNullOrEmpty(headerName))
        {
            throw new ArgumentException($"'{nameof(headerName)}' cannot be null or empty.", nameof(headerName));
        }

        HeaderName = headerName;
        Value = value ?? throw new ArgumentNullException(nameof(value));
        Append = append;
        Condition = condition;
    }

    internal ResponseCondition Condition { get; }

    internal bool Append { get; }

    internal string HeaderName { get; }

    internal string Value { get; }

    // Assumes the response status code has been set on the HttpContext already.
    /// <inheritdoc/>
    public override ValueTask ApplyAsync(ResponseTrailersTransformContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (Condition == ResponseCondition.Always
            || Success(context) == (Condition == ResponseCondition.Success))
        {
            if (Append)
            {
                var existingHeader = TakeHeader(context, HeaderName);
                var value = StringValues.Concat(existingHeader, Value);
                SetHeader(context, HeaderName, value);
            }
            else
            {
                SetHeader(context, HeaderName, Value);
            }
        }

        return default;
    }
}
