// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Yarp.ReverseProxy.Transforms.Tests;

public class RequestHeaderXForwardedProtoTransformTests
{
    [Theory]
    // Using ";" to represent multi-line headers
    [InlineData("", "http", ForwardedTransformActions.Set, "http")]
    [InlineData("", "http", ForwardedTransformActions.Append, "http")]
    [InlineData("", "http", ForwardedTransformActions.Remove, "")]
    [InlineData("existing,Header", "http", ForwardedTransformActions.Set, "http")]
    [InlineData("existing;Header", "http", ForwardedTransformActions.Set, "http")]
    [InlineData("existing,Header", "http", ForwardedTransformActions.Append, "existing,Header;http")]
    [InlineData("existing;Header", "http", ForwardedTransformActions.Append, "existing;Header;http")]
    [InlineData("existing;Header", "http", ForwardedTransformActions.Remove, "")]
    public async Task Scheme_Added(string startValue, string scheme, ForwardedTransformActions action, string expected)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = scheme;
        var proxyRequest = new HttpRequestMessage();
        proxyRequest.Headers.Add("name", startValue.Split(";", StringSplitOptions.RemoveEmptyEntries));
        var transform = new RequestHeaderXForwardedProtoTransform("name", action);
        await transform.ApplyAsync(new RequestTransformContext()
        {
            HttpContext = httpContext,
            ProxyRequest = proxyRequest,
            HeadersCopied = true,
        });
        if (string.IsNullOrEmpty(expected))
        {
            Assert.False(proxyRequest.Headers.TryGetValues("name", out var _));
        }
        else
        {
            Assert.Equal(expected.Split(";", StringSplitOptions.RemoveEmptyEntries), proxyRequest.Headers.GetValues("name"));
        }
    }
}
