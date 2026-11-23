// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Yarp.ReverseProxy.Transforms;

internal sealed class QueryTransformFactory : ITransformFactory
{
    internal const string QueryValueParameterKey = "QueryValueParameter";
    internal const string QueryRouteParameterKey = "QueryRouteParameter";
    internal const string QueryParameterKey = "QueryParameter";
    internal const string QueryRemoveParameterKey = "QueryRemoveParameter";
    internal const string AppendKey = "Append";
    internal const string SetKey = "Set";

    public bool Validate(TransformRouteValidationContext context, IReadOnlyDictionary<string, string> transformValues)
    {
        if (transformValues.TryGetValue(QueryValueParameterKey, out var queryValueParameter))
        {
            TransformHelpers.TryCheckTooManyParameters(context, transformValues, expected: 2);
            if (!transformValues.TryGetValue(AppendKey, out var _) && !transformValues.TryGetValue(SetKey, out var _))
            {
                context.Errors.Add(new ArgumentException($"Unexpected parameters for QueryValueParameter: {string.Join(';', transformValues.Keys)}. Expected 'Append' or 'Set'."));
            }
        }
        else if (transformValues.TryGetValue(QueryRouteParameterKey, out var queryRouteParameter))
        {
            TransformHelpers.TryCheckTooManyParameters(context, transformValues, expected: 2);
            if (!transformValues.TryGetValue(AppendKey, out var _) && !transformValues.TryGetValue(SetKey, out var _))
            {
                context.Errors.Add(new ArgumentException($"Unexpected parameters for QueryRouteParameter: {string.Join(';', transformValues.Keys)}. Expected 'Append' or 'Set'."));
            }
        }
        else if (transformValues.TryGetValue(QueryParameterKey, out var queryParameter))
        {
            TransformHelpers.TryCheckTooManyParameters(context, transformValues, expected: 2);
            if (!transformValues.TryGetValue(SetKey, out var _) || transformValues.TryGetValue(AppendKey, out var _))
            {
                context.Errors.Add(new ArgumentException($"Unexpected parameters for QueryParameter: {string.Join(';', transformValues.Keys)}. Expected 'Set'."));
            }
        }
        else if (transformValues.TryGetValue(QueryRemoveParameterKey, out var removeQueryParameter))
        {
            TransformHelpers.TryCheckTooManyParameters(context, transformValues, expected: 1);
        }
        else
        {
            return false;
        }

        return true;
    }

    public bool Build(TransformBuilderContext context, IReadOnlyDictionary<string, string> transformValues)
    {
        if (transformValues.TryGetValue(QueryValueParameterKey, out var queryValueParameter))
        {
            TransformHelpers.CheckTooManyParameters(transformValues, expected: 2);
            if (transformValues.TryGetValue(AppendKey, out var appendValue))
            {
                context.AddQueryValue(queryValueParameter, appendValue, append: true);
            }
            else if (transformValues.TryGetValue(SetKey, out var setValue))
            {
                context.AddQueryValue(queryValueParameter, setValue, append: false);
            }
            else
            {
                throw new NotSupportedException(string.Join(";", transformValues.Keys));
            }
        }
        else if (transformValues.TryGetValue(QueryRouteParameterKey, out var queryRouteParameter))
        {
            TransformHelpers.CheckTooManyParameters(transformValues, expected: 2);
            if (transformValues.TryGetValue(AppendKey, out var routeValueKeyAppend))
            {
                context.AddQueryRouteValue(queryRouteParameter, routeValueKeyAppend, append: true);
            }
            else if (transformValues.TryGetValue(SetKey, out var routeValueKeySet))
            {
                context.AddQueryRouteValue(queryRouteParameter, routeValueKeySet, append: false);
            }
            else
            {
                throw new NotSupportedException(string.Join(";", transformValues.Keys));
            }
        }
        else if (transformValues.TryGetValue(QueryParameterKey, out var queryParameter))
        {
            TransformHelpers.CheckTooManyParameters(transformValues, expected: 2);
            if (transformValues.TryGetValue(SetKey, out var setValue) && !transformValues.TryGetValue(AppendKey, out var _))
            {
                context.AddQueryParameter(queryParameter, setValue);
            }
            else
            {
                throw new NotSupportedException(string.Join(";", transformValues.Keys));
            }
        }
        else if (transformValues.TryGetValue(QueryRemoveParameterKey, out var removeQueryParameter))
        {
            TransformHelpers.CheckTooManyParameters(transformValues, expected: 1);
            context.AddQueryRemoveKey(removeQueryParameter);
        }
        else
        {
            return false;
        }

        return true;
    }
}
