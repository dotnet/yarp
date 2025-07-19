// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;

namespace Yarp.ReverseProxy.Transforms;

/// <summary>
/// Sets or appends simple request header values.
/// </summary>
public class RequestHeaderValueTransform : RequestHeaderTransform
{
    public RequestHeaderValueTransform(string headerName, string value, bool append) : base(headerName, append)
    {
        ArgumentException.ThrowIfNullOrEmpty(headerName);
        ArgumentNullException.ThrowIfNull(value);
        Value = value;
    }

    internal string Value { get; }

    /// <inheritdoc/>
    public override ValueTask ApplyAsync(RequestTransformContext context)
    {
        return base.ApplyAsync(context);
    }

    /// <inheritdoc/>
    protected override string GetValue(RequestTransformContext context)
    {
        return Value;
    }
}
