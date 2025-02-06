// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Yarp.ReverseProxy.Transforms;

internal sealed class HttpMethodTransformFactory : ITransformFactory
{
    internal const string HttpMethodChangeKey = "HttpMethodChange";
    internal const string SetKey = "Set";

    public bool Validate(TransformRouteValidationContext context, IReadOnlyDictionary<string, string> transformValues)
    {
        if (transformValues.TryGetValue(HttpMethodChangeKey, out var _))
        {
            TransformHelpers.CheckTooManyParameters(transformValues, expected: 2);
            if (!transformValues.TryGetValue(SetKey, out var _))
            {
                context.Errors.Add(new ArgumentException($"Unexpected parameters for HttpMethod: {string.Join(';', transformValues.Keys)}. Expected 'Set'"));
            }
        }
        else
        {
            return false;
        }

        return true;
    }

    public bool Build(TransformBuilderContext context, IReadOnlyDictionary<string, string> transformValues)
    {
        if (transformValues.TryGetValue(HttpMethodChangeKey, out var fromHttpMethod))
        {
            TransformHelpers.CheckTooManyParameters(transformValues, expected: 2);
            if (transformValues.TryGetValue(SetKey, out var toHttpMethod))
            {
                context.AddHttpMethodChange(fromHttpMethod, toHttpMethod);
            }
        }
        else
        {
            return false;
        }

        return true;
    }
}
