// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Yarp.ReverseProxy.Transforms.Tests;

public class HttpMethodChangeTransformTests
{
    [Theory]
    [InlineData("PUT", "POST", "PUT", "POST")]
    [InlineData("PUT", "POST", "POST", "POST")]
    [InlineData("PUT", "POST", "GET", "GET")]
    public async Task HttpMethodChange_Works(string fromMethod, string toMethod, string requestMethod, string expected)
    {
        var httpContext = new DefaultHttpContext();
        var request = new HttpRequestMessage() { Method = new HttpMethod(requestMethod) };
        var context = new RequestTransformContext()
        {
            HttpContext = httpContext,
            ProxyRequest = request,
        };
        var transform = new HttpMethodChangeTransform(fromMethod, toMethod);
        await transform.ApplyAsync(context);
        Assert.Equal(expected, request.Method.Method);
    }
}
