// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    internal class QueryStringParameterTransform : RequestParametersTransform
    {
        private readonly QueryStringTransformMode _mode;
        private readonly string _key;
        private readonly string _routeValueKey;

        public QueryStringParameterTransform(QueryStringTransformMode mode, string key, string routeValueKey)
        {
            _mode = mode;
            _key = key;
            _routeValueKey = routeValueKey;
        }

        public override void Apply(RequestParametersTransformContext context)
        {
            if (context == null)
            {
                throw new System.ArgumentNullException(nameof(context));
            }

            var routeValues = context.HttpContext.Request.RouteValues;
            if (routeValues.TryGetValue(_routeValueKey, out object value))
            {
                switch (_mode)
                {
                    case QueryStringTransformMode.Append:
                        context.Query = context.Query.Add(_key, value.ToString());
                        break;
                    default:
                        throw new NotImplementedException(_mode.ToString());
                }
            }
        }
    }

    public enum QueryStringTransformMode
    {
        Append,
    }
}
