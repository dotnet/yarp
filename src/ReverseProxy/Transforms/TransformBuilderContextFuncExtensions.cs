// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Yarp.ReverseProxy.Transforms;

/// <summary>
/// Extension methods for <see cref="TransformBuilderContext"/>.
/// </summary>
public static class TransformBuilderContextFuncExtensions
{
    /// <summary>
    /// Adds a transform Func that runs on each request for the given route.
    /// </summary>
    public static TransformBuilderContext AddRequestTransform(this TransformBuilderContext context, Func<RequestTransformContext, ValueTask> func)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(func);

        context.RequestTransforms.Add(new RequestFuncTransform(func));
        return context;
    }

    /// <summary>
    /// Adds a transform Func that runs on each response for the given route.
    /// </summary>
    public static TransformBuilderContext AddResponseTransform(this TransformBuilderContext context, Func<ResponseTransformContext, ValueTask> func)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(func);

        context.ResponseTransforms.Add(new ResponseFuncTransform(func));
        return context;
    }

    /// <summary>
    /// Adds a transform Func that runs on each response for the given route.
    /// </summary>
    public static TransformBuilderContext AddResponseTrailersTransform(this TransformBuilderContext context, Func<ResponseTrailersTransformContext, ValueTask> func)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(func);

        context.ResponseTrailersTransforms.Add(new ResponseTrailersFuncTransform(func));
        return context;
    }
}
