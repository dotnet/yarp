// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Yarp.ReverseProxy.Transforms;

public class QueryParameterFromStaticTransform : QueryParameterTransform
{
    public QueryParameterFromStaticTransform(QueryStringTransformMode mode, string key, string value)
        : base(mode, key)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException($"'{nameof(key)}' cannot be null or empty.", nameof(key));
        }

        ArgumentNullException.ThrowIfNull(value);

        Value = value;
    }

    internal string Value { get; }

    /// <inheritdoc/>
    protected override string GetValue(RequestTransformContext context)
    {
        return Value;
    }
}
