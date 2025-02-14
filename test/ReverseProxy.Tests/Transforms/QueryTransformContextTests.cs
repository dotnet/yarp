// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Http;
using Xunit;

namespace Yarp.ReverseProxy.Transforms.Tests;

public class QueryTransformContextTests
{
    [Fact]
    public void Collection_TryGetValue_CaseInsensitive()
    {
        var httpContext = new DefaultHttpContext { Request = { QueryString = new QueryString("?z=1") } };
        var queryTransformContext = new QueryTransformContext(httpContext.Request);
        queryTransformContext.Collection.TryGetValue("Z", out var result);
        Assert.Equal("1", result);
    }

    [Fact]
    public void Collection_RemoveKey_CaseInsensitive()
    {
        var httpContext = new DefaultHttpContext { Request = { QueryString = new QueryString("?z=1") } };
        var queryTransformContext = new QueryTransformContext(httpContext.Request);
        queryTransformContext.Collection.Remove("Z");
        Assert.False(queryTransformContext.QueryString.HasValue);
    }
}
