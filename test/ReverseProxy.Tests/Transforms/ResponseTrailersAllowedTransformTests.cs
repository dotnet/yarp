// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Net.Http.Headers;
using Xunit;
using Yarp.Tests.Common;

namespace Yarp.ReverseProxy.Transforms.Tests;

public class ResponseTrailersAllowedTransformTests
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
        var trailerFeature = new TestTrailersFeature();
        httpContext.Features.Set<IHttpResponseTrailersFeature>(trailerFeature);
        var proxyResponse = new HttpResponseMessage();
        proxyResponse.TrailingHeaders.TryAddWithoutValidation("header1", "value1");
        proxyResponse.TrailingHeaders.TryAddWithoutValidation("header2", "value2");
        proxyResponse.TrailingHeaders.TryAddWithoutValidation("header3", "value3");
        proxyResponse.TrailingHeaders.TryAddWithoutValidation("header4", "value4");
        proxyResponse.TrailingHeaders.TryAddWithoutValidation("header5", "value5");

        var allowed = names.Split(';');
        var transform = new ResponseTrailersAllowedTransform(allowed);
        var transformContext = new ResponseTrailersTransformContext()
        {
            HttpContext = httpContext,
            ProxyResponse = proxyResponse,
            HeadersCopied = false,
        };
        await transform.ApplyAsync(transformContext);

        Assert.True(transformContext.HeadersCopied);

        Assert.Equal(expected, trailerFeature.Trailers.Count());
        foreach (var header in trailerFeature.Trailers)
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
        var trailerFeature = new TestTrailersFeature();
        httpContext.Features.Set<IHttpResponseTrailersFeature>(trailerFeature);
        var proxyResponse = new HttpResponseMessage();
        proxyResponse.TrailingHeaders.TryAddWithoutValidation(HeaderNames.Connection, "value1");
        proxyResponse.TrailingHeaders.TryAddWithoutValidation(HeaderNames.TransferEncoding, "value2");
        proxyResponse.TrailingHeaders.TryAddWithoutValidation(HeaderNames.KeepAlive, "value3");

        var allowed = names.Split(';');
        var transform = new ResponseTrailersAllowedTransform(allowed);
        var transformContext = new ResponseTrailersTransformContext()
        {
            HttpContext = httpContext,
            ProxyResponse = proxyResponse,
            HeadersCopied = false,
        };
        await transform.ApplyAsync(transformContext);

        Assert.True(transformContext.HeadersCopied);

        Assert.Equal(expected, trailerFeature.Trailers.Count());
        foreach (var header in trailerFeature.Trailers)
        {
            Assert.Contains(header.Key, allowed, StringComparer.OrdinalIgnoreCase);
        }
    }
}
