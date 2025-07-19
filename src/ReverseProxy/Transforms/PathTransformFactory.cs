// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing.Template;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Yarp.ReverseProxy.Transforms;

internal sealed class PathTransformFactory : ITransformFactory
{
    internal const string PathSetKey = "PathSet";
    internal const string PathPrefixKey = "PathPrefix";
    internal const string PathRemovePrefixKey = "PathRemovePrefix";
    internal const string PathPatternKey = "PathPattern";

    private readonly TemplateBinderFactory _binderFactory;

    public PathTransformFactory(TemplateBinderFactory binderFactory)
    {
        ArgumentNullException.ThrowIfNull(binderFactory);
        _binderFactory = binderFactory;
    }

    public bool Validate(TransformRouteValidationContext context, IReadOnlyDictionary<string, string> transformValues)
    {
        if (transformValues.TryGetValue(PathSetKey, out var pathSet))
        {
            TransformHelpers.TryCheckTooManyParameters(context, transformValues, expected: 1);
            CheckPathNotNull(context, PathSetKey, pathSet);
        }
        else if (transformValues.TryGetValue(PathPrefixKey, out var pathPrefix))
        {
            TransformHelpers.TryCheckTooManyParameters(context, transformValues, expected: 1);
            CheckPathNotNull(context, PathPrefixKey, pathPrefix);
        }
        else if (transformValues.TryGetValue(PathRemovePrefixKey, out var pathRemovePrefix))
        {
            TransformHelpers.TryCheckTooManyParameters(context, transformValues, expected: 1);
            CheckPathNotNull(context, PathRemovePrefixKey, pathRemovePrefix);
        }
        else if (transformValues.TryGetValue(PathPatternKey, out var pathPattern))
        {
            TransformHelpers.TryCheckTooManyParameters(context, transformValues, expected: 1);
            CheckPathNotNull(context, PathPatternKey, pathPattern);
            // TODO: Validate the pattern format. Does it build?
        }
        else
        {
            return false;
        }

        return true;
    }

    private static void CheckPathNotNull(TransformRouteValidationContext context, string fieldName, string? path)
    {
        if (path is null)
        {
            context.Errors.Add(new ArgumentNullException(fieldName));
        }
    }

    public bool Build(TransformBuilderContext context, IReadOnlyDictionary<string, string> transformValues)
    {
        if (transformValues.TryGetValue(PathSetKey, out var pathSet))
        {
            TransformHelpers.CheckTooManyParameters(transformValues, expected: 1);
            var path = MakePathString(pathSet);
            context.AddPathSet(path);
        }
        else if (transformValues.TryGetValue(PathPrefixKey, out var pathPrefix))
        {
            TransformHelpers.CheckTooManyParameters(transformValues, expected: 1);
            var path = MakePathString(pathPrefix);
            context.AddPathPrefix(path);
        }
        else if (transformValues.TryGetValue(PathRemovePrefixKey, out var pathRemovePrefix))
        {
            TransformHelpers.CheckTooManyParameters(transformValues, expected: 1);
            var path = MakePathString(pathRemovePrefix);
            context.AddPathRemovePrefix(path);
        }
        else if (transformValues.TryGetValue(PathPatternKey, out var pathPattern))
        {
            TransformHelpers.CheckTooManyParameters(transformValues, expected: 1);
            var path = MakePathString(pathPattern);
            // We don't use the extension here because we want to avoid doing a DI lookup for the binder every time.
            context.RequestTransforms.Add(new PathRouteValuesTransform(path.Value!, _binderFactory));
        }
        else
        {
            return false;
        }

        return true;
    }

    private static PathString MakePathString(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!path.StartsWith('/'))
        {
            path = "/" + path;
        }
        return new PathString(path);
    }
}
