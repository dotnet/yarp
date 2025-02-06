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

public class ResponseHeadersAllowedTransformTests
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
        var proxyResponse = new HttpResponseMessage();
        proxyResponse.Headers.TryAddWithoutValidation("header1", "value1");
        proxyResponse.Headers.TryAddWithoutValidation("header2", "value2");
        proxyResponse.Headers.TryAddWithoutValidation("header3", "value3");
        proxyResponse.Headers.TryAddWithoutValidation("header4", "value4");
        proxyResponse.Headers.TryAddWithoutValidation("header5", "value5");

        var allowed = names.Split(';');
        var transform = new ResponseHeadersAllowedTransform(allowed);
        var transformContext = new ResponseTransformContext()
        {
            HttpContext = httpContext,
            ProxyResponse = proxyResponse,
            HeadersCopied = false,
        };
        await transform.ApplyAsync(transformContext);

        Assert.True(transformContext.HeadersCopied);

        Assert.Equal(expected, httpContext.Response.Headers.Count());
        foreach (var header in httpContext.Response.Headers)
        {
            Assert.Contains(header.Key, allowed, StringComparer.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("Allow", 1)]
    [InlineData("content-disposition;header0", 2)]
    [InlineData("content-length;content-Location;Content-Type", 3)]
    [InlineData("Allow;Content-Disposition;Content-Encoding;Content-Language;Content-Location;Content-MD5;Content-Range;Content-Type;Expires;Last-Modified;Content-Length", 11)]
    public async Task ContentHeaders_CopiedIfAllowed(string names, int expected)
    {
        var httpContext = new DefaultHttpContext();
        var proxyResponse = new HttpResponseMessage();
        proxyResponse.Content = new StringContent("");
        proxyResponse.Content.Headers.TryAddWithoutValidation("header0", "value0");
        proxyResponse.Content.Headers.TryAddWithoutValidation(HeaderNames.Allow, "value1");
        proxyResponse.Content.Headers.TryAddWithoutValidation(HeaderNames.ContentDisposition, "value2");
        proxyResponse.Content.Headers.TryAddWithoutValidation(HeaderNames.ContentEncoding, "value3");
        proxyResponse.Content.Headers.TryAddWithoutValidation(HeaderNames.ContentLanguage, "value4");
        proxyResponse.Content.Headers.TryAddWithoutValidation(HeaderNames.ContentLocation, "value5");
        proxyResponse.Content.Headers.TryAddWithoutValidation(HeaderNames.ContentMD5, "value6");
        proxyResponse.Content.Headers.TryAddWithoutValidation(HeaderNames.ContentRange, "value7");
        proxyResponse.Content.Headers.TryAddWithoutValidation(HeaderNames.ContentType, "value8");
        proxyResponse.Content.Headers.TryAddWithoutValidation(HeaderNames.Expires, "value9");
        proxyResponse.Content.Headers.TryAddWithoutValidation(HeaderNames.LastModified, "value10");
        proxyResponse.Content.Headers.TryAddWithoutValidation(HeaderNames.ContentLength, "0");

        var allowed = names.Split(';');
        var transform = new ResponseHeadersAllowedTransform(allowed);
        var transformContext = new ResponseTransformContext()
        {
            HttpContext = httpContext,
            ProxyResponse = proxyResponse,
            HeadersCopied = false,
        };
        await transform.ApplyAsync(transformContext);

        Assert.True(transformContext.HeadersCopied);

        Assert.Equal(expected, httpContext.Response.Headers.Count());
        foreach (var header in httpContext.Response.Headers)
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
        var proxyResponse = new HttpResponseMessage();
        proxyResponse.Headers.TryAddWithoutValidation(HeaderNames.Connection, "value1");
        proxyResponse.Headers.TryAddWithoutValidation(HeaderNames.TransferEncoding, "value2");
        proxyResponse.Headers.TryAddWithoutValidation(HeaderNames.KeepAlive, "value3");

        var allowed = names.Split(';');
        var transform = new ResponseHeadersAllowedTransform(allowed);
        var transformContext = new ResponseTransformContext()
        {
            HttpContext = httpContext,
            ProxyResponse = proxyResponse,
            HeadersCopied = false,
        };
        await transform.ApplyAsync(transformContext);

        Assert.True(transformContext.HeadersCopied);

        Assert.Equal(expected, httpContext.Response.Headers.Count());
        foreach (var header in httpContext.Response.Headers)
        {
            Assert.Contains(header.Key, allowed, StringComparer.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task ProxyResponseNull_DoNothing()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.StatusCode = 502;

        var transform = new ResponseHeadersAllowedTransform(new[] { "header1" });
        var transformContext = new ResponseTransformContext()
        {
            HttpContext = httpContext,
            ProxyResponse = null,
            HeadersCopied = false,
        };
        await transform.ApplyAsync(transformContext);
    }
}
