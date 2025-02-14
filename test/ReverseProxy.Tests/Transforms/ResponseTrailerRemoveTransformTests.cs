// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Xunit;
using Yarp.Tests.Common;

namespace Yarp.ReverseProxy.Transforms.Tests;

public class ResponseTrailerRemoveTransformTests
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
    public async Task RemoveTrailerFromFeature_Success(string names, string values, int status, ResponseCondition condition, string removedHeader, string expected)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.StatusCode = status;
        var trailerFeature = new TestTrailersFeature();
        httpContext.Features.Set<IHttpResponseTrailersFeature>(trailerFeature);
        var proxyResponse = new HttpResponseMessage();
        foreach (var (name, subvalues) in TestResources.ParseNameAndValues(names, values))
        {
            trailerFeature.Trailers[name] = subvalues;
        }

        var transform = new ResponseTrailerRemoveTransform(removedHeader, condition);
        await transform.ApplyAsync(new ResponseTrailersTransformContext()
        {
            HttpContext = httpContext,
            ProxyResponse = proxyResponse,
            HeadersCopied = true,
        });

        var expectedHeaders = expected.Split("; ", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(expectedHeaders, trailerFeature.Trailers.Select(h => h.Key));
    }
}
