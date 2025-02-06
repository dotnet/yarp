// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Yarp.ReverseProxy.Transforms.Tests;

public class ResponseHeaderValueTransformTests
{
    [Theory]
    // Using ";" to represent multi-line headers
    [InlineData("", 400, "new", false, ResponseCondition.Success, "", false)]
    [InlineData("", 502, "new", false, ResponseCondition.Success, "", true)]
    [InlineData("", 200, "new", false, ResponseCondition.Success, "new", false)]
    [InlineData("", 400, "new", false, ResponseCondition.Always, "new", false)]
    [InlineData("", 200, "new", false, ResponseCondition.Always, "new", false)]
    [InlineData("", 502, "new", false, ResponseCondition.Always, "new", true)]
    [InlineData("", 502, "new", false, ResponseCondition.Failure, "new", false)]
    [InlineData("", 502, "new", false, ResponseCondition.Failure, "new", true)]
    [InlineData("", 200, "new", false, ResponseCondition.Failure, "", false)]
    [InlineData("start", 400, "new", false, ResponseCondition.Success, "start", false)]
    [InlineData("start", 200, "new", false, ResponseCondition.Success, "new", false)]
    [InlineData("start", 502, "new", false, ResponseCondition.Success, "start", true)]
    [InlineData("start", 400, "new", false, ResponseCondition.Always, "new", false)]
    [InlineData("start", 200, "new", false, ResponseCondition.Always, "new", false)]
    [InlineData("start", 400, "new", true, ResponseCondition.Success, "start", false)]
    [InlineData("start", 200, "new", true, ResponseCondition.Success, "start;new", false)]
    [InlineData("start", 400, "new", true, ResponseCondition.Always, "start;new", false)]
    [InlineData("start", 200, "new", true, ResponseCondition.Always, "start;new", false)]
    [InlineData("start,value", 400, "new", true, ResponseCondition.Success, "start,value", false)]
    [InlineData("start,value", 200, "new", true, ResponseCondition.Success, "start,value;new", false)]
    [InlineData("start,value", 400, "new", true, ResponseCondition.Always, "start,value;new", false)]
    [InlineData("start,value", 200, "new", true, ResponseCondition.Always, "start,value;new", false)]
    [InlineData("start;value", 400, "new", true, ResponseCondition.Success, "start;value", false)]
    [InlineData("start;value", 200, "new", true, ResponseCondition.Success, "start;value;new", false)]
    [InlineData("start;value", 400, "new", true, ResponseCondition.Always, "start;value;new", false)]
    [InlineData("start;value", 200, "new", true, ResponseCondition.Always, "start;value;new", false)]
    public async Task AddResponseHeader_Success(string startValue, int status, string value, bool append, ResponseCondition condition, string expected, bool responseNull)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Headers["name"] = startValue.Split(";", System.StringSplitOptions.RemoveEmptyEntries);
        httpContext.Response.StatusCode = status;
        var transformContext = new ResponseTransformContext()
        {
            HttpContext = httpContext,
            ProxyResponse = responseNull ? null : new HttpResponseMessage(),
            HeadersCopied = true,
        };
        var transform = new ResponseHeaderValueTransform("name", value, append, condition);
        await transform.ApplyAsync(transformContext);
        Assert.Equal(expected.Split(";", System.StringSplitOptions.RemoveEmptyEntries), httpContext.Response.Headers["name"]);
    }
}
