// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Net.Http.Headers;
using Xunit;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;
using Yarp.ReverseProxy.Transforms.Builder.Tests;
using Yarp.Tests.Common;

namespace Yarp.ReverseProxy.Forwarder.Tests;

public class HttpTransformerTests
{
    private static readonly string[] RestrictedHeaders = new[]
    {
        HeaderNames.Connection,
        HeaderNames.TransferEncoding,
        HeaderNames.KeepAlive,
        HeaderNames.Upgrade,
        HeaderNames.ProxyConnection,
        HeaderNames.ProxyAuthenticate,
        "Proxy-Authentication-Info",
        HeaderNames.ProxyAuthorization,
        "Proxy-Features",
        "Proxy-Instruction",
        "Security-Scheme",
        "ALPN",
        "Close",
        "HTTP2-Settings",
        HeaderNames.UpgradeInsecureRequests,
        HeaderNames.TE,
        HeaderNames.AltSvc,
    };

    [Fact]
    public async Task TransformRequestAsync_RemovesRestrictedHeaders()
    {
        var transformer = HttpTransformer.Default;
        var httpContext = new DefaultHttpContext();
        var proxyRequest = new HttpRequestMessage(HttpMethod.Get, "https://localhost");

        foreach (var header in RestrictedHeaders)
        {
            httpContext.Request.Headers[header] = "value";
        }

        await transformer.TransformRequestAsync(httpContext, proxyRequest, "prefix", CancellationToken.None);

        foreach (var header in RestrictedHeaders)
        {
            Assert.False(proxyRequest.Headers.Contains(header));
        }

        Assert.Null(proxyRequest.Content);
    }

    [Fact]
    public async Task TransformRequestAsync_KeepOriginalHost()
    {
        var transformer = HttpTransformer.Empty;
        var httpContext = new DefaultHttpContext();
        var proxyRequest = new HttpRequestMessage(HttpMethod.Get, "https://localhost");

        httpContext.Request.Host = new HostString("example.com:3456");

        await transformer.TransformRequestAsync(httpContext, proxyRequest, "prefix", CancellationToken.None);

        Assert.Equal("example.com:3456", proxyRequest.Headers.Host);
    }

    [Fact]
    public async Task TransformRequestAsync_TETrailers_Copied()
    {
        var transformer = HttpTransformer.Default;
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Protocol = "HTTP/2";
        var proxyRequest = new HttpRequestMessage(HttpMethod.Get, "https://localhost");

        httpContext.Request.Headers[HeaderNames.TE] = "traiLers";

        await transformer.TransformRequestAsync(httpContext, proxyRequest, "prefix", CancellationToken.None);

        Assert.True(proxyRequest.Headers.TryGetValues(HeaderNames.TE, out var values));
        var value = Assert.Single(values);
        Assert.Equal("traiLers", value);

        Assert.Null(proxyRequest.Content);
    }

    [Fact]
    public async Task TransformRequestAsync_ContentLengthAndTransferEncoding_ContentLengthRemoved()
    {
        var transformer = HttpTransformer.Default;
        var httpContext = new DefaultHttpContext();
        var proxyRequest = new HttpRequestMessage(HttpMethod.Get, "https://localhost")
        {
            Content = new ByteArrayContent(Array.Empty<byte>())
        };

        httpContext.Request.Headers[HeaderNames.TransferEncoding] = "chUnked";
        httpContext.Request.Headers[HeaderNames.ContentLength] = "10";

        await transformer.TransformRequestAsync(httpContext, proxyRequest, "prefix", CancellationToken.None);

        Assert.False(proxyRequest.Content.Headers.TryGetValues(HeaderNames.ContentLength, out var _));
        // Transfer-Encoding is on the restricted list and removed. HttpClient will re-add it if required.
        Assert.False(proxyRequest.Headers.TryGetValues(HeaderNames.TransferEncoding, out var _));
    }

    [Fact]
    public async Task TransformRequestAsync_SetDestinationPrefix()
    {
        const string updatedDestinationPrefix = "https://contoso.com";
        var transformer = TransformBuilderTests.CreateTransformBuilder().CreateInternal(context =>
        {
            context.AddRequestTransform(transformContext =>
            {
                transformContext.DestinationPrefix = updatedDestinationPrefix;
                return ValueTask.CompletedTask;
            });
        });
        var httpContext = new DefaultHttpContext();
        var proxyRequest = new HttpRequestMessage(HttpMethod.Get, requestUri: (string)null)
        {
            Content = new ByteArrayContent(Array.Empty<byte>()),
        };

        await transformer.TransformRequestAsync(httpContext, proxyRequest, "https://localhost", CancellationToken.None);
        Assert.Equal(new Uri(updatedDestinationPrefix), proxyRequest.RequestUri);
    }

    [Theory]
    [InlineData(HttpStatusCode.Continue)]
    [InlineData(HttpStatusCode.SwitchingProtocols)]
    [InlineData(HttpStatusCode.NoContent)]
    [InlineData(HttpStatusCode.ResetContent)]
    public async Task TransformResponseAsync_ContentLength0OnBodylessStatusCode_ContentLengthRemoved(HttpStatusCode statusCode)
    {
        var transformer = HttpTransformer.Default;
        var httpContext = new DefaultHttpContext();

        var proxyResponse = new HttpResponseMessage(statusCode)
        {
            Content = new ByteArrayContent(Array.Empty<byte>())
        };

        Assert.Equal(0, proxyResponse.Content.Headers.ContentLength);
        await transformer.TransformResponseAsync(httpContext, proxyResponse, CancellationToken.None);
        Assert.False(httpContext.Response.Headers.ContainsKey(HeaderNames.ContentLength));
    }

    [Fact]
    public async Task TransformResponseAsync_RemovesRestrictedHeaders()
    {
        var transformer = HttpTransformer.Default;
        var httpContext = new DefaultHttpContext();
        var proxyResponse = new HttpResponseMessage()
        {
            Content = new ByteArrayContent(Array.Empty<byte>())
        };

        foreach (var header in RestrictedHeaders)
        {
            if (!proxyResponse.Headers.TryAddWithoutValidation(header, "value"))
            {
                Assert.True(proxyResponse.Content.Headers.TryAddWithoutValidation(header, "value"));
            }
        }

        await transformer.TransformResponseAsync(httpContext, proxyResponse, CancellationToken.None);

        foreach (var header in RestrictedHeaders)
        {
            Assert.False(httpContext.Response.Headers.ContainsKey(header));
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TransformResponseAsync_StrictTransportSecurity_CopiedIfNotPresent(bool alreadyPresent)
    {
        var transformer = HttpTransformer.Default;
        var httpContext = new DefaultHttpContext();
        var proxyResponse = new HttpResponseMessage()
        {
            Content = new ByteArrayContent(Array.Empty<byte>())
        };

        if (alreadyPresent)
        {
            httpContext.Response.Headers.StrictTransportSecurity = "max-age=31536000; includeSubDomains";
        }

        Assert.True(proxyResponse.Headers.TryAddWithoutValidation(HeaderNames.StrictTransportSecurity, "max-age=31000; preload"));

        await transformer.TransformResponseAsync(httpContext, proxyResponse, CancellationToken.None);

        var result = httpContext.Response.Headers.StrictTransportSecurity;
        if (alreadyPresent)
        {
            Assert.Equal("max-age=31536000; includeSubDomains", result);
        }
        else
        {
            Assert.Equal("max-age=31000; preload", result);
        }
    }

    [Fact]
    public async Task TransformResponseAsync_ContentLengthAndTransferEncoding_ContentLengthRemoved()
    {
        var transformer = HttpTransformer.Default;
        var httpContext = new DefaultHttpContext();
        var proxyResponse = new HttpResponseMessage()
        {
            Content = new ByteArrayContent(new byte[10])
        };

        proxyResponse.Headers.TransferEncodingChunked = true;
        Assert.Equal(10, proxyResponse.Content.Headers.ContentLength);

        await transformer.TransformResponseAsync(httpContext, proxyResponse, CancellationToken.None);

        Assert.False(httpContext.Response.Headers.ContainsKey(HeaderNames.ContentLength));
        // Transfer-Encoding is on the restricted list and removed. HttpClient will re-add it if required.
        Assert.False(httpContext.Response.Headers.ContainsKey(HeaderNames.TransferEncoding));
    }

    [Fact]
    public async Task TransformResponseTrailersAsync_RemovesRestrictedHeaders()
    {
        var transformer = HttpTransformer.Default;
        var httpContext = new DefaultHttpContext();
        var trailersFeature = new TestTrailersFeature();
        httpContext.Features.Set<IHttpResponseTrailersFeature>(trailersFeature);
        var proxyResponse = new HttpResponseMessage();

        foreach (var header in RestrictedHeaders)
        {
            Assert.True(proxyResponse.TrailingHeaders.TryAddWithoutValidation(header, "value"));
        }

        await transformer.TransformResponseTrailersAsync(httpContext, proxyResponse, CancellationToken.None);

        foreach (var header in RestrictedHeaders)
        {
            Assert.False(trailersFeature.Trailers.ContainsKey(header));
        }
    }

    public enum ImplementationType
    {
        StructuredTransformer,
        DerivedWithoutCT,
        DerivedWithCT,
    }

    public static IEnumerable<object[]> ImplementationTypes_MemberData() =>
        Enum.GetValues<ImplementationType>().Select(i => new object[] { i });

    [Theory]
    [MemberData(nameof(ImplementationTypes_MemberData))]
    public async Task DerivedImplementation_TransformRequestAsync_DerivedImplementationCalled(ImplementationType implementationType)
    {
        var implementationCalled = 0;

        var transformer = GetTransformerImplementation(implementationType, () => implementationCalled++);

        var httpContext = new DefaultHttpContext();
        var proxyRequest = new HttpRequestMessage();
        var destinationPrefix = "http://destinationhost:9090/path";

        using var cts = new CancellationTokenSource();
        await transformer.TransformRequestAsync(httpContext, proxyRequest, destinationPrefix, cts.Token);

        Assert.Equal(1, implementationCalled);
    }

    [Theory]
    [MemberData(nameof(ImplementationTypes_MemberData))]
    public async Task DerivedImplementation_TransformResponseAsync_DerivedImplementationCalled(ImplementationType implementationType)
    {
        var implementationCalled = 0;

        var transformer = GetTransformerImplementation(implementationType, () => implementationCalled++);

        var httpContext = new DefaultHttpContext();
        var proxyResponse = new HttpResponseMessage();

        using var cts = new CancellationTokenSource();
        await transformer.TransformResponseAsync(httpContext, proxyResponse, cts.Token);

        Assert.Equal(1, implementationCalled);
    }

    [Theory]
    [MemberData(nameof(ImplementationTypes_MemberData))]
    public async Task DerivedImplementation_TransformResponseTrailersAsync_DerivedImplementationCalled(ImplementationType implementationType)
    {
        var implementationCalled = 0;

        var transformer = GetTransformerImplementation(implementationType, () => implementationCalled++);

        var httpContext = new DefaultHttpContext();
        var proxyResponse = new HttpResponseMessage();

        httpContext.Features.Set<IHttpResponseTrailersFeature>(new TestTrailersFeature());

        using var cts = new CancellationTokenSource();
        await transformer.TransformResponseTrailersAsync(httpContext, proxyResponse, cts.Token);

        Assert.Equal(1, implementationCalled);
    }

    private static HttpTransformer GetTransformerImplementation(ImplementationType implementationType, Action callback)
    {
        return implementationType switch
        {
            ImplementationType.StructuredTransformer => TransformBuilderTests.CreateTransformBuilder().CreateInternal(context =>
            {
                context.AddRequestTransform(context =>
                {
                    callback();
                    return default;
                });
                context.AddResponseTransform(context =>
                {
                    callback();
                    return default;
                });
                context.AddResponseTrailersTransform(context =>
                {
                    callback();
                    return default;
                });
            }),
            ImplementationType.DerivedWithoutCT => new DerivedTransformerWithoutCT { Callback = callback },
            ImplementationType.DerivedWithCT => new DerivedTransformerWithCT { Callback = callback },
            _ => throw new InvalidOperationException(implementationType.ToString())
        };
    }

    private sealed class DerivedTransformerWithoutCT : HttpTransformer
    {
        public Action Callback { get; set; }

#pragma warning disable CS0672 // We're intentionally testing the obsolete overloads
#pragma warning disable CS0618
        public override ValueTask TransformRequestAsync(HttpContext httpContext, HttpRequestMessage proxyRequest, string destinationPrefix)
        {
            Callback();

            return base.TransformRequestAsync(httpContext, proxyRequest, destinationPrefix);
        }

        public override ValueTask<bool> TransformResponseAsync(HttpContext httpContext, HttpResponseMessage proxyResponse)
        {
            Callback();
            return base.TransformResponseAsync(httpContext, proxyResponse);
        }

        public override ValueTask TransformResponseTrailersAsync(HttpContext httpContext, HttpResponseMessage proxyResponse)
        {
            Callback();
            return base.TransformResponseTrailersAsync(httpContext, proxyResponse);
        }
#pragma warning restore CS0618
#pragma warning restore CS0672
    }

    private sealed class DerivedTransformerWithCT : HttpTransformer
    {
        public Action Callback { get; set; }

        public override ValueTask TransformRequestAsync(HttpContext httpContext, HttpRequestMessage proxyRequest, string destinationPrefix, CancellationToken cancellationToken)
        {
            Callback();
            return base.TransformRequestAsync(httpContext, proxyRequest, destinationPrefix, cancellationToken);
        }

        public override ValueTask<bool> TransformResponseAsync(HttpContext httpContext, HttpResponseMessage proxyResponse, CancellationToken cancellationToken)
        {
            Callback();
            return base.TransformResponseAsync(httpContext, proxyResponse, cancellationToken);
        }

        public override ValueTask TransformResponseTrailersAsync(HttpContext httpContext, HttpResponseMessage proxyResponse, CancellationToken cancellationToken)
        {
            Callback();
            return base.TransformResponseTrailersAsync(httpContext, proxyResponse, cancellationToken);
        }
    }
}
