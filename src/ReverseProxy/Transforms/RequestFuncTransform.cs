// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;

namespace Yarp.ReverseProxy.Transforms;

/// <summary>
/// A request transform that runs the given Func.
/// </summary>
public class RequestFuncTransform : RequestTransform
{
    private readonly Func<RequestTransformContext, ValueTask> _func;

    public RequestFuncTransform(Func<RequestTransformContext, ValueTask> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        _func = func;
    }

    /// <inheritdoc/>
    public override ValueTask ApplyAsync(RequestTransformContext context)
    {
        return _func(context);
    }
}
