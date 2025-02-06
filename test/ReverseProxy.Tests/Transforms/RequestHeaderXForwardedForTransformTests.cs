// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Yarp.ReverseProxy.Transforms.Tests;

public class RequestHeaderXForwardedForTransformTests
{
    [Theory]
    // Using ";" to represent multi-line headers
    [InlineData("", "", ForwardedTransformActions.Set, "")]
    [InlineData("", "", ForwardedTransformActions.Append, "")]
    [InlineData("", "", ForwardedTransformActions.Remove, "")]
    [InlineData("", "::1", ForwardedTransformActions.Set, "::1")]
    [InlineData("", "127.0.0.1", ForwardedTransformActions.Set, "127.0.0.1")]
    [InlineData("", "::ffff:127.0.0.1", ForwardedTransformActions.Set, "127.0.0.1")]
    [InlineData("", "127.0.0.1", ForwardedTransformActions.Append, "127.0.0.1")]
    [InlineData("", "::ffff:127.0.0.1", ForwardedTransformActions.Append, "127.0.0.1")]
    [InlineData("", "127.0.0.1", ForwardedTransformActions.Remove, "")]
    [InlineData("existing,Header", "", ForwardedTransformActions.Set, "")]
    [InlineData("existing;Header", "", ForwardedTransformActions.Set, "")]
    [InlineData("existing,Header", "", ForwardedTransformActions.Append, "existing,Header")]
    [InlineData("existing;Header", "", ForwardedTransformActions.Append, "existing;Header")]
    [InlineData("existing;Header", "", ForwardedTransformActions.Remove, "")]
    [InlineData("existing,Header", "127.0.0.1", ForwardedTransformActions.Set, "127.0.0.1")]
    [InlineData("existing;Header", "127.0.0.1", ForwardedTransformActions.Set, "127.0.0.1")]
    [InlineData("existing,Header", "127.0.0.1", ForwardedTransformActions.Append, "existing,Header;127.0.0.1")]
    [InlineData("existing;Header", "127.0.0.1", ForwardedTransformActions.Append, "existing;Header;127.0.0.1")]
    [InlineData("existing;Header", "127.0.0.1", ForwardedTransformActions.Remove, "")]
    public async Task RemoteIp_Added(string startValue, string remoteIp, ForwardedTransformActions action, string expected)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = string.IsNullOrEmpty(remoteIp) ? null : IPAddress.Parse(remoteIp);
        var proxyRequest = new HttpRequestMessage();
        proxyRequest.Headers.Add("name", startValue.Split(";", StringSplitOptions.RemoveEmptyEntries));
        var transform = new RequestHeaderXForwardedForTransform("name", action);
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
