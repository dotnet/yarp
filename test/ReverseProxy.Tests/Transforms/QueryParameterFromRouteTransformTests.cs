// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;
using Xunit;

namespace Yarp.ReverseProxy.Transforms.Tests;

public class QueryParameterFromRouteTransformTests
{
    [Theory]
    [InlineData("/{a}/{b}/{c}", "a", "?z=6")]
    [InlineData("/{a}/{b}/{c}", "c", "?z=8")]
    [InlineData("/{a}/{*remainder}", "remainder", "?z=7%2F8")]
    public async Task Append_AddsQueryParameterWithRouteValue(string pattern, string routeValueKey, string expected)
    {
        const string path = "/6/7/8";

        var routeValues = new RouteValueDictionary();
        var templateMatcher = new TemplateMatcher(TemplateParser.Parse(pattern), new RouteValueDictionary());
        templateMatcher.TryMatch(path, routeValues);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.RouteValues = routeValues;
        var context = new RequestTransformContext()
        {
            Path = path,
            Query = new QueryTransformContext(httpContext.Request),
            HttpContext = httpContext
        };
        var transform = new QueryParameterRouteTransform(QueryStringTransformMode.Append, "z", routeValueKey);
        await transform.ApplyAsync(context);
        Assert.Equal(expected, context.Query.QueryString.Value);
    }

    [Fact]
    public void Append_IgnoresExistingQueryParameter()
    {
        const string path = "/6/7/8";

        var routeValues = new RouteValueDictionary();
        var templateMatcher = new TemplateMatcher(TemplateParser.Parse("/{a}/{b}/{c}"), new RouteValueDictionary());
        templateMatcher.TryMatch(path, routeValues);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.RouteValues = routeValues;
        httpContext.Request.QueryString = new QueryString("?z=1");
        var context = new RequestTransformContext()
        {
            Path = path,
            Query = new QueryTransformContext(httpContext.Request),
            HttpContext = httpContext
        };
        var transform = new QueryParameterRouteTransform(QueryStringTransformMode.Append, "z", "a");
        transform.ApplyAsync(context);
        Assert.Equal("?z=1&z=6", context.Query.QueryString.Value);
    }

    [Fact]
    public void Set_OverwritesExistingQueryParameter()
    {
        const string path = "/6/7/8";

        var routeValues = new RouteValueDictionary();
        var templateMatcher = new TemplateMatcher(TemplateParser.Parse("/{a}/{b}/{c}"), new RouteValueDictionary());
        templateMatcher.TryMatch(path, routeValues);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.RouteValues = routeValues;
        httpContext.Request.QueryString = new QueryString("?z=1");
        var context = new RequestTransformContext()
        {
            Path = path,
            Query = new QueryTransformContext(httpContext.Request),
            HttpContext = httpContext
        };
        var transform = new QueryParameterRouteTransform(QueryStringTransformMode.Set, "z", "a");
        transform.ApplyAsync(context);
        Assert.Equal("?z=6", context.Query.QueryString.Value);
    }

    [Fact]
    public void Set_AddsNewQueryParameter()
    {
        const string path = "/6/7/8";

        var routeValues = new RouteValueDictionary();
        var templateMatcher = new TemplateMatcher(TemplateParser.Parse("/{a}/{b}/{c}"), new RouteValueDictionary());
        templateMatcher.TryMatch(path, routeValues);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.RouteValues = routeValues;
        var context = new RequestTransformContext()
        {
            Path = path,
            Query = new QueryTransformContext(httpContext.Request),
            HttpContext = httpContext
        };
        var transform = new QueryParameterRouteTransform(QueryStringTransformMode.Set, "z", "a");
        transform.ApplyAsync(context);
        Assert.Equal("?z=6", context.Query.QueryString.Value);
    }
}
