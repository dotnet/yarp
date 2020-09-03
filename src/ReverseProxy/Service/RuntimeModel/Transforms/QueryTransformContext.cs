// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    /// <summary>
    /// Transform state for use with <see cref="RequestParametersTransform"/>
    /// </summary>
    public class QueryTransformContext
    {
        private readonly HttpRequest _request;
        private readonly QueryString _originalQueryString;
        private Dictionary<string, StringValues> _modifiedQueryParameters;

        public QueryTransformContext(HttpRequest request)
        {
            _request = request ?? throw new ArgumentNullException(nameof(request));
            _originalQueryString = request.QueryString;
            _modifiedQueryParameters = null;
        }

        public QueryString QueryString
        {
            get
            {
                if (_modifiedQueryParameters == null)
                {
                    return _originalQueryString;
                }

#if NETCOREAPP3_1
                var queryBuilder = new QueryBuilder(_modifiedQueryParameters.Select(pair => new System.Collections.Generic.KeyValuePair<string, string>(pair.Key, pair.Value.ToString())));
#elif NETCOREAPP5_0
                var queryBuilder = new QueryBuilder(_modifiedQueryParameters);
#else
#error A target framework was added to the project and needs to be added to this condition.
#endif
                return queryBuilder.ToQueryString();
            }
        }

        public Dictionary<string, StringValues> ModifiedQueryParameters
        {
            get
            {
                if (_modifiedQueryParameters == null)
                {
                    _modifiedQueryParameters = QueryHelpers.ParseQuery(_originalQueryString.Value);
                }

                return _modifiedQueryParameters;
            }
        }
    }
}
