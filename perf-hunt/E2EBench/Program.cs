// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
// E2EBench: realistic end-to-end throughput/latency harness for the full MapReverseProxy pipeline.
//
// One process performs ONE measurement of a (target, http) combination so working-set and allocation
// snapshots stay clean. It hosts a real Kestrel backend, optionally a real YARP proxy (MapReverseProxy
// + LoadFromMemory, 1 route/1 cluster/1 destination -> backend), and an in-process async load client.
//
//   --target direct : client -> backend           (framework/Kestrel baseline, no YARP)
//   --target proxy  : client -> YARP proxy -> backend (framework + YARP)
//   (proxy - direct) isolates YARP's end-to-end cost.
//
// HTTP:
//   --http 1 : HTTP/1.1 everywhere
//   --http 2 : HTTP/2 (h2c prior knowledge) client->front and, for proxy, front->backend
//
// Metrics over the measured window: throughput (req/s), latency percentiles, process-wide
// allocated-bytes/request (before/after differencing cancels the identical client+backend cost so the
// delta reflects the proxy change), and working set. Everything else in the process is byte-identical
// across a before/after comparison, so a before-vs-after allocation delta attributes to the proxy.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;

namespace E2EBench;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var target = GetStr(args, "--target", "proxy");     // proxy | direct
        var http = GetInt(args, "--http", 1);               // 1 | 2
        var connections = GetInt(args, "--connections", 64);
        var warmupSec = GetInt(args, "--warmup", 5);
        var measureSec = GetInt(args, "--duration", 10);
        var bodyBytes = GetInt(args, "--body", 100);
        var label = GetStr(args, "--label", "run");

        var proto = http == 2 ? HttpProtocols.Http2 : HttpProtocols.Http1;
        var reqVersion = http == 2 ? HttpVersion.Version20 : HttpVersion.Version11;

        var backendPort = FreePort();
        var backend = BuildBackend(backendPort, proto, bodyBytes);
        await backend.StartAsync();
        var backendUrl = $"http://127.0.0.1:{backendPort}/";

        IHost? proxy = null;
        string targetUrl;
        if (target == "proxy")
        {
            var proxyPort = FreePort();
            proxy = BuildProxy(proxyPort, proto, backendUrl, reqVersion);
            await proxy.StartAsync();
            targetUrl = $"http://127.0.0.1:{proxyPort}/";
        }
        else
        {
            targetUrl = backendUrl;
        }

        var handler = new SocketsHttpHandler
        {
            EnableMultipleHttp2Connections = true,
            MaxConnectionsPerServer = connections,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(10),
            AutomaticDecompression = DecompressionMethods.None,
            UseProxy = false,
            UseCookies = false,
        };
        var client = new HttpMessageInvoker(handler);
        var uri = new Uri(targetUrl);

        // Phase 1: warmup (also opens the connection pool).
        await RunLoad(client, uri, reqVersion, connections, TimeSpan.FromSeconds(warmupSec), measure: false);

        // Phase 2: measured window.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var proc = Process.GetCurrentProcess();
        proc.Refresh();
        var wsBefore = proc.WorkingSet64;
        var allocBefore = GC.GetTotalAllocatedBytes(precise: true);
        var gc0 = GC.CollectionCount(0);
        var gc1 = GC.CollectionCount(1);
        var gc2 = GC.CollectionCount(2);

        var sw = Stopwatch.StartNew();
        var result = await RunLoad(client, uri, reqVersion, connections, TimeSpan.FromSeconds(measureSec), measure: true);
        sw.Stop();

        var allocAfter = GC.GetTotalAllocatedBytes(precise: true);
        proc.Refresh();
        var wsAfter = proc.WorkingSet64;

        var elapsed = sw.Elapsed.TotalSeconds;
        var rps = result.Count / elapsed;
        var allocPerReq = result.Count > 0 ? (double)(allocAfter - allocBefore) / result.Count : 0;
        var hist = result.Histogram;
        var total = result.Count;

        Console.WriteLine();
        Console.WriteLine($"RESULT label={label} target={target} http={http} conns={connections} dur={elapsed:F1}s " +
            $"requests={total} errors={result.Errors} rps={rps:F0} " +
            $"p50us={Percentile(hist, total, 0.50)} p90us={Percentile(hist, total, 0.90)} " +
            $"p99us={Percentile(hist, total, 0.99)} p999us={Percentile(hist, total, 0.999)} maxus={result.MaxMicros} " +
            $"alloc_per_req_B={allocPerReq:F1} ws_MB={wsAfter / (1024.0 * 1024.0):F1} " +
            $"gc0={GC.CollectionCount(0) - gc0} gc1={GC.CollectionCount(1) - gc1} gc2={GC.CollectionCount(2) - gc2}");

        client.Dispose();
        if (proxy is not null)
        {
            await proxy.StopAsync();
            proxy.Dispose();
        }
        await backend.StopAsync();
        backend.Dispose();
        return 0;
    }

    private sealed class LoadResult
    {
        public long Count;
        public long Errors;
        public long MaxMicros;
        public long[] Histogram = Array.Empty<long>();
    }

    private const int HistBuckets = 200_000; // 1us resolution up to 200ms; above clamps to last bucket.

    private static async Task<LoadResult> RunLoad(
        HttpMessageInvoker client, Uri uri, Version version, int workers, TimeSpan duration, bool measure)
    {
        var deadline = Stopwatch.GetTimestamp() + (long)(duration.TotalSeconds * Stopwatch.Frequency);
        var perWorker = new (long count, long errors, long max, long[] hist)[workers];
        var tasks = new Task[workers];

        for (var w = 0; w < workers; w++)
        {
            var idx = w;
            var hist = measure ? new long[HistBuckets] : Array.Empty<long>();
            tasks[w] = Task.Run(async () =>
            {
                long count = 0, errors = 0, max = 0;
                var buffer = new byte[8192];
                while (Stopwatch.GetTimestamp() < deadline)
                {
                    var start = Stopwatch.GetTimestamp();
                    try
                    {
                        using var req = new HttpRequestMessage(HttpMethod.Get, uri)
                        {
                            Version = version,
                            VersionPolicy = HttpVersionPolicy.RequestVersionExact,
                        };
                        using var resp = await client.SendAsync(req, CancellationToken.None);
                        var body = resp.Content;
                        using var s = await body.ReadAsStreamAsync();
                        while (await s.ReadAsync(buffer) > 0) { }
                        if (!resp.IsSuccessStatusCode)
                        {
                            errors++;
                            continue;
                        }
                    }
                    catch
                    {
                        errors++;
                        continue;
                    }

                    if (measure)
                    {
                        var us = (Stopwatch.GetTimestamp() - start) * 1_000_000L / Stopwatch.Frequency;
                        if (us > max)
                        {
                            max = us;
                        }
                        var bucket = us >= HistBuckets ? HistBuckets - 1 : (int)us;
                        hist[bucket]++;
                        count++;
                    }
                }
                perWorker[idx] = (count, errors, max, hist);
            });
        }

        await Task.WhenAll(tasks);

        var result = new LoadResult { Histogram = measure ? new long[HistBuckets] : Array.Empty<long>() };
        foreach (var (count, errors, max, hist) in perWorker)
        {
            result.Count += count;
            result.Errors += errors;
            if (max > result.MaxMicros)
            {
                result.MaxMicros = max;
            }
            if (measure && hist.Length > 0)
            {
                for (var i = 0; i < HistBuckets; i++)
                {
                    result.Histogram[i] += hist[i];
                }
            }
        }
        return result;
    }

    private static long Percentile(long[] hist, long total, double p)
    {
        if (total == 0 || hist.Length == 0)
        {
            return -1;
        }
        var target = (long)Math.Ceiling(p * total);
        long cum = 0;
        for (var i = 0; i < hist.Length; i++)
        {
            cum += hist[i];
            if (cum >= target)
            {
                return i;
            }
        }
        return hist.Length - 1;
    }

    private static IHost BuildBackend(int port, HttpProtocols proto, int bodyBytes)
    {
        var payload = new byte[bodyBytes];
        for (var i = 0; i < payload.Length; i++)
        {
            payload[i] = (byte)('a' + (i % 26));
        }

        return Host.CreateDefaultBuilder()
            .ConfigureLogging(b => b.ClearProviders().SetMinimumLevel(LogLevel.Warning))
            .ConfigureWebHostDefaults(web =>
            {
                web.UseKestrel(k => k.Listen(IPAddress.Loopback, port, lo => lo.Protocols = proto));
                web.Configure(app => app.Run(async ctx =>
                {
                    ctx.Response.StatusCode = 200;
                    ctx.Response.ContentType = "application/octet-stream";
                    ctx.Response.ContentLength = payload.Length;
                    await ctx.Response.Body.WriteAsync(payload);
                }));
            })
            .Build();
    }

    private static IHost BuildProxy(int port, HttpProtocols proto, string backendUrl, Version forwardVersion)
    {
        var routes = new[]
        {
            new RouteConfig
            {
                RouteId = "route0",
                ClusterId = "cluster0",
                Match = new RouteMatch { Path = "/{**catchall}" },
            },
        };
        var clusters = new[]
        {
            new ClusterConfig
            {
                ClusterId = "cluster0",
                Destinations = new System.Collections.Generic.Dictionary<string, DestinationConfig>
                {
                    ["dest0"] = new DestinationConfig { Address = backendUrl },
                },
                HttpRequest = new ForwarderRequestConfig
                {
                    Version = forwardVersion,
                    VersionPolicy = HttpVersionPolicy.RequestVersionExact,
                },
            },
        };

        return Host.CreateDefaultBuilder()
            .ConfigureLogging(b => b.ClearProviders().SetMinimumLevel(LogLevel.Warning))
            .ConfigureWebHostDefaults(web =>
            {
                web.UseKestrel(k => k.Listen(IPAddress.Loopback, port, lo => lo.Protocols = proto));
                web.ConfigureServices(services =>
                {
                    services.AddReverseProxy().LoadFromMemory(routes, clusters);
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(e => e.MapReverseProxy());
                });
            })
            .Build();
    }

    private static int FreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    private static int GetInt(string[] args, string name, int def)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name && int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
            {
                return v;
            }
        }
        return def;
    }

    private static string GetStr(string[] args, string name, string def)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name)
            {
                return args[i + 1];
            }
        }
        return def;
    }
}
