// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Yarp.ReverseProxy.Transforms.Builder;

internal sealed class ActionTransformProvider : ITransformProvider
{
    private readonly Action<TransformBuilderContext> _action;

    public ActionTransformProvider(Action<TransformBuilderContext> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _action = action;
    }

    public void Apply(TransformBuilderContext transformBuildContext)
    {
        _action(transformBuildContext);
    }

    public void ValidateRoute(TransformRouteValidationContext context)
    {
    }

    public void ValidateCluster(TransformClusterValidationContext context)
    {
    }
}
