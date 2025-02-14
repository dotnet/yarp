// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Yarp.ReverseProxy.Transforms.Builder;

public static class TransformHelpers
{
    public static void TryCheckTooManyParameters(TransformRouteValidationContext context, IReadOnlyDictionary<string, string> rawTransform, int expected)
    {
        if (rawTransform.Count > expected)
        {
            context.Errors.Add(new InvalidOperationException("The transform contains more parameters than expected: " + string.Join(';', rawTransform.Keys)));
        }
    }

    public static void CheckTooManyParameters(IReadOnlyDictionary<string, string> rawTransform, int expected)
    {
        if (rawTransform.Count > expected)
        {
            throw new InvalidOperationException("The transform contains more parameters than expected: " + string.Join(';', rawTransform.Keys));
        }
    }

    internal static void RemoveAllXForwardedHeaders(TransformBuilderContext context, string prefix)
    {
        context.AddXForwardedFor(prefix + ForwardedTransformFactory.ForKey, ForwardedTransformActions.Remove);
        context.AddXForwardedPrefix(prefix + ForwardedTransformFactory.PrefixKey, ForwardedTransformActions.Remove);
        context.AddXForwardedHost(prefix + ForwardedTransformFactory.HostKey, ForwardedTransformActions.Remove);
        context.AddXForwardedProto(prefix + ForwardedTransformFactory.ProtoKey, ForwardedTransformActions.Remove);
    }

    internal static void RemoveForwardedHeader(TransformBuilderContext context)
    {
        context.RequestTransforms.Add(RequestHeaderForwardedTransform.RemoveTransform);
    }
}
