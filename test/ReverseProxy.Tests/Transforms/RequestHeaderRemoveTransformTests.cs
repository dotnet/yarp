// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Xunit;
using Yarp.Tests.Common;

namespace Yarp.ReverseProxy.Transforms.Tests;

public class RequestHeaderRemoveTransformTests
{
    [Theory]
    [InlineData("header1", "value1", "header1", "")]
    [InlineData("header1", "value1", "headerX", "header1")]
    [InlineData("header1; header2; header3", "value1, value2, value3", "header2", "header1; header3")]
    [InlineData("header1; header2; header3", "value1, value2, value3", "headerX", "header1; header2; header3")]
    [InlineData("header1; header2; header2; header3", "value1, value2-1, value2-2, value3", "header2", "header1; header3")]
    public async Task RemoveHeader_Success(string names, string values, string removedHeader, string expected)
    {
        var httpContext = new DefaultHttpContext();
        var proxyRequest = new HttpRequestMessage();
        foreach (var pair in TestResources.ParseNameAndValues(names, values))
        {
            proxyRequest.Headers.Add(pair.Name, pair.Values);
        }

        var transform = new RequestHeaderRemoveTransform(removedHeader);
        await transform.ApplyAsync(new RequestTransformContext()
        {
            HttpContext = httpContext,
            ProxyRequest = proxyRequest,
            HeadersCopied = true,
        });

        var expectedHeaders = expected.Split("; ", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(expectedHeaders, proxyRequest.Headers.Select(h => h.Key));
    }
}
