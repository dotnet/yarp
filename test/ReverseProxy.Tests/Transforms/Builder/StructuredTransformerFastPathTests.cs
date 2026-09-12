// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using Xunit;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Utilities;
using Yarp.Tests.Common;

namespace Yarp.ReverseProxy.Transforms.Builder.Tests;

public class StructuredTransformerFastPathTests
{
    [Theory]
    [InlineData("HTTP/1.1", "127.0.0.1")]
    [InlineData("HTTP/2", "::ffff:127.0.0.1")]
    [InlineData("HTTP/2", "2001:db8::1")]
    public async Task BuiltInXForwardedAndHeaderTransforms_MatchContextFallback(string protocol, string remoteIp)
    {
        static void Configure(TransformBuilderContext context)
        {
            context.AddXForwarded(ForwardedTransformActions.Append);
            context.AddRequestHeader("x-MiXeD-Set", "set", append: false);
            context.AddRequestHeader("X-Duplicate", "tail", append: true);
            context.AddRequestHeaderRemove("x-REMOVE");
        }

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var optimized = CreateTransformer(Configure);
        var fallback = CreateTransformerWithFallback(Configure);
        Assert.True(UsesFastPath(optimized));
        Assert.False(UsesFastPath(fallback));

        var optimizedResult = await TransformRequestAsync(optimized, protocol, IPAddress.Parse(remoteIp), cancellation.Token);
        var fallbackResult = await TransformRequestAsync(fallback, protocol, IPAddress.Parse(remoteIp), cancellation.Token);

        Assert.Equal(fallbackResult, optimizedResult);
        Assert.Contains("X-Duplicate:one\u001ftwo\u001ftail", optimizedResult.Headers);
        Assert.Contains("x-MiXeD-Set:set", optimizedResult.Headers);
        Assert.DoesNotContain(optimizedResult.Headers, header => header.StartsWith("x-REMOVE:", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("HTTP/1.1", "127.0.0.1", 1234)]
    [InlineData("HTTP/2", "2001:db8::1", 4321)]
    public async Task ForwardedTransform_MatchesContextFallback(string protocol, string remoteIp, int remotePort)
    {
        static void Configure(TransformBuilderContext context)
        {
            context.UseDefaultForwarders = false;
            context.RequestTransforms.Add(new RequestHeaderForwardedTransform(
                new TestRandomFactory(),
                forFormat: NodeFormat.IpAndPort,
                byFormat: NodeFormat.None,
                host: true,
                proto: true,
                action: ForwardedTransformActions.Append));
            context.AddRequestHeader("X-Set", "replacement", append: false);
        }

        var optimized = CreateTransformer(Configure);
        var fallback = CreateTransformerWithFallback(Configure);
        Assert.True(UsesFastPath(optimized));
        Assert.False(UsesFastPath(fallback));

        var optimizedResult = await TransformRequestAsync(optimized, protocol, IPAddress.Parse(remoteIp), default, remotePort);
        var fallbackResult = await TransformRequestAsync(fallback, protocol, IPAddress.Parse(remoteIp), default, remotePort);

        Assert.Equal(fallbackResult, optimizedResult);
        Assert.Contains(optimizedResult.Headers, header =>
            header.StartsWith("Forwarded:for=\"unterminated\u001ffor=", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("HTTP/1.1")]
    [InlineData("HTTP/2")]
    public async Task PathQueryEncodingAndOrdering_ArePreserved(string protocol)
    {
        static void Configure(TransformBuilderContext context)
        {
            context.AddPathRemovePrefix("/api");
            context.AddPathPrefix("/v2/%2F");
            context.AddQueryValue("key", "replacement", append: false);
            context.AddQueryValue("added", "a/b", append: true);
            context.AddQueryRemoveKey("remove");
        }

        var transformer = CreateTransformer(Configure);
        Assert.False(UsesFastPath(transformer));
        var result = await TransformRequestAsync(transformer, protocol, IPAddress.IPv6Loopback, default);

        Assert.Equal("http://destination/base/v2/%252F/items/%252Fvalue?key=replacement&escaped=a%2Fb&added=a%2Fb", result.Uri);
        Assert.Equal(protocol == "HTTP/2", result.Headers.Contains("TE:traiLers"));
    }

    [Fact]
    public async Task CustomSyncAndAsyncTransforms_PreserveReassignmentAndCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var observedToken = default(CancellationToken);
        var transformer = CreateTransformer(context =>
        {
            context.AddRequestTransform(transformContext =>
            {
                observedToken = transformContext.CancellationToken;
                transformContext.DestinationPrefix = "https://other.example/base";
                transformContext.Path = "/reassigned/%2F";
                transformContext.Query = new QueryTransformContext(transformContext.HttpContext.Request);
                transformContext.Query.Collection["custom"] = "one/two";
                return default;
            });
            context.AddRequestTransform(async transformContext =>
            {
                await Task.Yield();
                transformContext.ProxyRequest.Headers.TryAddWithoutValidation("X-Async", "set");
            });
        });
        Assert.False(UsesFastPath(transformer));

        var result = await TransformRequestAsync(transformer, "HTTP/2", IPAddress.Loopback, cancellation.Token);

        Assert.Equal(cancellation.Token, observedToken);
        Assert.Equal("https://other.example/base/reassigned/%252F?key=one&key=two&remove=yes&escaped=a%2Fb&custom=one%2Ftwo", result.Uri);
        Assert.Contains("X-Async:set", result.Headers);
    }

    [Fact]
    public async Task DerivedBuiltInTransform_UsesOverriddenApplyAsync()
    {
        var transformer = CreateTransformer(context =>
        {
            context.UseDefaultForwarders = false;
            context.RequestTransforms.Add(new DerivedRequestHeaderValueTransform());
        });
        Assert.False(UsesFastPath(transformer));

        var result = await TransformRequestAsync(transformer, "HTTP/2", IPAddress.Loopback, default);

        Assert.Contains("X-Derived:derived", result.Headers);
    }

    [Fact]
    public async Task HeadersAllowed_OrderingAndHeadersCopied_MatchContextFallback()
    {
        static void Configure(TransformBuilderContext context)
        {
            context.UseDefaultForwarders = false;
            context.AddRequestHeader("X-Before", "before", append: true);
            context.AddRequestHeadersAllowed("X-Before", "X-After");
            context.AddRequestHeader("X-After", "after", append: true);
        }

        var optimized = CreateTransformer(Configure);
        var fallback = CreateTransformerWithFallback(Configure);
        Assert.True(UsesFastPath(optimized));
        Assert.False(UsesFastPath(fallback));

        var optimizedResult = await TransformRequestAsync(optimized, "HTTP/2", IPAddress.Loopback, default);
        var fallbackResult = await TransformRequestAsync(fallback, "HTTP/2", IPAddress.Loopback, default);

        Assert.Equal(fallbackResult, optimizedResult);
        Assert.Contains("X-Before:original-before\u001fbefore\u001foriginal-before", optimizedResult.Headers);
        Assert.Contains("X-After:original-after\u001fafter", optimizedResult.Headers);
    }

    [Fact]
    public async Task OriginalHostAndRouteValue_MatchContextFallback()
    {
        static void Configure(TransformBuilderContext context)
        {
            context.UseDefaultForwarders = false;
            context.AddOriginalHost(useOriginal: true);
            context.AddRequestHeaderRouteValue("X-Route", "id", append: false);
        }

        var optimized = CreateTransformer(Configure);
        var fallback = CreateTransformerWithFallback(Configure);
        Assert.True(UsesFastPath(optimized));
        Assert.False(UsesFastPath(fallback));

        var optimizedResult = await TransformRequestAsync(optimized, "HTTP/2", IPAddress.Loopback, default);
        var fallbackResult = await TransformRequestAsync(fallback, "HTTP/2", IPAddress.Loopback, default);

        Assert.Equal(fallbackResult, optimizedResult);
        Assert.Contains("Host:xn--host-6j1i.example:8443", optimizedResult.Headers);
        Assert.Contains("X-Route:route-value", optimizedResult.Headers);
    }

    [Fact]
    public void EligibleBuiltIns_OverrideFastPath()
    {
        RequestTransform[] transforms =
        [
            RequestHeaderOriginalHostTransform.OriginalHost,
            new RequestHeaderXForwardedForTransform("X-Forwarded-For", ForwardedTransformActions.Set),
            new RequestHeaderXForwardedHostTransform("X-Forwarded-Host", ForwardedTransformActions.Set),
            new RequestHeaderXForwardedProtoTransform("X-Forwarded-Proto", ForwardedTransformActions.Set),
            new RequestHeaderXForwardedPrefixTransform("X-Forwarded-Prefix", ForwardedTransformActions.Set),
            new RequestHeaderForwardedTransform(
                new TestRandomFactory(),
                NodeFormat.Ip,
                NodeFormat.None,
                host: true,
                proto: true,
                ForwardedTransformActions.Set),
            new RequestHeaderRemoveTransform("X-Remove"),
            new RequestHeaderValueTransform("X-Value", "value", append: false),
            new RequestHeaderRouteValueTransform("X-Route", "id", append: false),
            new RequestHeadersAllowedTransform(["X-Allowed"]),
        ];

        foreach (var transform in transforms)
        {
            var method = transform.GetType().GetMethod(
                "ApplyFast",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(method);
            Assert.Equal(transform.GetType(), method.DeclaringType);
        }
    }

    [Theory]
    [InlineData("HTTP/1.1")]
    [InlineData("HTTP/2")]
    public async Task ResponseHeadersAndTrailers_MatchContextFallback(string protocol)
    {
        static void Configure(TransformBuilderContext context)
        {
            context.AddXForwarded();
            context.AddResponseHeader("X-Append", "tail", append: true, ResponseCondition.Always);
            context.AddResponseHeaderRemove("X-Remove", ResponseCondition.Always);
            context.AddResponseTrailer("X-Trailer", "tail", append: true, ResponseCondition.Always);
        }

        var optimized = CreateTransformer(Configure);
        var fallback = CreateTransformerWithFallback(Configure);
        Assert.True(UsesFastPath(optimized));
        Assert.False(UsesFastPath(fallback));

        var optimizedResult = await TransformResponseAsync(optimized, protocol);
        var fallbackResult = await TransformResponseAsync(fallback, protocol);

        Assert.Equal(fallbackResult, optimizedResult);
        Assert.Contains("X-Append:one\u001ftwo\u001ftail", optimizedResult.Headers);
        Assert.Contains("X-Trailer:one\u001ftwo\u001ftail", optimizedResult.Trailers);
    }

    [Fact]
    public async Task BuiltInTransforms_AreConcurrencySafe()
    {
        static void Configure(TransformBuilderContext context)
        {
            context.AddXForwarded(ForwardedTransformActions.Append);
            context.AddRequestHeader("X-Duplicate", "tail", append: true);
        }

        var optimized = CreateTransformer(Configure);
        var fallback = CreateTransformerWithFallback(Configure);
        Assert.True(UsesFastPath(optimized));
        Assert.False(UsesFastPath(fallback));

        await Parallel.ForEachAsync(Enumerable.Range(0, 200), async (index, _) =>
        {
            var protocol = index % 2 == 0 ? "HTTP/1.1" : "HTTP/2";
            var ipAddress = index % 3 == 0 ? IPAddress.Loopback : IPAddress.IPv6Loopback;
            var optimizedResult = await TransformRequestAsync(optimized, protocol, ipAddress, default, index);
            var fallbackResult = await TransformRequestAsync(fallback, protocol, ipAddress, default, index);
            Assert.Equal(fallbackResult, optimizedResult);
        });
    }

    private static StructuredTransformer CreateTransformer(Action<TransformBuilderContext> configure)
    {
        return TransformBuilderTests.CreateTransformBuilder().CreateInternal(configure);
    }

    private static StructuredTransformer CreateTransformerWithFallback(Action<TransformBuilderContext> configure)
    {
        return TransformBuilderTests.CreateTransformBuilder().CreateInternal(context =>
        {
            configure(context);
            context.AddRequestTransform(static _ => default);
        });
    }

    private static async Task<RequestSnapshot> TransformRequestAsync(
        StructuredTransformer transformer,
        string protocol,
        IPAddress remoteIp,
        CancellationToken cancellationToken,
        int remotePort = 4321)
    {
        var context = CreateHttpContext(protocol, remoteIp, remotePort);
        using var request = new HttpRequestMessage
        {
            Content = new ByteArrayContent(Array.Empty<byte>()),
        };

        await transformer.TransformRequestAsync(context, request, "http://destination/base", cancellationToken);

        return new RequestSnapshot(request.RequestUri!.AbsoluteUri, GetHeaders(request));
    }

    private static async Task<ResponseSnapshot> TransformResponseAsync(StructuredTransformer transformer, string protocol)
    {
        var context = CreateHttpContext(protocol, IPAddress.IPv6Loopback, 4321);
        var trailersFeature = new TestTrailersFeature();
        context.Features.Set<IHttpResponseTrailersFeature>(trailersFeature);
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(Array.Empty<byte>()),
        };
        response.Headers.TryAddWithoutValidation("X-Append", new[] { "one", "two" });
        response.Headers.TryAddWithoutValidation("X-Remove", "remove");
        response.TrailingHeaders.TryAddWithoutValidation("X-Trailer", new[] { "one", "two" });

        await transformer.TransformResponseAsync(context, response, default);
        await transformer.TransformResponseTrailersAsync(context, response, default);

        return new ResponseSnapshot(GetHeaders(context.Response.Headers), GetHeaders(trailersFeature.Trailers));
    }

    private static DefaultHttpContext CreateHttpContext(string protocol, IPAddress remoteIp, int remotePort)
    {
        var context = new DefaultHttpContext();
        context.Request.Protocol = protocol;
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("ho本st.example", 8443);
        context.Request.PathBase = "/base";
        context.Request.Path = "/api/items/%2Fvalue";
        context.Request.QueryString = new QueryString("?key=one&key=two&remove=yes&escaped=a%2Fb");
        context.Connection.RemoteIpAddress = remoteIp;
        context.Connection.RemotePort = remotePort;
        context.Connection.LocalIpAddress = IPAddress.Loopback;
        context.Connection.LocalPort = 443;

        context.Request.Headers["x-forwarded-for"] = new StringValues(new[] { "192.0.2.1", "2001:db8::2" });
        context.Request.Headers["X-FORWARDED-HOST"] = new StringValues(new[] { "prior.example", "older.example" });
        context.Request.Headers["x-forwarded-proto"] = new StringValues(new[] { "http", "https" });
        context.Request.Headers["X-Forwarded-Prefix"] = new StringValues(new[] { "/prior", "/older" });
        context.Request.Headers["Forwarded"] = new StringValues(new[] { "for=\"unterminated", "for=192.0.2.1;proto=http" });
        context.Request.Headers["X-Duplicate"] = new StringValues(new[] { "one", "two" });
        context.Request.Headers["X-Remove"] = "remove";
        context.Request.Headers["X-Mixed-Set"] = "old";
        context.Request.Headers["X-Before"] = "original-before";
        context.Request.Headers["X-After"] = "original-after";
        context.Request.Headers[HeaderNames.TE] = "gzip, traiLers";
        context.Request.RouteValues["id"] = "route-value";
        return context;
    }

    private static bool UsesFastPath(StructuredTransformer transformer)
    {
        var field = typeof(StructuredTransformer).GetField(
            "_canUseRequestTransformFastPath",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<bool>(field.GetValue(transformer));
    }

    private static string[] GetHeaders(HttpRequestMessage request)
    {
        return request.Headers.NonValidated
            .Concat(request.Content!.Headers.NonValidated)
            .Select(header => $"{header.Key}:{string.Join('\u001f', header.Value)}")
            .OrderBy(header => header, StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] GetHeaders(IHeaderDictionary headers)
    {
        return headers
            .Select(header => $"{header.Key}:{string.Join('\u001f', header.Value.ToArray())}")
            .OrderBy(header => header, StringComparer.Ordinal)
            .ToArray();
    }

    private sealed record RequestSnapshot(string Uri, string[] Headers)
    {
        public bool Equals(RequestSnapshot? other)
        {
            return other is not null
                && string.Equals(Uri, other.Uri, StringComparison.Ordinal)
                && Headers.SequenceEqual(other.Headers, StringComparer.Ordinal);
        }

        public override int GetHashCode() => HashCode.Combine(Uri, Headers.Length);
    }

    private sealed record ResponseSnapshot(string[] Headers, string[] Trailers)
    {
        public bool Equals(ResponseSnapshot? other)
        {
            return other is not null
                && Headers.SequenceEqual(other.Headers, StringComparer.Ordinal)
                && Trailers.SequenceEqual(other.Trailers, StringComparer.Ordinal);
        }

        public override int GetHashCode() => HashCode.Combine(Headers.Length, Trailers.Length);
    }

    private sealed class TestRandomFactory : IRandomFactory
    {
        public Random CreateRandomInstance() => Random.Shared;
    }

    private sealed class DerivedRequestHeaderValueTransform : RequestHeaderValueTransform
    {
        public DerivedRequestHeaderValueTransform()
            : base("X-Derived", "base", append: false)
        {
        }

        public override ValueTask ApplyAsync(RequestTransformContext context)
        {
            context.ProxyRequest.Headers.TryAddWithoutValidation("X-Derived", "derived");
            return default;
        }
    }
}
