// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using Xunit;

namespace Yarp.ReverseProxy.Transforms.Tests;

public class RequestHeadersAllowedTransformTests
{
    [Theory]
    [InlineData("", 0)]
    [InlineData("header1", 1)]
    [InlineData("header1;header2", 2)]
    [InlineData("header1;header2;header3", 3)]
    [InlineData("header1;header2;header2;header3", 3)]
    public async Task AllowedHeaders_Copied(string names, int expected)
    {
        var httpContext = new DefaultHttpContext();
        var proxyRequest = new HttpRequestMessage();
        httpContext.Request.Headers["header1"] = "value1";
        httpContext.Request.Headers["header2"] = "value2";
        httpContext.Request.Headers["header3"] = "value3";
        httpContext.Request.Headers["header4"] = "value4";
        httpContext.Request.Headers["header5"] = "value5";
        httpContext.Request.Headers.ContentLength = 0;

        var allowed = names.Split(';');
        var transform = new RequestHeadersAllowedTransform(allowed);
        var transformContext = new RequestTransformContext()
        {
            HttpContext = httpContext,
            ProxyRequest = proxyRequest,
            HeadersCopied = false,
        };
        await transform.ApplyAsync(transformContext);

        Assert.True(transformContext.HeadersCopied);

        Assert.Equal(expected, proxyRequest.Headers.Count());
        foreach (var header in proxyRequest.Headers)
        {
            Assert.Contains(header.Key, allowed, StringComparer.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("Allow", 1)]
    [InlineData("content-disposition;header2", 1)]
    [InlineData("content-length;content-Location;Content-Type", 3)]
    [InlineData("Allow;Content-Disposition;Content-Encoding;Content-Language;Content-Location;Content-MD5;Content-Range;Content-Type;Expires;Last-Modified;Content-Length", 11)]
    public async Task ContentHeaders_CopiedIfAllowed(string names, int expected)
    {
        var httpContext = new DefaultHttpContext();
        var proxyRequest = new HttpRequestMessage();
        httpContext.Request.Headers[HeaderNames.Allow] = "value1";
        httpContext.Request.Headers[HeaderNames.ContentDisposition] = "value2";
        httpContext.Request.Headers[HeaderNames.ContentEncoding] = "value3";
        httpContext.Request.Headers[HeaderNames.ContentLanguage] = "value4";
        httpContext.Request.Headers[HeaderNames.ContentLocation] = "value5";
        httpContext.Request.Headers[HeaderNames.ContentMD5] = "value6";
        httpContext.Request.Headers[HeaderNames.ContentRange] = "value7";
        httpContext.Request.Headers[HeaderNames.ContentType] = "value8";
        httpContext.Request.Headers[HeaderNames.Expires] = "value9";
        httpContext.Request.Headers[HeaderNames.LastModified] = "value10";
        httpContext.Request.Headers.ContentLength = 0;

        var allowed = names.Split(';');
        var transform = new RequestHeadersAllowedTransform(allowed);
        var transformContext = new RequestTransformContext()
        {
            HttpContext = httpContext,
            ProxyRequest = proxyRequest,
            HeadersCopied = false,
        };
        await transform.ApplyAsync(transformContext);

        Assert.True(transformContext.HeadersCopied);

        Assert.Empty(proxyRequest.Headers);
        var content = proxyRequest.Content;
        if (expected == 0)
        {
            Assert.Null(content);
            return;
        }

        Assert.Equal(expected, content.Headers.Count());
        foreach (var header in content.Headers)
        {
            Assert.Contains(header.Key, allowed, StringComparer.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("connection", 1)]
    [InlineData("Transfer-Encoding;Keep-Alive", 2)]
    // See https://github.com/dotnet/yarp/blob/51d797986b1fea03500a1ad173d13a1176fb5552/src/ReverseProxy/Forwarder/RequestUtilities.cs#L61-L83
    public async Task RestrictedHeaders_CopiedIfAllowed(string names, int expected)
    {
        var httpContext = new DefaultHttpContext();
        var proxyRequest = new HttpRequestMessage();
        httpContext.Request.Headers[HeaderNames.Connection] = "value1";
        httpContext.Request.Headers[HeaderNames.TransferEncoding] = "value2";
        httpContext.Request.Headers[HeaderNames.KeepAlive] = "value3";

        var allowed = names.Split(';');
        var transform = new RequestHeadersAllowedTransform(allowed);
        var transformContext = new RequestTransformContext()
        {
            HttpContext = httpContext,
            ProxyRequest = proxyRequest,
            HeadersCopied = false,
        };
        await transform.ApplyAsync(transformContext);

        Assert.True(transformContext.HeadersCopied);

        Assert.Equal(expected, proxyRequest.Headers.Count());
        foreach (var header in proxyRequest.Headers)
        {
            Assert.Contains(header.Key, allowed, StringComparer.OrdinalIgnoreCase);
        }
    }
}
