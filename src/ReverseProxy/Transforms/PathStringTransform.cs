// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Yarp.ReverseProxy.Transforms;

/// <summary>
/// Modifies the proxy request Path with the given value.
/// </summary>
public class PathStringTransform : RequestTransform
{
    /// <summary>
    /// Creates a new transform.
    /// </summary>
    /// <param name="mode">A <see cref="PathTransformMode"/> indicating how the given value should update the existing path.</param>
    /// <param name="value">The path value used to update the existing value.</param>
    public PathStringTransform(PathTransformMode mode, PathString value)
    {
        ArgumentNullException.ThrowIfNull(value.Value);

        Mode = mode;
        Value = value;
    }

    internal PathString Value { get; }

    internal PathTransformMode Mode { get; }

    /// <inheritdoc/>
    public override ValueTask ApplyAsync(RequestTransformContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        switch (Mode)
        {
            case PathTransformMode.Set:
                context.Path = Value;
                break;
            case PathTransformMode.Prefix:
                context.Path = Value + context.Path;
                break;
            case PathTransformMode.RemovePrefix:
                context.Path = context.Path.StartsWithSegments(Value, out var remainder) ? remainder : context.Path;
                break;
            default:
                throw new NotImplementedException(Mode.ToString());
        }

        return default;
    }

    public enum PathTransformMode
    {
        Set,
        Prefix,
        RemovePrefix,
    }
}
