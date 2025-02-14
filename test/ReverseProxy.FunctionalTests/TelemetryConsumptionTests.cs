// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Yarp.ReverseProxy.Common;
using Yarp.ReverseProxy.Forwarder;
using Yarp.Telemetry.Consumption;

namespace Yarp.ReverseProxy;

public class TelemetryConsumptionTests
{
    public enum RegistrationApproach
    {
        WithInstanceHelper,
        WithGenericHelper,
        Manual
    }

    private static void RegisterTelemetryConsumers(IServiceCollection services, RegistrationApproach approach)
    {
        if (approach == RegistrationApproach.WithInstanceHelper)
        {
            services.AddTelemetryConsumer(new TelemetryConsumer());
            services.AddTelemetryConsumer(new SecondTelemetryConsumer());
        }
        else if (approach == RegistrationApproach.WithGenericHelper)
        {
            services.AddTelemetryConsumer<TelemetryConsumer>();
            services.AddTelemetryConsumer<SecondTelemetryConsumer>();
        }
        else if (approach == RegistrationApproach.Manual)
        {
            services.AddSingleton<TelemetryConsumer>();
            services.AddSingleton(services => (IForwarderTelemetryConsumer)services.GetRequiredService<TelemetryConsumer>());
            services.AddSingleton(services => (IKestrelTelemetryConsumer)services.GetRequiredService<TelemetryConsumer>());
            services.AddSingleton(services => (IHttpTelemetryConsumer)services.GetRequiredService<TelemetryConsumer>());
            services.AddSingleton(services => (INameResolutionTelemetryConsumer)services.GetRequiredService<TelemetryConsumer>());
            services.AddSingleton(services => (INetSecurityTelemetryConsumer)services.GetRequiredService<TelemetryConsumer>());
            services.AddSingleton(services => (ISocketsTelemetryConsumer)services.GetRequiredService<TelemetryConsumer>());

            services.AddSingleton<SecondTelemetryConsumer>();
            services.AddSingleton(services => (IForwarderTelemetryConsumer)services.GetRequiredService<SecondTelemetryConsumer>());
            services.AddSingleton(services => (IKestrelTelemetryConsumer)services.GetRequiredService<SecondTelemetryConsumer>());
            services.AddSingleton(services => (IHttpTelemetryConsumer)services.GetRequiredService<SecondTelemetryConsumer>());
            services.AddSingleton(services => (INameResolutionTelemetryConsumer)services.GetRequiredService<SecondTelemetryConsumer>());
            services.AddSingleton(services => (INetSecurityTelemetryConsumer)services.GetRequiredService<SecondTelemetryConsumer>());
            services.AddSingleton(services => (ISocketsTelemetryConsumer)services.GetRequiredService<SecondTelemetryConsumer>());

            services.AddTelemetryListeners();
        }
    }

    private static void RegisterMetricsConsumers(IServiceCollection services, RegistrationApproach approach)
    {
        if (approach == RegistrationApproach.WithInstanceHelper)
        {
            services.AddMetricsConsumer(new MetricsConsumer());
        }
        else if (approach == RegistrationApproach.WithGenericHelper)
        {
            services.AddMetricsConsumer<MetricsConsumer>();
        }
        else if (approach == RegistrationApproach.Manual)
        {
            services.AddSingleton<MetricsConsumer>();
            services.AddSingleton(services => (IMetricsConsumer<ForwarderMetrics>)services.GetRequiredService<MetricsConsumer>());
            services.AddSingleton(services => (IMetricsConsumer<KestrelMetrics>)services.GetRequiredService<MetricsConsumer>());
            services.AddSingleton(services => (IMetricsConsumer<HttpMetrics>)services.GetRequiredService<MetricsConsumer>());
            services.AddSingleton(services => (IMetricsConsumer<NameResolutionMetrics>)services.GetRequiredService<MetricsConsumer>());
            services.AddSingleton(services => (IMetricsConsumer<NetSecurityMetrics>)services.GetRequiredService<MetricsConsumer>());
            services.AddSingleton(services => (IMetricsConsumer<SocketsMetrics>)services.GetRequiredService<MetricsConsumer>());

            services.AddTelemetryListeners();
        }
    }

    private static void VerifyStages(string[] expected, List<(string Stage, DateTime Timestamp)> stages)
    {
        Assert.Equal(expected, stages.Select(s => s.Stage).ToArray());

        for (var i = 1; i < stages.Count; i++)
        {
            Assert.True(stages[i - 1].Timestamp <= stages[i].Timestamp);
        }
    }

    [Theory]
    [InlineData(RegistrationApproach.WithInstanceHelper)]
    [InlineData(RegistrationApproach.WithGenericHelper)]
    [InlineData(RegistrationApproach.Manual)]
    public async Task TelemetryConsumptionWorks(RegistrationApproach registrationApproach)
    {
        var useHttpsOnDestination = !OperatingSystem.IsMacOS();

        var test = new TestEnvironment(
            async context => await context.Response.WriteAsync("Foo"))
        {
            UseHttpsOnDestination = useHttpsOnDestination,
            ClusterId = Guid.NewGuid().ToString(),
            ConfigureProxy = proxyBuilder => RegisterTelemetryConsumers(proxyBuilder.Services, registrationApproach),
        };

        await test.Invoke(async uri =>
        {
            using var httpClient = new HttpClient();
            await httpClient.GetStringAsync(uri);
        });

        var expected = new[]
        {
            "OnConnectionStart-Kestrel",
            "OnRequestStart-Kestrel",
            "OnForwarderInvoke",
            "OnForwarderStart",
            "OnForwarderStage-SendAsyncStart",
            "OnRequestStart",
            "OnConnectStart",
            "OnConnectStop",
            "OnHandshakeStart",
            "OnHandshakeStop",
            "OnConnectionEstablished",
            "OnRequestLeftQueue",
            "OnRequestHeadersStart",
            "OnRequestHeadersStop",
            "OnResponseHeadersStart",
            "OnResponseHeadersStop",
            "OnRequestStop",
            "OnForwarderStage-SendAsyncStop",
            "OnForwarderStage-ResponseContentTransferStart",
            "OnContentTransferred",
            "OnForwarderStop",
            "OnRequestStop-Kestrel",
            "OnConnectionStop-Kestrel",
        };

        if (!useHttpsOnDestination)
        {
            expected = expected.Where(s => !s.Contains("OnHandshake", StringComparison.Ordinal)).ToArray();
        }

        foreach (var consumerType in new[] { typeof(TelemetryConsumer), typeof(SecondTelemetryConsumer) })
        {
            Assert.True(TelemetryConsumer.PerClusterTelemetry.TryGetValue((test.ClusterId, consumerType), out var stages));
            VerifyStages(expected, stages);
        }
    }

    [Theory]
    [InlineData(RegistrationApproach.WithInstanceHelper)]
    [InlineData(RegistrationApproach.WithGenericHelper)]
    [InlineData(RegistrationApproach.Manual)]
    public async Task NonProxyTelemetryConsumptionWorks(RegistrationApproach registrationApproach)
    {
        var redirected = false;

        var test = new TestEnvironment(
            async context =>
            {
                if (redirected)
                {
                    await context.Response.WriteAsync("Foo");
                }
                else
                {
                    context.Response.Redirect("/foo");
                    redirected = true;
                }
            })
        {
            UseHttpsOnDestination = true,
            ConfigureProxy = proxyBuilder => RegisterTelemetryConsumers(proxyBuilder.Services, registrationApproach),
        };
        var path = $"/{Guid.NewGuid()}";

        await test.Invoke(async uri =>
        {
            using var httpClient = new HttpClient();
            await httpClient.GetStringAsync($"{uri.TrimEnd('/')}{path}");
        });

        var expected = new[]
        {
            "OnRequestStart",
            "OnConnectStart",
            "OnConnectStop",
            "OnConnectionEstablished",
            "OnRequestLeftQueue",
            "OnRequestHeadersStart",
            "OnRequestHeadersStop",
            "OnResponseHeadersStart",
            "OnResponseHeadersStop",
#if NET8_0_OR_GREATER
            "OnRedirect",
#endif
            "OnRequestHeadersStart",
            "OnRequestHeadersStop",
            "OnResponseHeadersStart",
            "OnResponseHeadersStop",
            "OnResponseContentStart",
            "OnResponseContentStop",
            "OnRequestStop",
        };

        foreach (var consumerType in new[] { typeof(TelemetryConsumer), typeof(SecondTelemetryConsumer) })
        {
            Assert.True(TelemetryConsumer.PerPathAndQueryTelemetry.TryGetValue((path, consumerType), out var stages));
            VerifyStages(expected, stages);
        }
    }

    private class SecondTelemetryConsumer : TelemetryConsumer { }

    private class TelemetryConsumer :
        IForwarderTelemetryConsumer,
        IKestrelTelemetryConsumer,
        IHttpTelemetryConsumer,
        INameResolutionTelemetryConsumer,
        INetSecurityTelemetryConsumer,
        ISocketsTelemetryConsumer
    {
        public static readonly ConcurrentDictionary<(string, Type), List<(string Stage, DateTime Timestamp)>> PerClusterTelemetry = new();
        public static readonly ConcurrentDictionary<(string, Type), List<(string Stage, DateTime Timestamp)>> PerPathAndQueryTelemetry = new();

        private readonly AsyncLocal<List<(string Stage, DateTime Timestamp)>> _stages = new();

        private void AddStage(string stage, DateTime timestamp)
        {
            var stages = _stages.Value ??= new List<(string Stage, DateTime Timestamp)>();

            lock (stages)
            {
                stages.Add((stage, timestamp));
            }
        }

        public void OnForwarderStart(DateTime timestamp, string destinationPrefix) => AddStage(nameof(OnForwarderStart), timestamp);
        public void OnForwarderStop(DateTime timestamp, int statusCode) => AddStage(nameof(OnForwarderStop), timestamp);
        public void OnForwarderFailed(DateTime timestamp, ForwarderError error) => AddStage(nameof(OnForwarderFailed), timestamp);
        public void OnForwarderStage(DateTime timestamp, Telemetry.Consumption.ForwarderStage stage) => AddStage($"{nameof(OnForwarderStage)}-{stage}", timestamp);
        public void OnContentTransferring(DateTime timestamp, bool isRequest, long contentLength, long iops, TimeSpan readTime, TimeSpan writeTime) => AddStage(nameof(OnContentTransferring), timestamp);
        public void OnContentTransferred(DateTime timestamp, bool isRequest, long contentLength, long iops, TimeSpan readTime, TimeSpan writeTime, TimeSpan firstReadTime) => AddStage(nameof(OnContentTransferred), timestamp);
        public void OnForwarderInvoke(DateTime timestamp, string clusterId, string routeId, string destinationId)
        {
            AddStage(nameof(OnForwarderInvoke), timestamp);
            PerClusterTelemetry.TryAdd((clusterId, GetType()), _stages.Value);
        }
        public void OnRequestStart(DateTime timestamp, string scheme, string host, int port, string pathAndQuery, int versionMajor, int versionMinor, HttpVersionPolicy versionPolicy)
        {
            AddStage(nameof(OnRequestStart), timestamp);
            PerPathAndQueryTelemetry.TryAdd((pathAndQuery, GetType()), _stages.Value);
        }
        public void OnRequestStop(DateTime timestamp) => AddStage(nameof(OnRequestStop), timestamp);
        public void OnRequestFailed(DateTime timestamp) => AddStage(nameof(OnRequestFailed), timestamp);
        public void OnConnectionEstablished(DateTime timestamp, int versionMajor, int versionMinor) => AddStage(nameof(OnConnectionEstablished), timestamp);
        public void OnRequestLeftQueue(DateTime timestamp, TimeSpan timeOnQueue, int versionMajor, int versionMinor) => AddStage(nameof(OnRequestLeftQueue), timestamp);
        public void OnRequestHeadersStart(DateTime timestamp) => AddStage(nameof(OnRequestHeadersStart), timestamp);
        public void OnRequestHeadersStop(DateTime timestamp) => AddStage(nameof(OnRequestHeadersStop), timestamp);
        public void OnRequestContentStart(DateTime timestamp) => AddStage(nameof(OnRequestContentStart), timestamp);
        public void OnRequestContentStop(DateTime timestamp, long contentLength) => AddStage(nameof(OnRequestContentStop), timestamp);
        public void OnResponseHeadersStart(DateTime timestamp) => AddStage(nameof(OnResponseHeadersStart), timestamp);
        public void OnResponseHeadersStop(DateTime timestamp) => AddStage(nameof(OnResponseHeadersStop), timestamp);
        public void OnResponseContentStart(DateTime timestamp) => AddStage(nameof(OnResponseContentStart), timestamp);
        public void OnResponseContentStop(DateTime timestamp) => AddStage(nameof(OnResponseContentStop), timestamp);
        public void OnResolutionStart(DateTime timestamp, string hostNameOrAddress) => AddStage(nameof(OnResolutionStart), timestamp);
        public void OnResolutionStop(DateTime timestamp) => AddStage(nameof(OnResolutionStop), timestamp);
        public void OnResolutionFailed(DateTime timestamp) => AddStage(nameof(OnResolutionFailed), timestamp);
        public void OnHandshakeStart(DateTime timestamp, bool isServer, string targetHost) => AddStage(nameof(OnHandshakeStart), timestamp);
        public void OnHandshakeStop(DateTime timestamp, SslProtocols protocol) => AddStage(nameof(OnHandshakeStop), timestamp);
        public void OnHandshakeFailed(DateTime timestamp, bool isServer, TimeSpan elapsed, string exceptionMessage) => AddStage(nameof(OnHandshakeFailed), timestamp);
        public void OnConnectStart(DateTime timestamp, string address) => AddStage(nameof(OnConnectStart), timestamp);
        public void OnConnectStop(DateTime timestamp) => AddStage(nameof(OnConnectStop), timestamp);
        public void OnConnectFailed(DateTime timestamp, SocketError error, string exceptionMessage) => AddStage(nameof(OnConnectFailed), timestamp);
        public void OnConnectionStart(DateTime timestamp, string connectionId, string localEndPoint, string remoteEndPoint) => AddStage($"{nameof(OnConnectionStart)}-Kestrel", timestamp);
        public void OnRequestStart(DateTime timestamp, string connectionId, string requestId, string httpVersion, string path, string method) => AddStage($"{nameof(OnRequestStart)}-Kestrel", timestamp);
        public void OnRequestStop(DateTime timestamp, string connectionId, string requestId, string httpVersion, string path, string method) => AddStage($"{nameof(OnRequestStop)}-Kestrel", timestamp);
        public void OnConnectionStop(DateTime timestamp, string connectionId) => AddStage($"{nameof(OnConnectionStop)}-Kestrel", timestamp);
        public void OnRedirect(DateTime timestamp, string redirectUri) => AddStage(nameof(OnRedirect), timestamp);
    }

    [Theory]
    [InlineData(RegistrationApproach.WithInstanceHelper)]
    [InlineData(RegistrationApproach.WithGenericHelper)]
    [InlineData(RegistrationApproach.Manual)]
    public async Task MetricsConsumptionWorks(RegistrationApproach registrationApproach)
    {
        MetricsOptions.Interval = TimeSpan.FromMilliseconds(10);

        var test = new TestEnvironment(
            async context => await context.Response.WriteAsync("Foo"))
        {
            UseHttpsOnDestination = true,
            ConfigureProxy = proxyBuilder => RegisterMetricsConsumers(proxyBuilder.Services, registrationApproach),
        };
        var consumerBox = new MetricsConsumer.MetricsConsumerBox();
        MetricsConsumer.ScopeInstance.Value = consumerBox;
        MetricsConsumer consumer = null;

        await test.Invoke(async uri =>
        {
            var httpClient = new HttpClient();
            await httpClient.GetStringAsync(uri);

            consumer = consumerBox.Instance;

            try
            {
                // Do some arbitrary DNS work to get metrics, since we're connecting to localhost
                _ = await Dns.GetHostAddressesAsync("microsoft.com");
            }
            catch { }

            await Task.WhenAll(
                WaitAsync(() => consumer.ProxyMetrics.LastOrDefault()?.RequestsStarted > 0, nameof(ForwarderMetrics)),
                WaitAsync(() => consumer.KestrelMetrics.LastOrDefault()?.TotalConnections > 0, nameof(KestrelMetrics)),
                WaitAsync(() => consumer.HttpMetrics.LastOrDefault()?.RequestsStarted > 0, nameof(HttpMetrics)),
                WaitAsync(() => consumer.SocketsMetrics.LastOrDefault()?.OutgoingConnectionsEstablished > 0, nameof(SocketsMetrics)),
                WaitAsync(() => consumer.NetSecurityMetrics.LastOrDefault()?.TotalTlsHandshakes > 0, nameof(NetSecurityMetrics)),
                WaitAsync(() => consumer.NameResolutionMetrics.LastOrDefault()?.DnsLookupsRequested > 0, nameof(NameResolutionMetrics)));
        });

        VerifyTimestamp(consumer.ProxyMetrics.Last().Timestamp);
        VerifyTimestamp(consumer.KestrelMetrics.Last().Timestamp);
        VerifyTimestamp(consumer.HttpMetrics.Last().Timestamp);
        VerifyTimestamp(consumer.SocketsMetrics.Last().Timestamp);
        VerifyTimestamp(consumer.NetSecurityMetrics.Last().Timestamp);
        VerifyTimestamp(consumer.NameResolutionMetrics.Last().Timestamp);

        static void VerifyTimestamp(DateTime timestamp)
        {
            var now = DateTime.UtcNow;
            Assert.InRange(timestamp, now.Subtract(TimeSpan.FromSeconds(10)), now.AddSeconds(10));
        }

        static async Task WaitAsync(Func<bool> condition, string name)
        {
            var stopwatch = Stopwatch.StartNew();
            while (!condition())
            {
                if (stopwatch.Elapsed > TimeSpan.FromSeconds(10))
                {
                    throw new TimeoutException($"Timed out waiting for {name}");
                }
                await Task.Delay(10);
            }
        }
    }

    private sealed class MetricsConsumer :
        IMetricsConsumer<ForwarderMetrics>,
        IMetricsConsumer<KestrelMetrics>,
        IMetricsConsumer<HttpMetrics>,
        IMetricsConsumer<NameResolutionMetrics>,
        IMetricsConsumer<NetSecurityMetrics>,
        IMetricsConsumer<SocketsMetrics>
    {
        public sealed class MetricsConsumerBox
        {
            public MetricsConsumer Instance;
        }

        public static readonly AsyncLocal<MetricsConsumerBox> ScopeInstance = new();

        public readonly ConcurrentQueue<ForwarderMetrics> ProxyMetrics = new();
        public readonly ConcurrentQueue<KestrelMetrics> KestrelMetrics = new();
        public readonly ConcurrentQueue<HttpMetrics> HttpMetrics = new();
        public readonly ConcurrentQueue<SocketsMetrics> SocketsMetrics = new();
        public readonly ConcurrentQueue<NetSecurityMetrics> NetSecurityMetrics = new();
        public readonly ConcurrentQueue<NameResolutionMetrics> NameResolutionMetrics = new();

        public MetricsConsumer()
        {
            ScopeInstance.Value.Instance = this;
        }

        public void OnMetrics(ForwarderMetrics previous, ForwarderMetrics current) => ProxyMetrics.Enqueue(current);
        public void OnMetrics(KestrelMetrics previous, KestrelMetrics current) => KestrelMetrics.Enqueue(current);
        public void OnMetrics(SocketsMetrics previous, SocketsMetrics current) => SocketsMetrics.Enqueue(current);
        public void OnMetrics(NetSecurityMetrics previous, NetSecurityMetrics current) => NetSecurityMetrics.Enqueue(current);
        public void OnMetrics(NameResolutionMetrics previous, NameResolutionMetrics current) => NameResolutionMetrics.Enqueue(current);
        public void OnMetrics(HttpMetrics previous, HttpMetrics current) => HttpMetrics.Enqueue(current);
    }
}
