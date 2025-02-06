// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Yarp.ReverseProxy.Transforms.Tests;

public class ResponseTransformTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TakeHeader_RemovesAndReturnsHttpResponseHeader(bool copiedHeaders)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Headers["name"] = "value0";
        var proxyResponse = new HttpResponseMessage();
        proxyResponse.Headers.Add("Name", "value1");
        proxyResponse.Content = new StringContent("hello world");
        proxyResponse.Content.Headers.Add("Name", "value2");
        var result = ResponseTransform.TakeHeader(new ResponseTransformContext()
        {
            HttpContext = httpContext,
            ProxyResponse = proxyResponse,
            HeadersCopied = copiedHeaders,
        }, "name");
        Assert.Equal("value0", result);
        Assert.False(httpContext.Response.Headers.TryGetValue("name", out var _));
        Assert.Equal(new[] { "value1" }, proxyResponse.Headers.GetValues("name"));
        Assert.Equal(new[] { "value2" }, proxyResponse.Content.Headers.GetValues("name"));
    }

    [Fact]
    public void TakeHeader_HeadersNotCopied_ReturnsHttpResponseMessageHeader()
    {
        var httpContext = new DefaultHttpContext();
        var proxyResponse = new HttpResponseMessage();
        proxyResponse.Headers.Add("Name", "value1");
        proxyResponse.Content = new StringContent("hello world");
        proxyResponse.Content.Headers.Add("Name", "value2");
        var result = ResponseTransform.TakeHeader(new ResponseTransformContext()
        {
            HttpContext = httpContext,
            ProxyResponse = proxyResponse,
            HeadersCopied = false,
        }, "name");
        Assert.Equal("value1", result);
    }

    [Fact]
    public void TakeHeader_HeadersNotCopied_ReturnsHttpContentHeader()
    {
        var httpContext = new DefaultHttpContext();
        var proxyResponse = new HttpResponseMessage();
        proxyResponse.Content = new StringContent("hello world");
        proxyResponse.Content.Headers.Add("Name", "value2");
        var result = ResponseTransform.TakeHeader(new ResponseTransformContext()
        {
            HttpContext = httpContext,
            ProxyResponse = proxyResponse,
            HeadersCopied = false,
        }, "name");
        Assert.Equal("value2", result);
    }

    [Fact]
    public void TakeHeader_HeadersCopied_ReturnsNothing()
    {
        var httpContext = new DefaultHttpContext();
        var proxyResponse = new HttpResponseMessage();
        proxyResponse.Headers.Add("Name", "value1");
        proxyResponse.Content = new StringContent("hello world");
        proxyResponse.Content.Headers.Add("Name", "value2");
        var result = ResponseTransform.TakeHeader(new ResponseTransformContext()
        {
            HttpContext = httpContext,
            ProxyResponse = proxyResponse,
            HeadersCopied = true,
        }, "name");
        Assert.Equal(StringValues.Empty, result);
    }

    [Fact]
    public void TakeHeader_ResponseNull_ReturnsNothing()
    {
        var httpContext = new DefaultHttpContext();
        var result = ResponseTransform.TakeHeader(new ResponseTransformContext()
        {
            HttpContext = httpContext,
            ProxyResponse = null,
            HeadersCopied = false,
        }, "name");
        Assert.Equal(StringValues.Empty, result);
    }
}
