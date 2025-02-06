// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Yarp.ReverseProxy.Transforms.Tests;

public class RequestHeaderXForwardedPrefixTransformTests
{
    [Theory]
    // Using ";" to represent multi-line headers
    [InlineData("", "", ForwardedTransformActions.Set, "")]
    [InlineData("", "", ForwardedTransformActions.Append, "")]
    [InlineData("", "", ForwardedTransformActions.Remove, "")]
    [InlineData("", "/", ForwardedTransformActions.Set, "/")]
    [InlineData("", "/", ForwardedTransformActions.Append, "/")]
    [InlineData("", "/base", ForwardedTransformActions.Set, "/base")]
    [InlineData("", "/base", ForwardedTransformActions.Append, "/base")]
    [InlineData("", "/base", ForwardedTransformActions.Remove, "")]
    [InlineData("", "/base/value", ForwardedTransformActions.Set, "/base/value")]
    [InlineData("", "/base/value", ForwardedTransformActions.Append, "/base/value")]
    [InlineData("", "/base本", ForwardedTransformActions.Set, "/base%E6%9C%AC")]
    [InlineData("existing,Header", "", ForwardedTransformActions.Set, "")]
    [InlineData("existing;Header", "", ForwardedTransformActions.Set, "")]
    [InlineData("existing,Header", "", ForwardedTransformActions.Append, "existing,Header")]
    [InlineData("existing;Header", "", ForwardedTransformActions.Append, "existing;Header")]
    [InlineData("existing;Header", "", ForwardedTransformActions.Remove, "")]
    [InlineData("existing,Header", "/base", ForwardedTransformActions.Set, "/base")]
    [InlineData("existing;Header", "/base", ForwardedTransformActions.Set, "/base")]
    [InlineData("existing,Header", "/base", ForwardedTransformActions.Append, "existing,Header;/base")]
    [InlineData("existing;Header", "/base", ForwardedTransformActions.Append, "existing;Header;/base")]
    [InlineData("existing;Header", "/base", ForwardedTransformActions.Remove, "")]
    public async Task PathBase_Added(string startValue, string pathBase, ForwardedTransformActions action, string expected)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.PathBase = string.IsNullOrEmpty(pathBase) ? new PathString() : new PathString(pathBase);
        var proxyRequest = new HttpRequestMessage();
        proxyRequest.Headers.Add("name", startValue.Split(";", StringSplitOptions.RemoveEmptyEntries));
        var transform = new RequestHeaderXForwardedPrefixTransform("name", action);
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
