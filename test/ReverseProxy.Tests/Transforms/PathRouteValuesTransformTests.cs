// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Yarp.ReverseProxy.Transforms.Tests;

public class PathRouteValuesTransformTests
{
    [Theory]
    [InlineData("/{a}/{b}/{c}", "/6/7/8")]
    [InlineData("/{a}/foo/{b}/{c}/{d}", "/6/foo/7/8")] // Unknown value (d) dropped
    [InlineData("/{a}/foo/{b}", "/6/foo/7")] // Extra values (c) dropped
    public async Task ReplacesPatternWithRouteValues(string transformValue, string expected)
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddOptions();
        serviceCollection.AddRouting();
        using var services = serviceCollection.BuildServiceProvider();

        var routeValues = new Dictionary<string, object>
        {
            { "a", "6" },
            { "b", "7" },
            { "c", "8" },
        };

        var httpContext = new DefaultHttpContext();
        httpContext.Request.RouteValues = new RouteValueDictionary(routeValues);
        var context = new RequestTransformContext()
        {
            Path = "/",
            HttpContext = httpContext
        };
        var transform = new PathRouteValuesTransform(transformValue, services.GetRequiredService<TemplateBinderFactory>());
        await transform.ApplyAsync(context);
        Assert.Equal(expected, context.Path);

        // The transform should not modify the original request's route values
        Assert.Equal(routeValues, httpContext.Request.RouteValues);
    }

    [Fact]
    public async Task RouteValuesWithSlashesNotEncoded()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddOptions();
        serviceCollection.AddRouting();
        using var services = serviceCollection.BuildServiceProvider();

        var routeValues = new Dictionary<string, object>
        {
            { "a", "abc" },
            { "b", "def" },
            { "remainder", "klm/nop/qrs" },
        };

        var httpContext = new DefaultHttpContext();
        httpContext.Request.RouteValues = new RouteValueDictionary(routeValues);
        var context = new RequestTransformContext()
        {
            Path = "/",
            HttpContext = httpContext
        };
        var transform = new PathRouteValuesTransform("/{a}/{b}/{**remainder}", services.GetRequiredService<TemplateBinderFactory>());
        await transform.ApplyAsync(context);
        Assert.Equal("/abc/def/klm/nop/qrs", context.Path.Value);

        // The transform should not modify the original request's route values
        Assert.Equal(routeValues, httpContext.Request.RouteValues);
    }
}
