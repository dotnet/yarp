// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace Yarp.ReverseProxy.Transforms.Tests;

public class QueryParameterTemplateTransformTests
{
    [Fact]
    public async Task Set_UsesRouteAndQueryValues()
    {
        var routeValues = new RouteValueDictionary
        {
            ["remainder"] = "7/8",
            ["plugin"] = "dark"
        };

        var context = CreateContext(routeValues, new QueryString("?size=small"));
        var transform = new QueryParameterTemplateTransform(QueryStringTransformMode.Set, "path", "img/{plugin}/{**remainder}/{size}");

        await transform.ApplyAsync(context);

        Assert.Equal("img/dark/7/8/small", context.Query.Collection["path"].ToString());
        Assert.Equal("small", context.Query.Collection["size"].ToString());
    }

    [Fact]
    public async Task Set_SkipsWhenTokenMissing()
    {
        var routeValues = new RouteValueDictionary
        {
            ["remainder"] = "7/8"
        };

        var context = CreateContext(routeValues, QueryString.Empty);
        var transform = new QueryParameterTemplateTransform(QueryStringTransformMode.Set, "path", "img/{missing}");

        await transform.ApplyAsync(context);

        Assert.False(context.Query.Collection.ContainsKey("path"));
        Assert.Equal(QueryString.Empty, context.Query.QueryString);
    }

    [Fact]
    public async Task Set_RouteValuesTakePrecedenceOverQuery()
    {
        var routeValues = new RouteValueDictionary
        {
            ["value"] = "fromRoute"
        };

        var context = CreateContext(routeValues, new QueryString("?value=fromQuery"));
        var transform = new QueryParameterTemplateTransform(QueryStringTransformMode.Set, "path", "{value}");

        await transform.ApplyAsync(context);

        Assert.Equal("fromRoute", context.Query.Collection["path"].ToString());
    }

    [Fact]
    public async Task Set_CreatesExpectedPathAndQueryFromTemplate()
    {
        const string originalPath = "/img/cache/classifieds/photo.jpg";

        var routeValues = new RouteValueDictionary
        {
            ["category"] = "cache",
            ["remainder"] = "classifieds/photo.jpg"
        };

        var context = CreateContext(routeValues, QueryString.Empty, new PathString(originalPath));
        var pathTransform = new PathStringTransform(PathStringTransform.PathTransformMode.Set, new PathString("/v1/weather/render"));
        var queryTransform = new QueryParameterTemplateTransform(QueryStringTransformMode.Set, "path", "img/{category}/{remainder}");

        await pathTransform.ApplyAsync(context);
        await queryTransform.ApplyAsync(context);

        Assert.Equal(new PathString("/v1/weather/render"), context.Path);
        Assert.Equal("img/cache/classifieds/photo.jpg", context.Query.Collection["path"].ToString());
    }

    private static RequestTransformContext CreateContext(RouteValueDictionary routeValues, QueryString queryString, PathString? path = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.RouteValues = routeValues;
        httpContext.Request.QueryString = queryString;

        return new RequestTransformContext
        {
            Path = path ?? httpContext.Request.Path,
            Query = new QueryTransformContext(httpContext.Request),
            HttpContext = httpContext
        };
    }
}
