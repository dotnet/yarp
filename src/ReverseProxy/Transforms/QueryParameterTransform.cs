// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;

namespace Yarp.ReverseProxy.Transforms;

public abstract class QueryParameterTransform : RequestTransform
{
    public QueryParameterTransform(QueryStringTransformMode mode, string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException($"'{nameof(key)}' cannot be null or empty.", nameof(key));
        }

        Mode = mode;
        Key = key;
    }

    internal QueryStringTransformMode Mode { get; }

    internal string Key { get; }

    /// <inheritdoc/>
    public override ValueTask ApplyAsync(RequestTransformContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var value = GetValue(context);
        if (value is not null)
        {
            switch (Mode)
            {
                case QueryStringTransformMode.Append:
                    StringValues newValue = value;
                    if (context.Query.Collection.TryGetValue(Key, out var currentValue))
                    {
                        newValue = StringValues.Concat(currentValue, value);
                    }
                    context.Query.Collection[Key] = newValue;
                    break;
                case QueryStringTransformMode.Set:
                    context.Query.Collection[Key] = value;
                    break;
                default:
                    throw new NotImplementedException(Mode.ToString());
            }
        }

        return default;
    }

    protected abstract string? GetValue(RequestTransformContext context);
}

public enum QueryStringTransformMode
{
    Append,
    Set
}
