// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;

namespace Yarp.ReverseProxy.Transforms;

/// <summary>
/// A response trailers transform that runs the given Func.
/// </summary>
public class ResponseTrailersFuncTransform : ResponseTrailersTransform
{
    private readonly Func<ResponseTrailersTransformContext, ValueTask> _func;

    public ResponseTrailersFuncTransform(Func<ResponseTrailersTransformContext, ValueTask> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        _func = func;
    }

    /// <inheritdoc/>
    public override ValueTask ApplyAsync(ResponseTrailersTransformContext context)
    {
        return _func(context);
    }
}
