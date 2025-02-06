// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Net.Http;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Yarp.ReverseProxy.Sample;

internal sealed class MyTransformFactory : ITransformFactory
{
    public bool Validate(TransformRouteValidationContext context, IReadOnlyDictionary<string, string> transformValues)
    {
        if (transformValues.TryGetValue("CustomTransform", out var value))
        {
            if (string.IsNullOrEmpty(value))
            {
                context.Errors.Add(new ArgumentException("A non-empty CustomTransform value is required"));
            }

            return true; // Matched
        }
        return false;
    }

    public bool Build(TransformBuilderContext context, IReadOnlyDictionary<string, string> transformValues)
    {
        if (transformValues.TryGetValue("CustomTransform", out var value))
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException("A non-empty CustomTransform value is required");
            }

            context.AddRequestTransform(transformContext =>
            {
                transformContext.ProxyRequest.Options.Set(new HttpRequestOptionsKey<string>("CustomTransform"), value);
                return default;
            });

            return true;
        }

        return false;
    }
}
