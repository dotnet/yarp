// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using Xunit;
using Yarp.ReverseProxy.Forwarder;

namespace Yarp.ReverseProxy.Transforms.Tests;

public class RequestTransformTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TakeHeader_RemovesAndReturnsProxyRequestHeader(bool copiedHeaders)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["name"] = "value0";
        var proxyRequest = new HttpRequestMessage();
        proxyRequest.Headers.Add("Name", "value1");
        proxyRequest.Content = new StringContent("hello world");
        proxyRequest.Content.Headers.Add("Name", "value2");
        var result = RequestTransform.TakeHeader(new RequestTransformContext()
        {
            HttpContext = httpContext,
            ProxyRequest = proxyRequest,
            HeadersCopied = copiedHeaders,
        }, "name");
        Assert.Equal("value1", result);
        Assert.False(proxyRequest.Headers.TryGetValues("name", out var _));
        Assert.Equal(new[] { "value2" }, proxyRequest.Content.Headers.GetValues("name"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TakeHeaderFromContent_RemovesAndReturnsProxyContentHeader(bool copiedHeaders)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.ContentType = "value0";
        var proxyRequest = new HttpRequestMessage();
        proxyRequest.Content = new StringContent("hello world");
        var result = RequestTransform.TakeHeader(new RequestTransformContext()
        {
            HttpContext = httpContext,
            ProxyRequest = proxyRequest,
            HeadersCopied = copiedHeaders,
        }, HeaderNames.ContentType);
        Assert.Equal("text/plain; charset=utf-8", result);
        Assert.False(proxyRequest.Content.Headers.TryGetValues(HeaderNames.ContentType, out var _));
    }

    [Fact]
    public void TakeHeader_HeadersNotCopied_ReturnsHttpRequestHeader()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["name"] = "value0";
        var proxyRequest = new HttpRequestMessage();
        var result = RequestTransform.TakeHeader(new RequestTransformContext()
        {
            HttpContext = httpContext,
            ProxyRequest = proxyRequest,
            HeadersCopied = false,
        }, "name");
        Assert.Equal("value0", result);
    }

    [Fact]
    public void TakeHeader_HeadersCopied_ReturnsNothing()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["name"] = "value0";
        var proxyRequest = new HttpRequestMessage();
        var result = RequestTransform.TakeHeader(new RequestTransformContext()
        {
            HttpContext = httpContext,
            ProxyRequest = proxyRequest,
            HeadersCopied = true,
        }, "name");
        Assert.Equal(StringValues.Empty, result);
    }

    [Theory]
    [InlineData("header1", "header1", "")]
    [InlineData("header1", "headerX", "header1")]
    [InlineData("header1; header2; header3", "header2", "header1; header3")]
    [InlineData("header1", "Content-Encoding", "header1")]
    [InlineData("header1; Content-Encoding", "Content-Encoding", "header1")]
    [InlineData("header1; Content-Encoding", "header1", "Content-Encoding")]
    [InlineData("header1; Content-Encoding", "Content-Type", "header1; Content-Encoding")]
    [InlineData("header1; Content-Encoding", "headerX", "header1; Content-Encoding")]
    [InlineData("header1; Content-Encoding; Accept-Encoding", "header1", "Content-Encoding; Accept-Encoding")]
    [InlineData("header1; Content-Encoding; Accept-Encoding", "Content-Encoding", "header1; Accept-Encoding")]
    [InlineData("header1; Content-Encoding; Accept-Encoding", "Accept-Encoding", "header1; Content-Encoding")]
    [InlineData("header1; Content-Encoding; Accept-Encoding", "headerX", "header1; Content-Encoding; Accept-Encoding")]
    public void RemoveHeader_RemovesProxyRequestHeader(string names, string removedHeader, string expected)
    {
        var httpContext = new DefaultHttpContext();
        var proxyRequest = new HttpRequestMessage()
        {
            Content = new EmptyHttpContent()
        };

        foreach (var name in names.Split("; "))
        {
            httpContext.Request.Headers[name] = "value0";
            RequestUtilities.AddHeader(proxyRequest, name, "value1");
        }

        RequestTransform.RemoveHeader(new RequestTransformContext()
        {
            HttpContext = httpContext,
            ProxyRequest = proxyRequest
        }, removedHeader);

        foreach (var name in names.Split("; "))
        {
            Assert.True(httpContext.Request.Headers.TryGetValue(name, out var value));
            Assert.Equal("value0", value);
        }

        var expectedHeaders = expected.Split("; ", System.StringSplitOptions.RemoveEmptyEntries).OrderBy(h => h);
        var remainingHeaders = proxyRequest.Headers.Union(proxyRequest.Content.Headers).OrderBy(h => h.Key);
        Assert.Equal(expectedHeaders, remainingHeaders.Select(h => h.Key));
        Assert.All(remainingHeaders, h => Assert.Equal("value1", Assert.Single(h.Value)));
    }
}
