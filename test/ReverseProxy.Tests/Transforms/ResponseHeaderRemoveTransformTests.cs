// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Xunit;
using Yarp.Tests.Common;

namespace Yarp.ReverseProxy.Transforms.Tests;

public class ResponseHeaderRemoveTransformTests
{
    [Theory]
    [InlineData("header1", "value1", 200, ResponseCondition.Success, "header1", "")]
    [InlineData("header1", "value1", 404, ResponseCondition.Success, "header1", "header1")]
    [InlineData("header1", "value1", 200, ResponseCondition.Failure, "header1", "header1")]
    [InlineData("header1", "value1", 404, ResponseCondition.Failure, "header1", "")]
    [InlineData("header1", "value1", 200, ResponseCondition.Always, "header1", "")]
    [InlineData("header1", "value1", 404, ResponseCondition.Always, "header1", "")]
    [InlineData("header1", "value1", 200, ResponseCondition.Success, "headerX", "header1")]
    [InlineData("header1", "value1", 404, ResponseCondition.Success, "headerX", "header1")]
    [InlineData("header1", "value1", 200, ResponseCondition.Always, "headerX", "header1")]
    [InlineData("header1", "value1", 404, ResponseCondition.Always, "headerX", "header1")]
    [InlineData("header1; header2; header3", "value1, value2, value3", 200, ResponseCondition.Success, "header2", "header1; header3")]
    [InlineData("header1; header2; header3", "value1, value2, value3", 404, ResponseCondition.Success, "header2", "header1; header2; header3")]
    [InlineData("header1; header2; header3", "value1, value2, value3", 200, ResponseCondition.Always, "header2", "header1; header3")]
    [InlineData("header1; header2; header3", "value1, value2, value3", 404, ResponseCondition.Always, "header2", "header1; header3")]
    [InlineData("header1; header2; header3", "value1, value2, value3", 200, ResponseCondition.Success, "headerX", "header1; header2; header3")]
    [InlineData("header1; header2; header3", "value1, value2, value3", 404, ResponseCondition.Success, "headerX", "header1; header2; header3")]
    [InlineData("header1; header2; header3", "value1, value2, value3", 200, ResponseCondition.Always, "headerX", "header1; header2; header3")]
    [InlineData("header1; header2; header3", "value1, value2, value3", 404, ResponseCondition.Always, "headerX", "header1; header2; header3")]
    [InlineData("header1; header2; header2; header3", "value1, value2-1, value2-2, value3", 200, ResponseCondition.Success, "header2", "header1; header3")]
    [InlineData("header1; header2; header2; header3", "value1, value2-1, value2-2, value3", 404, ResponseCondition.Success, "header2", "header1; header2; header3")]
    [InlineData("header1; header2; header2; header3", "value1, value2-1, value2-2, value3", 200, ResponseCondition.Always, "header2", "header1; header3")]
    [InlineData("header1; header2; header2; header3", "value1, value2-1, value2-2, value3", 404, ResponseCondition.Always, "header2", "header1; header3")]
    public async Task RemoveHeader_Success(string names, string values, int status, ResponseCondition condition, string removedHeader, string expected)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.StatusCode = status;
        var proxyResponse = new HttpResponseMessage();
        foreach (var (name, subvalues) in TestResources.ParseNameAndValues(names, values))
        {
            httpContext.Response.Headers[name] = subvalues;
        }

        var transform = new ResponseHeaderRemoveTransform(removedHeader, condition);
        await transform.ApplyAsync(new ResponseTransformContext()
        {
            HttpContext = httpContext,
            ProxyResponse = proxyResponse,
            HeadersCopied = true,
        });

        var expectedHeaders = expected.Split("; ", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(expectedHeaders, httpContext.Response.Headers.Select(h => h.Key));
    }

    [Theory]
    [InlineData(ResponseCondition.Always)]
    [InlineData(ResponseCondition.Success)]
    [InlineData(ResponseCondition.Failure)]
    public async Task RemoveHeader_ResponseNull_DoNothing(ResponseCondition condition)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.StatusCode = 502;

        var transform = new ResponseHeaderRemoveTransform("header1", condition);
        await transform.ApplyAsync(new ResponseTransformContext()
        {
            HttpContext = httpContext,
            ProxyResponse = null,
            HeadersCopied = false,
        });
    }
}
