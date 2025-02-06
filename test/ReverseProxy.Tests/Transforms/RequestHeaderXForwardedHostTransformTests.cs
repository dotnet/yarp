// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Yarp.ReverseProxy.Transforms.Tests;

public class RequestHeaderXForwardedHostTransformTests
{
    [Theory]
    // Using ";" to represent multi-line headers
    [InlineData("", "", ForwardedTransformActions.Set, "")]
    [InlineData("", "", ForwardedTransformActions.Append, "")]
    [InlineData("", "", ForwardedTransformActions.Remove, "")]
    [InlineData("", "host", ForwardedTransformActions.Set, "host")]
    [InlineData("", "host:80", ForwardedTransformActions.Append, "host:80")]
    [InlineData("", "host:80", ForwardedTransformActions.Remove, "")]
    [InlineData("", "ho本st", ForwardedTransformActions.Set, "xn--host-6j1i")]
    [InlineData("", "::1", ForwardedTransformActions.Set, "::1")]
    [InlineData("", "[::1]:80", ForwardedTransformActions.Set, "[::1]:80")]
    [InlineData("existing,Header", "", ForwardedTransformActions.Set, "")]
    [InlineData("existing;Header", "", ForwardedTransformActions.Set, "")]
    [InlineData("existing,Header", "", ForwardedTransformActions.Append, "existing,Header")]
    [InlineData("existing;Header", "", ForwardedTransformActions.Append, "existing;Header")]
    [InlineData("existing;Header", "", ForwardedTransformActions.Remove, "")]
    [InlineData("existing,Header", "host", ForwardedTransformActions.Set, "host")]
    [InlineData("existing;Header", "host", ForwardedTransformActions.Set, "host")]
    [InlineData("existing,Header", "host:80", ForwardedTransformActions.Append, "existing,Header;host:80")]
    [InlineData("existing;Header", "host", ForwardedTransformActions.Append, "existing;Header;host")]
    [InlineData("existing;Header", "host", ForwardedTransformActions.Remove, "")]
    public async Task Host_Added(string startValue, string host, ForwardedTransformActions action, string expected)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Host = string.IsNullOrEmpty(host) ? new HostString() : new HostString(host);
        var proxyRequest = new HttpRequestMessage();
        proxyRequest.Headers.Add("name", startValue.Split(";", StringSplitOptions.RemoveEmptyEntries));
        var transform = new RequestHeaderXForwardedHostTransform("name", action);
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
