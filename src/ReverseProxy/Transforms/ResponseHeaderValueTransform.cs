// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;

namespace Yarp.ReverseProxy.Transforms;

/// <summary>
/// Sets or appends simple response header values.
/// </summary>
public class ResponseHeaderValueTransform : ResponseTransform
{
    public ResponseHeaderValueTransform(string headerName, string value, bool append, ResponseCondition condition)
    {
        if (string.IsNullOrEmpty(headerName))
        {
            throw new ArgumentException($"'{nameof(headerName)}' cannot be null or empty.", nameof(headerName));
        }

        HeaderName = headerName;
        ArgumentNullException.ThrowIfNull(value);
        Value = value;
        Append = append;
        Condition = condition;
    }

    internal ResponseCondition Condition { get; }

    internal bool Append { get; }

    internal string HeaderName { get; }

    internal string Value { get; }

    // Assumes the response status code has been set on the HttpContext already.
    /// <inheritdoc/>
    public override ValueTask ApplyAsync(ResponseTransformContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

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
