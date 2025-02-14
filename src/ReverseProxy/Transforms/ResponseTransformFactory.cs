// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Yarp.ReverseProxy.Transforms;

internal sealed class ResponseTransformFactory : ITransformFactory
{
    internal const string ResponseHeadersCopyKey = "ResponseHeadersCopy";
    internal const string ResponseTrailersCopyKey = "ResponseTrailersCopy";
    internal const string ResponseHeaderKey = "ResponseHeader";
    internal const string ResponseTrailerKey = "ResponseTrailer";
    internal const string ResponseHeaderRemoveKey = "ResponseHeaderRemove";
    internal const string ResponseTrailerRemoveKey = "ResponseTrailerRemove";
    internal const string ResponseHeadersAllowedKey = "ResponseHeadersAllowed";
    internal const string ResponseTrailersAllowedKey = "ResponseTrailersAllowed";
    internal const string WhenKey = "When";
    internal const string AppendKey = "Append";
    internal const string SetKey = "Set";

    public bool Validate(TransformRouteValidationContext context, IReadOnlyDictionary<string, string> transformValues)
    {
        if (transformValues.TryGetValue(ResponseHeadersCopyKey, out var copyHeaders))
        {
            TransformHelpers.TryCheckTooManyParameters(context, transformValues, expected: 1);
            if (!bool.TryParse(copyHeaders, out var _))
            {
                context.Errors.Add(new ArgumentException($"Unexpected value for ResponseHeadersCopy: {copyHeaders}. Expected 'true' or 'false'"));
            }
        }
        else if (transformValues.TryGetValue(ResponseTrailersCopyKey, out copyHeaders))
        {
            TransformHelpers.TryCheckTooManyParameters(context, transformValues, expected: 1);
            if (!bool.TryParse(copyHeaders, out var _))
            {
                context.Errors.Add(new ArgumentException($"Unexpected value for ResponseTrailersCopy: {copyHeaders}. Expected 'true' or 'false'"));
            }
        }
        else if (transformValues.TryGetValue(ResponseHeaderKey, out var _))
        {
            if (transformValues.TryGetValue(WhenKey, out var whenValue))
            {
                TransformHelpers.TryCheckTooManyParameters(context, transformValues, expected: 3);
                if (!Enum.TryParse<ResponseCondition>(whenValue, ignoreCase: true, out var _))
                {
                    context.Errors.Add(new ArgumentException($"Unexpected value for ResponseHeader:When: {whenValue}. Expected 'Always', 'Success', or 'Failure'"));
                }
            }
            else
            {
                TransformHelpers.TryCheckTooManyParameters(context, transformValues, expected: 2);
            }

            if (!transformValues.TryGetValue(SetKey, out var _) && !transformValues.TryGetValue(AppendKey, out var _))
            {
                context.Errors.Add(new ArgumentException($"Unexpected parameters for ResponseHeader: {string.Join(';', transformValues.Keys)}. Expected 'Set' or 'Append'"));
            }
        }
        else if (transformValues.TryGetValue(ResponseTrailerKey, out var _))
        {
            if (transformValues.TryGetValue(WhenKey, out var whenValue))
            {
                TransformHelpers.TryCheckTooManyParameters(context, transformValues, expected: 3);
                if (!Enum.TryParse<ResponseCondition>(whenValue, ignoreCase: true, out var _))
                {
                    context.Errors.Add(new ArgumentException($"Unexpected value for ResponseTrailer:When: {whenValue}. Expected 'Always', 'Success', or 'Failure'"));
                }
            }
            else
            {
                TransformHelpers.TryCheckTooManyParameters(context, transformValues, expected: 2);
            }

            if (!transformValues.TryGetValue(SetKey, out var _) && !transformValues.TryGetValue(AppendKey, out var _))
            {
                context.Errors.Add(new ArgumentException($"Unexpected parameters for ResponseTrailer: {string.Join(';', transformValues.Keys)}. Expected 'Set' or 'Append'"));
            }
        }
        else if (transformValues.TryGetValue(ResponseHeaderRemoveKey, out var _))
        {
            if (transformValues.TryGetValue(WhenKey, out var whenValue))
            {
                TransformHelpers.TryCheckTooManyParameters(context, transformValues, expected: 2);
                if (!Enum.TryParse<ResponseCondition>(whenValue, ignoreCase: true, out var _))
                {
                    context.Errors.Add(new ArgumentException($"Unexpected value for ResponseHeaderRemove:When: {whenValue}. Expected 'Always', 'Success', or 'Failure'"));
                }
            }
            else
            {
                TransformHelpers.TryCheckTooManyParameters(context, transformValues, expected: 1);
            }
        }
        else if (transformValues.TryGetValue(ResponseTrailerRemoveKey, out var _))
        {
            if (transformValues.TryGetValue(WhenKey, out var whenValue))
            {
                TransformHelpers.TryCheckTooManyParameters(context, transformValues, expected: 2);
                if (!Enum.TryParse<ResponseCondition>(whenValue, ignoreCase: true, out var _))
                {
                    context.Errors.Add(new ArgumentException($"Unexpected value for ResponseTrailerRemove:When: {whenValue}. Expected 'Always', 'Success', or 'Failure'"));
                }
            }
            else
            {
                TransformHelpers.TryCheckTooManyParameters(context, transformValues, expected: 1);
            }
        }
        else if (transformValues.TryGetValue(ResponseHeadersAllowedKey, out var _))
        {
            TransformHelpers.TryCheckTooManyParameters(context, transformValues, expected: 1);
        }
        else if (transformValues.TryGetValue(ResponseTrailersAllowedKey, out var _))
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
        if (transformValues.TryGetValue(ResponseHeadersCopyKey, out var copyHeaders))
        {
            TransformHelpers.CheckTooManyParameters(transformValues, expected: 1);
            context.CopyResponseHeaders = bool.Parse(copyHeaders);
        }
        else if (transformValues.TryGetValue(ResponseTrailersCopyKey, out copyHeaders))
        {
            TransformHelpers.CheckTooManyParameters(transformValues, expected: 1);
            context.CopyResponseTrailers = bool.Parse(copyHeaders);
        }
        else if (transformValues.TryGetValue(ResponseHeaderKey, out var responseHeaderName))
        {
            var condition = ResponseCondition.Success;
            if (transformValues.TryGetValue(WhenKey, out var whenValue))
            {
                TransformHelpers.CheckTooManyParameters(transformValues, expected: 3);
                condition = Enum.Parse<ResponseCondition>(whenValue, ignoreCase: true);
            }
            else
            {
                TransformHelpers.CheckTooManyParameters(transformValues, expected: 2);
            }

            if (transformValues.TryGetValue(SetKey, out var setValue))
            {
                context.AddResponseHeader(responseHeaderName, setValue, append: false, condition);
            }
            else if (transformValues.TryGetValue(AppendKey, out var appendValue))
            {
                context.AddResponseHeader(responseHeaderName, appendValue, append: true, condition);
            }
            else
            {
                throw new ArgumentException($"Unexpected parameters for ResponseHeader: {string.Join(';', transformValues.Keys)}. Expected 'Set' or 'Append'");
            }
        }
        else if (transformValues.TryGetValue(ResponseTrailerKey, out var responseTrailerName))
        {
            var condition = ResponseCondition.Success;
            if (transformValues.TryGetValue(WhenKey, out var whenValue))
            {
                TransformHelpers.CheckTooManyParameters(transformValues, expected: 3);
                condition = Enum.Parse<ResponseCondition>(whenValue, ignoreCase: true);
            }
            else
            {
                TransformHelpers.CheckTooManyParameters(transformValues, expected: 2);
            }

            if (transformValues.TryGetValue(SetKey, out var setValue))
            {
                context.AddResponseTrailer(responseTrailerName, setValue, append: false, condition);
            }
            else if (transformValues.TryGetValue(AppendKey, out var appendValue))
            {
                context.AddResponseTrailer(responseTrailerName, appendValue, append: true, condition);
            }
            else
            {
                throw new ArgumentException($"Unexpected parameters for ResponseTrailer: {string.Join(';', transformValues.Keys)}. Expected 'Set' or 'Append'");
            }
        }
        else if (transformValues.TryGetValue(ResponseHeaderRemoveKey, out var removeResponseHeaderName))
        {
            var condition = ResponseCondition.Success;
            if (transformValues.TryGetValue(WhenKey, out var whenValue))
            {
                TransformHelpers.CheckTooManyParameters(transformValues, expected: 2);
                condition = Enum.Parse<ResponseCondition>(whenValue, ignoreCase: true);
            }
            else
            {
                TransformHelpers.CheckTooManyParameters(transformValues, expected: 1);
            }

            context.AddResponseHeaderRemove(removeResponseHeaderName, condition);
        }
        else if (transformValues.TryGetValue(ResponseTrailerRemoveKey, out var removeResponseTrailerName))
        {
            var condition = ResponseCondition.Success;
            if (transformValues.TryGetValue(WhenKey, out var whenValue))
            {
                TransformHelpers.CheckTooManyParameters(transformValues, expected: 2);
                condition = Enum.Parse<ResponseCondition>(whenValue, ignoreCase: true);
            }
            else
            {
                TransformHelpers.CheckTooManyParameters(transformValues, expected: 1);
            }

            context.AddResponseTrailerRemove(removeResponseTrailerName, condition);
        }
        else if (transformValues.TryGetValue(ResponseHeadersAllowedKey, out var allowedHeaders))
        {
            TransformHelpers.CheckTooManyParameters(transformValues, expected: 1);
            var headersList = allowedHeaders.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            context.AddResponseHeadersAllowed(headersList);
        }
        else if (transformValues.TryGetValue(ResponseTrailersAllowedKey, out var allowedTrailers))
        {
            TransformHelpers.CheckTooManyParameters(transformValues, expected: 1);
            var headersList = allowedTrailers.Split(';', options: StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            context.AddResponseTrailersAllowed(headersList);
        }
        else
        {
            return false;
        }

        return true;
    }
}
