// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Security.Authentication;
using System.Text;
using Xunit;

namespace Yarp.ReverseProxy.Configuration.Tests;

public class HttpClientConfigTests
{
    [Fact]
    public void Equals_Same_Value_Returns_True()
    {
        var options1 = new HttpClientConfig
        {
            SslProtocols = SslProtocols.Tls11,
            DangerousAcceptAnyServerCertificate = false,
            MaxConnectionsPerServer = 20,
            WebProxy = new WebProxyConfig() { Address = new Uri("http://localhost:8080"), BypassOnLocal = true, UseDefaultCredentials = true },
            RequestHeaderEncoding = Encoding.UTF8.WebName,
            ResponseHeaderEncoding = Encoding.UTF8.WebName,
        };

        var options2 = new HttpClientConfig
        {
            SslProtocols = SslProtocols.Tls11,
            DangerousAcceptAnyServerCertificate = false,
            MaxConnectionsPerServer = 20,
            WebProxy = new WebProxyConfig() { Address = new Uri("http://localhost:8080"), BypassOnLocal = true, UseDefaultCredentials = true },
            RequestHeaderEncoding = Encoding.UTF8.WebName,
            ResponseHeaderEncoding = Encoding.UTF8.WebName,
        };

        var equals = options1.Equals(options2);

        Assert.True(equals);
        Assert.True(options1 == options2);
        Assert.False(options1 != options2);
        Assert.Equal(options1.GetHashCode(), options2.GetHashCode());
    }

    [Fact]
    public void Equals_Different_Value_Returns_False()
    {
        var options1 = new HttpClientConfig
        {
            SslProtocols = SslProtocols.Tls11,
            DangerousAcceptAnyServerCertificate = false,
            MaxConnectionsPerServer = 20,
            RequestHeaderEncoding = Encoding.UTF8.WebName,
        };

        var options2 = new HttpClientConfig
        {
            SslProtocols = SslProtocols.Tls12,
            DangerousAcceptAnyServerCertificate = true,
            MaxConnectionsPerServer = 20,
            RequestHeaderEncoding = Encoding.Latin1.WebName,
        };

        var equals = options1.Equals(options2);

        Assert.False(equals);
    }

    [Fact]
    public void Equals_Same_WebProxyAddress_Returns_True()
    {
        var options1 = new HttpClientConfig
        {
            WebProxy = new WebProxyConfig() { Address = new Uri("http://localhost:8080"), BypassOnLocal = true, UseDefaultCredentials = true }
        };

        var options2 = new HttpClientConfig
        {
            WebProxy = new WebProxyConfig() { Address = new Uri("http://localhost:8080"), BypassOnLocal = true, UseDefaultCredentials = true }
        };

        var equals = options1.Equals(options2);

        Assert.True(equals);
        Assert.True(options1 == options2);
        Assert.False(options1 != options2);
    }

    [Fact]
    public void Equals_Different_WebProxyAddress_Returns_False()
    {
        var options1 = new HttpClientConfig
        {
            WebProxy = new WebProxyConfig() { Address = new Uri("http://localhost:8080"), BypassOnLocal = true, UseDefaultCredentials = true }
        };

        var options2 = new HttpClientConfig
        {
            WebProxy = new WebProxyConfig() { Address = new Uri("http://localhost:9999"), BypassOnLocal = true, UseDefaultCredentials = true }
        };

        var equals = options1.Equals(options2);

        Assert.False(equals);
        Assert.True(options1 != options2);
        Assert.False(options1 == options2);
    }

    [Fact]
    public void Equals_Second_Null_Returns_False()
    {
        var options1 = new HttpClientConfig
        {
            SslProtocols = SslProtocols.Tls11,
            DangerousAcceptAnyServerCertificate = false,
            MaxConnectionsPerServer = 20
        };

        var equals = options1.Equals(null);

        Assert.False(equals);
    }
}
