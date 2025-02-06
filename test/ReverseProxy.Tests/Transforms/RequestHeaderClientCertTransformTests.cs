// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Yarp.ReverseProxy.Transforms.Tests;

public class RequestHeaderClientCertTransformTests
{
    [Fact]
    public async Task NoCert_NoOp()
    {
        var httpContext = new DefaultHttpContext();
        var proxyRequest = new HttpRequestMessage();
        var transform = new RequestHeaderClientCertTransform("Name");
        await transform.ApplyAsync(new RequestTransformContext() { HttpContext = httpContext, ProxyRequest = proxyRequest });
        Assert.Empty(proxyRequest.Headers);
    }

    [Fact]
    public async Task Cert_Encoded()
    {
        var httpContext = new DefaultHttpContext();
        var proxyRequest = new HttpRequestMessage();
        httpContext.Connection.ClientCertificate = Certificates.SelfSignedValidWithClientEku;
        var transform = new RequestHeaderClientCertTransform("Name");
        await transform.ApplyAsync(new RequestTransformContext() { HttpContext = httpContext, ProxyRequest = proxyRequest });
        var expected = Convert.ToBase64String(Certificates.SelfSignedValidWithClientEku.RawData);
        Assert.Equal(expected, proxyRequest.Headers.GetValues("Name").Single());
    }

    [Fact]
    public async Task ExistingHeader_NoCert_RemovesHeader()
    {
        var httpContext = new DefaultHttpContext();
        var proxyRequest = new HttpRequestMessage();
        proxyRequest.Headers.Add("Name", "OtherValue");
        var transform = new RequestHeaderClientCertTransform("Name");
        await transform.ApplyAsync(new RequestTransformContext()
        {
            HttpContext = httpContext,
            ProxyRequest = proxyRequest,
            HeadersCopied = true
        });
        Assert.Empty(proxyRequest.Headers);
    }

    [Fact]
    public async Task ExistingHeader_Replaced()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.ClientCertificate = Certificates.SelfSignedValidWithClientEku;
        var proxyRequest = new HttpRequestMessage();
        proxyRequest.Headers.Add("Name", "OtherValue");
        var transform = new RequestHeaderClientCertTransform("Name");
        await transform.ApplyAsync(new RequestTransformContext()
        {
            HttpContext = httpContext,
            ProxyRequest = proxyRequest,
            HeadersCopied = true
        });
        var expected = Convert.ToBase64String(Certificates.SelfSignedValidWithClientEku.RawData);
        Assert.Equal(expected, proxyRequest.Headers.GetValues("Name").Single());
    }

    private static class Certificates
    {
        public static X509Certificate2 SelfSignedValidWithClientEku { get; private set; } =
            new X509Certificate2(GetFullyQualifiedFilePath("validSelfSignedClientEkuCertificate.cer"));

        private static string GetFullyQualifiedFilePath(string filename)
        {
            var filePath = Path.Combine(AppContext.BaseDirectory, filename);
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException(filePath);
            }
            return filePath;
        }
    }
}
