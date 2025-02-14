// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Yarp.ReverseProxy.Transforms.Tests;

public class QueryParameterRemoveTransformTests
{
    [Fact]
    public async Task RemovesExistingQueryParameter()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = new QueryString("?z=1");
        var context = new RequestTransformContext()
        {
            Query = new QueryTransformContext(httpContext.Request)
        };
        var transform = new QueryParameterRemoveTransform("z");
        await transform.ApplyAsync(context);
        Assert.False(context.Query.QueryString.HasValue);
    }

    [Fact]
    public async Task LeavesOtherQueryParameters()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = new QueryString("?z=1&a=2");
        var context = new RequestTransformContext()
        {
            Query = new QueryTransformContext(httpContext.Request),
        };
        var transform = new QueryParameterRemoveTransform("z");
        await transform.ApplyAsync(context);
        Assert.Equal("?a=2", context.Query.QueryString.Value);
    }

    [Fact]
    public async Task DoesNotFailOnNonExistingQueryParameter()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = new QueryString("?z=1");
        var context = new RequestTransformContext()
        {
            Query = new QueryTransformContext(httpContext.Request),
        };
        var transform = new QueryParameterRemoveTransform("a");
        await transform.ApplyAsync(context);
        Assert.Equal("?z=1", context.Query.QueryString.Value);
    }
}
