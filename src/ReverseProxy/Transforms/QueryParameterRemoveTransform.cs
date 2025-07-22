// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;

namespace Yarp.ReverseProxy.Transforms;

/// <summary>
/// A request transform that removes the given query parameter.
/// </summary>
public class QueryParameterRemoveTransform : RequestTransform
{
    public QueryParameterRemoveTransform(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException($"'{nameof(key)}' cannot be null or empty.", nameof(key));
        }

        Key = key;
    }

    internal string Key { get; }

    /// <inheritdoc/>
    public override ValueTask ApplyAsync(RequestTransformContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Query.Collection.Remove(Key);

        return default;
    }
}
