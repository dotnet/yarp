// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    public class QueryStringParameterTransformTests
    {
        [Theory]
        [InlineData("/{a}/{b}/{c}", "a", "?z=6")]
        [InlineData("/{a}/{b}/{c}", "c", "?z=8")]
        [InlineData("/{a}/{*remainder}", "remainder", "?z=7/8")]
        public void Append_Pattern_AddsQueryStringParameterWithRouteValue(string pattern, string routeValueKey, string expected)
        {
            const string path = "/6/7/8";

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddOptions();
            serviceCollection.AddRouting();
            using var services = serviceCollection.BuildServiceProvider();

            var routeValues = new AspNetCore.Routing.RouteValueDictionary();
            var templateMatcher = new TemplateMatcher(TemplateParser.Parse(pattern), new AspNetCore.Routing.RouteValueDictionary());
            templateMatcher.TryMatch(path, routeValues);

            var httpContext = new DefaultHttpContext();
            httpContext.Request.RouteValues = routeValues;
            var context = new RequestParametersTransformContext()
            {
                Path = path,
                HttpContext = httpContext
            };
            var transform = new QueryStringParameterTransform(QueryStringTransformMode.Append, "z", routeValueKey);
            transform.Apply(context);
            Assert.Equal(expected, context.Query.Value);
        }
    }
}
