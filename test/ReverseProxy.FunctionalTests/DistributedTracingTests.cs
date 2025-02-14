// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Xunit;
using Yarp.ReverseProxy.Common;

namespace Yarp.ReverseProxy;

public class DistributedTracingTests
{
    // These constants depend on the default behavior of DistributedContextPropagator
    private const string Baggage = "Correlation-Context";
    private const string TraceParent = "traceparent";
    private const string TraceState = "tracestate";
    private const string RequestId = "Request-Id";

    [Theory]
    [InlineData(ActivityIdFormat.W3C)]
    [InlineData(ActivityIdFormat.Hierarchical)]
    public async Task DistributedTracing_Works(ActivityIdFormat idFormat)
    {
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

        var clientActivity = new Activity("Foo");
        clientActivity.SetIdFormat(idFormat);
        clientActivity.TraceStateString = "Bar";
        clientActivity.AddBaggage("One", "1");
        clientActivity.AddBaggage("Two", "2");
        clientActivity.Start();

        await test.Invoke(async uri =>
        {
            using var client = new HttpClient();
            Assert.Equal("Hello", await client.GetStringAsync(uri));
        });

        Assert.NotEmpty(proxyHeaders);
        Assert.NotEmpty(downstreamHeaders);

        ValidateActivities(idFormat, clientActivity, proxyHeaders, downstreamHeaders);
    }

    private static void ValidateActivities(ActivityIdFormat idFormat, Activity client, HeaderDictionary proxy, HeaderDictionary downstream)
    {
        var baggage = string.Join(", ", client.Baggage.Select(pair => $"{pair.Key}={pair.Value}"));
        Assert.Equal(baggage, proxy[Baggage]);
        Assert.Equal(baggage, downstream[Baggage]);

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
