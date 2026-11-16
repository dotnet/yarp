// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Yarp.ReverseProxy.Common;
using Yarp.ReverseProxy.Forwarder;

namespace Yarp.ReverseProxy;

[Collection(nameof(DistributedTracingTests))]
public class DistributedTracingTests
{
    private const string TraceParent = "traceparent";
    private const string TraceState = "tracestate";
    private const string RequestId = "Request-Id";

    [Theory]
    [InlineData(ActivityIdFormat.W3C, false)]
    [InlineData(ActivityIdFormat.W3C, true)]
    [InlineData(ActivityIdFormat.Hierarchical, true)]
    public async Task DistributedTracing_Works(ActivityIdFormat idFormat, bool usePreW3CPropagator)
    {
        var propagator = usePreW3CPropagator
            ? DistributedContextPropagator.CreatePreW3CPropagator()
            : DistributedContextPropagator.CreateDefaultPropagator();
        var proxyHeaders = new HeaderDictionary();
        var downstreamHeaders = new HeaderDictionary();

        var test = new TestEnvironment(
            async context =>
            {
                foreach (var header in context.Request.Headers)
                {
                    downstreamHeaders.Add(header);
                }
                await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes("Hello"));
            })
        {
            ConfigureProxyServices = services => services.AddSingleton(propagator),
            ConfigureDestinationServices = services => services.AddSingleton(propagator),
            ConfigureProxy = proxy => proxy.ConfigureHttpClient((_, handler) =>
            {
                handler.ActivityHeadersPropagator = new ReverseProxyPropagator(propagator);
            }),
            ConfigureProxyApp = proxyApp =>
            {
                proxyApp.Use(next => context =>
                {
                    foreach (var header in context.Request.Headers)
                    {
                        proxyHeaders.Add(header);
                    }
                    return next(context);
                });
            },
        };

        var forceDefaultIdFormat = Activity.ForceDefaultIdFormat;
        try
        {
            Activity.ForceDefaultIdFormat = false;
            using var clientActivity = new Activity("Foo");
            clientActivity.SetIdFormat(idFormat);
            clientActivity.TraceStateString = "foo=bar";
            clientActivity.AddBaggage("One", "1");
            clientActivity.AddBaggage("Two", "2");
            clientActivity.Start();

            await test.Invoke(async uri =>
            {
                using var client = new HttpClient(new SocketsHttpHandler
                {
                    ActivityHeadersPropagator = propagator,
                });
                Assert.Equal("Hello", await client.GetStringAsync(uri));
            });

            Assert.NotEmpty(proxyHeaders);
            Assert.NotEmpty(downstreamHeaders);

            ValidateActivities(idFormat, usePreW3CPropagator, clientActivity, proxyHeaders, downstreamHeaders);
        }
        finally
        {
            Activity.ForceDefaultIdFormat = forceDefaultIdFormat;
        }
    }

    [CollectionDefinition(nameof(DistributedTracingTests), DisableParallelization = true)]
    public sealed class DistributedTracingTestCollection;

    private static void ValidateActivities(ActivityIdFormat idFormat, bool usePreW3CPropagator,
        Activity client, HeaderDictionary proxy, HeaderDictionary downstream)
    {
        var baggageHeader = usePreW3CPropagator ? "Correlation-Context" : "baggage";
        var baggage = string.Join(", ", client.Baggage.Select(pair =>
            usePreW3CPropagator ? $"{pair.Key}={pair.Value}" : $"{pair.Key} = {pair.Value}"));
        Assert.Equal(baggage, proxy[baggageHeader]);
        Assert.Equal(baggage, downstream[baggageHeader]);

        if (idFormat == ActivityIdFormat.W3C)
        {
            Assert.True(ActivityContext.TryParse(proxy[TraceParent], proxy[TraceState], out var proxyContext));
            Assert.True(ActivityContext.TryParse(downstream[TraceParent], downstream[TraceState], out var downstreamContext));
            Assert.Equal(client.TraceStateString, proxyContext.TraceState);
            Assert.Equal(client.TraceStateString, downstreamContext.TraceState);
            var proxyTraceId = proxyContext.TraceId.ToHexString();
            var proxySpanId = proxyContext.SpanId.ToHexString();
            var downstreamTraceId = downstreamContext.TraceId.ToHexString();
            var downstreamSpanId = downstreamContext.SpanId.ToHexString();
            Assert.Equal(client.TraceId.ToHexString(), proxyTraceId);
            Assert.Equal(client.TraceId.ToHexString(), downstreamTraceId);
            Assert.NotEqual(proxySpanId, downstreamSpanId);
        }
        else
        {
            var proxyId = proxy[RequestId].ToString();
            var downstreamId = downstream[RequestId].ToString();
            Assert.StartsWith(client.Id, proxyId);
            Assert.StartsWith(proxyId, downstreamId);
            Assert.NotEqual(proxyId, downstreamId);
        }
    }
}
