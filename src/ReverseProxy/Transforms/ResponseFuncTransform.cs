// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;

namespace Yarp.ReverseProxy.Transforms;

/// <summary>
/// A response transform that runs the given Func.
/// </summary>
public class ResponseFuncTransform : ResponseTransform
{
    private readonly Func<ResponseTransformContext, ValueTask> _func;

    public ResponseFuncTransform(Func<ResponseTransformContext, ValueTask> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        _func = func;
    }

    /// <inheritdoc/>
    public override ValueTask ApplyAsync(ResponseTransformContext context)
    {
        return _func(context);
    }
}
