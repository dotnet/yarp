// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Xunit;
using Yarp.Tests.Common;

namespace Yarp.ReverseProxy.Transforms.Tests;

public class ResponseTrailerValueTransformTests
{
    [Theory]
    // Using ";" to represent multi-line headers
    [InlineData("", 400, "new", false, ResponseCondition.Success, "")]
    [InlineData("", 200, "new", false, ResponseCondition.Success, "new")]
    [InlineData("", 400, "new", false, ResponseCondition.Failure, "new")]
    [InlineData("", 200, "new", false, ResponseCondition.Failure, "")]
    [InlineData("", 400, "new", false, ResponseCondition.Always, "new")]
    [InlineData("", 200, "new", false, ResponseCondition.Always, "new")]
    [InlineData("start", 400, "new", false, ResponseCondition.Success, "start")]
    [InlineData("start", 200, "new", false, ResponseCondition.Success, "new")]
    [InlineData("start", 400, "new", false, ResponseCondition.Always, "new")]
    [InlineData("start", 200, "new", false, ResponseCondition.Always, "new")]
    [InlineData("start", 400, "new", true, ResponseCondition.Success, "start")]
    [InlineData("start", 200, "new", true, ResponseCondition.Success, "start;new")]
    [InlineData("start", 400, "new", true, ResponseCondition.Always, "start;new")]
    [InlineData("start", 200, "new", true, ResponseCondition.Always, "start;new")]
    [InlineData("start,value", 400, "new", true, ResponseCondition.Success, "start,value")]
    [InlineData("start,value", 200, "new", true, ResponseCondition.Success, "start,value;new")]
    [InlineData("start,value", 400, "new", true, ResponseCondition.Always, "start,value;new")]
    [InlineData("start,value", 200, "new", true, ResponseCondition.Always, "start,value;new")]
    [InlineData("start;value", 400, "new", true, ResponseCondition.Success, "start;value")]
    [InlineData("start;value", 200, "new", true, ResponseCondition.Success, "start;value;new")]
    [InlineData("start;value", 400, "new", true, ResponseCondition.Always, "start;value;new")]
    [InlineData("start;value", 200, "new", true, ResponseCondition.Always, "start;value;new")]
    public async Task AddResponseTrailer_Success(string startValue, int status, string value, bool append, ResponseCondition condition, string expected)
    {
        var httpContext = new DefaultHttpContext();
        var trailerFeature = new TestTrailersFeature();
        httpContext.Features.Set<IHttpResponseTrailersFeature>(trailerFeature);
        trailerFeature.Trailers["name"] = startValue.Split(";", System.StringSplitOptions.RemoveEmptyEntries);
        httpContext.Response.StatusCode = status;
        var transformContext = new ResponseTrailersTransformContext()
        {
            HttpContext = httpContext,
            ProxyResponse = new HttpResponseMessage(),
            HeadersCopied = true,
        };
        var transform = new ResponseTrailerValueTransform("name", value, append, condition);
        await transform.ApplyAsync(transformContext);
        Assert.Equal(expected.Split(";", System.StringSplitOptions.RemoveEmptyEntries), trailerFeature.Trailers["name"]);
    }
}
