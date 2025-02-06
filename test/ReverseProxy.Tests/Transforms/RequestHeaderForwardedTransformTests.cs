// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Xunit;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Transforms.Tests;

public class RequestHeaderForwardedTransformTests
{
    [Theory]
    // Using "|" to represent multi-line headers
    [InlineData("", "https", ForwardedTransformActions.Set, "proto=https")]
    [InlineData("", "https", ForwardedTransformActions.Append, "proto=https")]
    [InlineData("", "https", ForwardedTransformActions.Remove, "")]
    [InlineData("existing,Header", "https", ForwardedTransformActions.Set, "proto=https")]
    [InlineData("existing|Header", "https", ForwardedTransformActions.Set, "proto=https")]
    [InlineData("existing,Header", "https", ForwardedTransformActions.Append, "existing,Header|proto=https")]
    [InlineData("existing|Header", "https", ForwardedTransformActions.Append, "existing|Header|proto=https")]
    [InlineData("existing,Header", "https", ForwardedTransformActions.Remove, "")]
    [InlineData("existing|Header", "https", ForwardedTransformActions.Remove, "")]
    public async Task Proto_Added(string startValue, string scheme, ForwardedTransformActions action, string expected)
    {
        var randomFactory = new TestRandomFactory();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = scheme;
        var proxyRequest = new HttpRequestMessage();
        proxyRequest.Headers.Add("Forwarded", startValue.Split("|", StringSplitOptions.RemoveEmptyEntries));
        var transform = new RequestHeaderForwardedTransform(randomFactory, forFormat: NodeFormat.None,
            byFormat: NodeFormat.None, host: false, proto: true, action);
        await transform.ApplyAsync(new RequestTransformContext()
        {
            HttpContext = httpContext,
            ProxyRequest = proxyRequest,
            HeadersCopied = true,
        });
        if (string.IsNullOrEmpty(expected))
        {
            Assert.False(proxyRequest.Headers.TryGetValues("Forwarded", out _));
        }
        else
        {
            Assert.Equal(expected.Split("|", StringSplitOptions.RemoveEmptyEntries), proxyRequest.Headers.GetValues("Forwarded"));
        }
    }

    [Theory]
    // Using "|" to represent multi-line headers
    [InlineData("", "myHost", ForwardedTransformActions.Set, "host=\"myHost\"")]
    [InlineData("", "myHost", ForwardedTransformActions.Append, "host=\"myHost\"")]
    [InlineData("", "ho本st", ForwardedTransformActions.Set, "host=\"xn--host-6j1i\"")]
    [InlineData("", "myHost:80", ForwardedTransformActions.Set, "host=\"myHost:80\"")]
    [InlineData("", "ho本st:80", ForwardedTransformActions.Set, "host=\"xn--host-6j1i:80\"")]
    [InlineData("existing,Header", "myHost", ForwardedTransformActions.Set, "host=\"myHost\"")]
    [InlineData("existing|Header", "myHost", ForwardedTransformActions.Set, "host=\"myHost\"")]
    [InlineData("existing|Header", "myHost:80", ForwardedTransformActions.Set, "host=\"myHost:80\"")]
    [InlineData("existing,Header", "myHost", ForwardedTransformActions.Append, "existing,Header|host=\"myHost\"")]
    [InlineData("existing|Header", "myHost", ForwardedTransformActions.Append, "existing|Header|host=\"myHost\"")]
    [InlineData("existing|Header", "myHost:80", ForwardedTransformActions.Append, "existing|Header|host=\"myHost:80\"")]
    public async Task Host_Added(string startValue, string host, ForwardedTransformActions action, string expected)
    {
        var randomFactory = new TestRandomFactory();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Host = new HostString(host);
        var proxyRequest = new HttpRequestMessage();
        proxyRequest.Headers.Add("Forwarded", startValue.Split("|", StringSplitOptions.RemoveEmptyEntries));
        var transform = new RequestHeaderForwardedTransform(randomFactory, forFormat: NodeFormat.None,
            byFormat: NodeFormat.None, host: true, proto: false, action);
        await transform.ApplyAsync(new RequestTransformContext()
        {
            HttpContext = httpContext,
            ProxyRequest = proxyRequest,
            HeadersCopied = true,
        });
        Assert.Equal(expected.Split("|", StringSplitOptions.RemoveEmptyEntries), proxyRequest.Headers.GetValues("Forwarded"));
    }

    [Theory]
    // Using "|" to represent multi-line headers
    [InlineData("", "", 2, NodeFormat.Ip, ForwardedTransformActions.Set, "for=unknown")] // Missing IP falls back to Unknown
    [InlineData("", "", 0, NodeFormat.IpAndPort, ForwardedTransformActions.Append, "for=unknown")] // Missing port excluded
    [InlineData("", "", 2, NodeFormat.IpAndPort, ForwardedTransformActions.Append, "for=\"unknown:2\"")]
    [InlineData("", "::1", 2, NodeFormat.Unknown, ForwardedTransformActions.Set, "for=unknown")]
    [InlineData("", "::1", 2, NodeFormat.UnknownAndPort, ForwardedTransformActions.Append, "for=\"unknown:2\"")]
    [InlineData("", "::1", 2, NodeFormat.UnknownAndRandomPort, ForwardedTransformActions.Append, "for=\"unknown:_abcdefghi\"")]
    [InlineData("", "::1", 2, NodeFormat.Ip, ForwardedTransformActions.Set, "for=\"[::1]\"")]
    [InlineData("", "::1", 0, NodeFormat.IpAndPort, ForwardedTransformActions.Append, "for=\"[::1]\"")]
    [InlineData("", "::1", 2, NodeFormat.IpAndPort, ForwardedTransformActions.Append, "for=\"[::1]:2\"")]
    [InlineData("", "::1", 2, NodeFormat.IpAndRandomPort, ForwardedTransformActions.Append, "for=\"[::1]:_abcdefghi\"")]
    [InlineData("", "127.0.0.1", 2, NodeFormat.Ip, ForwardedTransformActions.Set, "for=127.0.0.1")]
    [InlineData("", "127.0.0.1", 2, NodeFormat.IpAndPort, ForwardedTransformActions.Append, "for=\"127.0.0.1:2\"")]
    [InlineData("", "127.0.0.1", 2, NodeFormat.IpAndRandomPort, ForwardedTransformActions.Append, "for=\"127.0.0.1:_abcdefghi\"")]
    [InlineData("", "::ffff:127.0.0.1", 2, NodeFormat.Ip, ForwardedTransformActions.Set, "for=127.0.0.1")]
    [InlineData("", "::ffff:127.0.0.1", 2, NodeFormat.IpAndPort, ForwardedTransformActions.Append, "for=\"127.0.0.1:2\"")]
    [InlineData("", "::ffff:127.0.0.1", 2, NodeFormat.IpAndRandomPort, ForwardedTransformActions.Append, "for=\"127.0.0.1:_abcdefghi\"")]
    [InlineData("", "::1", 2, NodeFormat.Random, ForwardedTransformActions.Set, "for=_abcdefghi")]
    [InlineData("", "::1", 2, NodeFormat.RandomAndPort, ForwardedTransformActions.Append, "for=\"_abcdefghi:2\"")]
    [InlineData("", "::1", 2, NodeFormat.RandomAndRandomPort, ForwardedTransformActions.Append, "for=\"_abcdefghi:_jklmnopqr\"")]
    [InlineData("existing,header", "::1", 2, NodeFormat.Random, ForwardedTransformActions.Set, "for=_abcdefghi")]
    [InlineData("existing,header", "::1", 2, NodeFormat.RandomAndPort, ForwardedTransformActions.Append, "existing,header|for=\"_abcdefghi:2\"")]
    [InlineData("existing|header", "::1", 2, NodeFormat.RandomAndPort, ForwardedTransformActions.Append, "existing|header|for=\"_abcdefghi:2\"")]
    [InlineData("existing,header", "::1", 2, NodeFormat.RandomAndRandomPort, ForwardedTransformActions.Append, "existing,header|for=\"_abcdefghi:_jklmnopqr\"")]
    [InlineData("existing|header", "::1", 2, NodeFormat.RandomAndRandomPort, ForwardedTransformActions.Append, "existing|header|for=\"_abcdefghi:_jklmnopqr\"")]
    public async Task For_Added(string startValue, string ip, int port, NodeFormat format, ForwardedTransformActions action, string expected)
    {
        var randomFactory = new TestRandomFactory();
        randomFactory.Instance = new TestRandom() { Sequence = new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17 } };
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = string.IsNullOrEmpty(ip) ? null : IPAddress.Parse(ip);
        httpContext.Connection.RemotePort = port;
        var proxyRequest = new HttpRequestMessage();
        proxyRequest.Headers.Add("Forwarded", startValue.Split("|", StringSplitOptions.RemoveEmptyEntries));
        var transform = new RequestHeaderForwardedTransform(randomFactory, forFormat: format,
            byFormat: NodeFormat.None, host: false, proto: false, action);
        await transform.ApplyAsync(new RequestTransformContext()
        {
            HttpContext = httpContext,
            ProxyRequest = proxyRequest,
            HeadersCopied = true,
        });
        Assert.Equal(expected.Split("|", StringSplitOptions.RemoveEmptyEntries), proxyRequest.Headers.GetValues("Forwarded"));
    }

    [Theory]
    // Using "|" to represent multi-line headers
    [InlineData("", "", 2, NodeFormat.Ip, ForwardedTransformActions.Set, "by=unknown")] // Missing IP falls back to Unknown
    [InlineData("", "", 0, NodeFormat.IpAndPort, ForwardedTransformActions.Append, "by=unknown")] // Missing port excluded
    [InlineData("", "", 2, NodeFormat.IpAndPort, ForwardedTransformActions.Append, "by=\"unknown:2\"")]
    [InlineData("", "", 2, NodeFormat.IpAndRandomPort, ForwardedTransformActions.Append, "by=\"unknown:_abcdefghi\"")]
    [InlineData("", "::1", 2, NodeFormat.Unknown, ForwardedTransformActions.Set, "by=unknown")]
    [InlineData("", "::1", 2, NodeFormat.UnknownAndPort, ForwardedTransformActions.Append, "by=\"unknown:2\"")]
    [InlineData("", "::1", 2, NodeFormat.UnknownAndRandomPort, ForwardedTransformActions.Append, "by=\"unknown:_abcdefghi\"")]
    [InlineData("", "::1", 2, NodeFormat.Ip, ForwardedTransformActions.Set, "by=\"[::1]\"")]
    [InlineData("", "::1", 0, NodeFormat.IpAndPort, ForwardedTransformActions.Append, "by=\"[::1]\"")]
    [InlineData("", "::1", 2, NodeFormat.IpAndPort, ForwardedTransformActions.Append, "by=\"[::1]:2\"")]
    [InlineData("", "::1", 2, NodeFormat.IpAndRandomPort, ForwardedTransformActions.Append, "by=\"[::1]:_abcdefghi\"")]
    [InlineData("", "127.0.0.1", 2, NodeFormat.Ip, ForwardedTransformActions.Set, "by=127.0.0.1")]
    [InlineData("", "127.0.0.1", 2, NodeFormat.IpAndPort, ForwardedTransformActions.Append, "by=\"127.0.0.1:2\"")]
    [InlineData("", "127.0.0.1", 2, NodeFormat.IpAndRandomPort, ForwardedTransformActions.Append, "by=\"127.0.0.1:_abcdefghi\"")]
    [InlineData("", "::ffff:127.0.0.1", 2, NodeFormat.Ip, ForwardedTransformActions.Set, "by=127.0.0.1")]
    [InlineData("", "::ffff:127.0.0.1", 2, NodeFormat.IpAndPort, ForwardedTransformActions.Append, "by=\"127.0.0.1:2\"")]
    [InlineData("", "::ffff:127.0.0.1", 2, NodeFormat.IpAndRandomPort, ForwardedTransformActions.Append, "by=\"127.0.0.1:_abcdefghi\"")]
    [InlineData("", "::1", 2, NodeFormat.Random, ForwardedTransformActions.Set, "by=_abcdefghi")]
    [InlineData("", "::1", 2, NodeFormat.RandomAndPort, ForwardedTransformActions.Append, "by=\"_abcdefghi:2\"")]
    [InlineData("", "::1", 2, NodeFormat.RandomAndRandomPort, ForwardedTransformActions.Append, "by=\"_abcdefghi:_jklmnopqr\"")]
    [InlineData("existing,header", "::1", 2, NodeFormat.Random, ForwardedTransformActions.Set, "by=_abcdefghi")]
    [InlineData("existing,header", "::1", 2, NodeFormat.RandomAndPort, ForwardedTransformActions.Append, "existing,header|by=\"_abcdefghi:2\"")]
    [InlineData("existing|header", "::1", 2, NodeFormat.RandomAndPort, ForwardedTransformActions.Append, "existing|header|by=\"_abcdefghi:2\"")]
    [InlineData("existing,header", "::1", 2, NodeFormat.RandomAndRandomPort, ForwardedTransformActions.Append, "existing,header|by=\"_abcdefghi:_jklmnopqr\"")]
    [InlineData("existing|header", "::1", 2, NodeFormat.RandomAndRandomPort, ForwardedTransformActions.Append, "existing|header|by=\"_abcdefghi:_jklmnopqr\"")]
    public async Task By_Added(string startValue, string ip, int port, NodeFormat format, ForwardedTransformActions action, string expected)
    {
        var randomFactory = new TestRandomFactory();
        randomFactory.Instance = new TestRandom() { Sequence = new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17 } };
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.LocalIpAddress = string.IsNullOrEmpty(ip) ? null : IPAddress.Parse(ip);
        httpContext.Connection.LocalPort = port;
        var proxyRequest = new HttpRequestMessage();
        proxyRequest.Headers.Add("Forwarded", startValue.Split("|", StringSplitOptions.RemoveEmptyEntries));
        var transform = new RequestHeaderForwardedTransform(randomFactory, forFormat: NodeFormat.None,
            byFormat: format, host: false, proto: false, action);
        await transform.ApplyAsync(new RequestTransformContext()
        {
            HttpContext = httpContext,
            ProxyRequest = proxyRequest,
            HeadersCopied = true,
        });
        Assert.Equal(expected.Split("|", StringSplitOptions.RemoveEmptyEntries), proxyRequest.Headers.GetValues("Forwarded"));
    }

    [Theory]
    // Using "|" to represent multi-line headers
    [InlineData("", ForwardedTransformActions.Set, "proto=https;host=\"myHost:80\";for=\"[::1]:10\";by=_abcdefghi")]
    [InlineData("", ForwardedTransformActions.Append, "proto=https;host=\"myHost:80\";for=\"[::1]:10\";by=_abcdefghi")]
    [InlineData("otherHeader", ForwardedTransformActions.Set, "proto=https;host=\"myHost:80\";for=\"[::1]:10\";by=_abcdefghi")]
    [InlineData("otherHeader", ForwardedTransformActions.Append, "otherHeader|proto=https;host=\"myHost:80\";for=\"[::1]:10\";by=_abcdefghi")]
    public async Task AllValues_Added(string startValue, ForwardedTransformActions action, string expected)
    {
        var randomFactory = new TestRandomFactory();
        randomFactory.Instance = new TestRandom() { Sequence = new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17 } };
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("myHost", 80);
        httpContext.Connection.RemoteIpAddress = IPAddress.IPv6Loopback;
        httpContext.Connection.RemotePort = 10;
        var proxyRequest = new HttpRequestMessage();
        proxyRequest.Headers.Add("Forwarded", startValue.Split("|", StringSplitOptions.RemoveEmptyEntries));
        var transform = new RequestHeaderForwardedTransform(randomFactory,
            forFormat: NodeFormat.IpAndPort,
            byFormat: NodeFormat.Random,
            host: true, proto: true, action);
        await transform.ApplyAsync(new RequestTransformContext()
        {
            HttpContext = httpContext,
            ProxyRequest = proxyRequest,
            HeadersCopied = true,
        });
        Assert.Equal(expected.Split("|", StringSplitOptions.RemoveEmptyEntries), proxyRequest.Headers.GetValues("Forwarded"));
    }

    internal class TestRandomFactory : IRandomFactory
    {
        internal TestRandom Instance { get; set; }

        public Random CreateRandomInstance()
        {
            return Instance;
        }
    }

    public class TestRandom : Random
    {
        public int[] Sequence { get; set; }
        public int Offset { get; set; }

        public override int Next(int maxValue)
        {
            return Sequence[Offset++];
        }
    }
}
