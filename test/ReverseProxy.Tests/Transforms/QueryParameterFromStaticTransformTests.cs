// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Yarp.ReverseProxy.Transforms.Tests;

public class QueryParameterFromStaticTransformTests
{
    [Fact]
    public async Task Append_AddsQueryStringParameterWithStaticValue()
    {
        var httpContext = new DefaultHttpContext();
        var context = new RequestTransformContext()
        {
            Query = new QueryTransformContext(httpContext.Request),
            HttpContext = httpContext
        };
        var transform = new QueryParameterFromStaticTransform(QueryStringTransformMode.Append, "z", "foo");
        await transform.ApplyAsync(context);
        Assert.Equal("?z=foo", context.Query.QueryString.Value);
    }

    [Fact]
    public async Task Append_IgnoresExistingQueryStringParameter()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = new QueryString("?z=1");
        var context = new RequestTransformContext()
        {
            Query = new QueryTransformContext(httpContext.Request),
            HttpContext = httpContext
        };
        var transform = new QueryParameterFromStaticTransform(QueryStringTransformMode.Append, "z", "foo");
        await transform.ApplyAsync(context);
        Assert.Equal("?z=1&z=foo", context.Query.QueryString.Value);
    }

    [Fact]
    public async Task Set_OverwritesExistingQueryStringParameter()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = new QueryString("?z=1");
        var context = new RequestTransformContext()
        {
            Query = new QueryTransformContext(httpContext.Request),
            HttpContext = httpContext
        };
        var transform = new QueryParameterFromStaticTransform(QueryStringTransformMode.Set, "z", "foo");
        await transform.ApplyAsync(context);
        Assert.Equal("?z=foo", context.Query.QueryString.Value);
    }

    [Fact]
    public async Task Set_AddsNewQueryStringParameter()
    {
        var httpContext = new DefaultHttpContext();
        var context = new RequestTransformContext()
        {
            Query = new QueryTransformContext(httpContext.Request),
            HttpContext = httpContext
        };
        var transform = new QueryParameterFromStaticTransform(QueryStringTransformMode.Set, "z", "foo");
        await transform.ApplyAsync(context);
        Assert.Equal("?z=foo", context.Query.QueryString.Value);
    }

    [Fact]
    public async Task Set_AddsNewEmptyQueryStringParameter()
    {
        var httpContext = new DefaultHttpContext();
        var context = new RequestTransformContext()
        {
            Query = new QueryTransformContext(httpContext.Request),
            HttpContext = httpContext
        };
        var transform = new QueryParameterFromStaticTransform(QueryStringTransformMode.Set, "z", "");
        await transform.ApplyAsync(context);
        Assert.Equal("?z=", context.Query.QueryString.Value);
    }

    [Fact]
    public async Task Set_OverwritesExistingParamWithEmptyValue()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = new QueryString("?z=foo");
        var context = new RequestTransformContext()
        {
            Query = new QueryTransformContext(httpContext.Request),
            HttpContext = httpContext
        };
        var transform = new QueryParameterFromStaticTransform(QueryStringTransformMode.Set, "z", "");
        await transform.ApplyAsync(context);
        Assert.Equal("?z=", context.Query.QueryString.Value);
    }

    [Fact]
    public async Task Set_AddNewEmptyParamToExistingQueryStringParameter()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = new QueryString("?x=1");
        var context = new RequestTransformContext()
        {
            Query = new QueryTransformContext(httpContext.Request),
            HttpContext = httpContext
        };
        var transform = new QueryParameterFromStaticTransform(QueryStringTransformMode.Set, "z", "");
        await transform.ApplyAsync(context);
        Assert.Equal("?x=1&z=", context.Query.QueryString.Value);
    }

    [Fact]
    public async Task Append_AddsNewEmptyQueryStringParameter()
    {
        var httpContext = new DefaultHttpContext();
        var context = new RequestTransformContext()
        {
            Query = new QueryTransformContext(httpContext.Request),
            HttpContext = httpContext
        };
        var transform = new QueryParameterFromStaticTransform(QueryStringTransformMode.Append, "z", "");
        await transform.ApplyAsync(context);
        Assert.Equal("?z=", context.Query.QueryString.Value);
    }

    [Fact]
    public async Task Append_AppendsEmptyValueToExistingParam()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = new QueryString("?z=foo");
        var context = new RequestTransformContext()
        {
            Query = new QueryTransformContext(httpContext.Request),
            HttpContext = httpContext
        };
        var transform = new QueryParameterFromStaticTransform(QueryStringTransformMode.Append, "z", "");
        await transform.ApplyAsync(context);
        Assert.Equal("?z=foo&z=", context.Query.QueryString.Value);
    }
}
