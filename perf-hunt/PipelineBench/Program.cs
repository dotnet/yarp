// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
// PipelineBench: in-process micro-benchmark of the full MapReverseProxy request pipeline.
//
// Goal: isolate YARP per-request pipeline cost (routing endpoint metadata lookup, pipeline
// initializer, session affinity, load balancing, passive health, limits, forwarder middleware)
// from Kestrel and from the network/backend. It does this by:
//   * Building the REAL public pipeline: UseRouting() -> UseEndpoints(e => e.MapReverseProxy()).
//   * Replacing IHttpForwarder with a stub so we measure the pipeline AROUND the forwarder,
//     not the forwarder internals (those were Phase 1 / direct-IHttpForwarder scope).
//   * Driving a fresh DefaultHttpContext through the built RequestDelegate on a single thread.
//   * Measuring exact allocations/request via GC.GetTotalAllocatedBytes(precise:true) (process-wide,
//     so thread-pool continuation allocations in async mode are also counted) and CPU/request via
//     Stopwatch, over N iterations x T trials (min + mean +/- sample stddev).
//
// Two modes per scenario:
//   SYNC : stub forwarder completes synchronously. Cleanest CPU signal; captures all non-async
//          allocations (feature object, feature store, lookups).
//   ASYNC: stub forwarder awaits Task.Yield() once, forcing every genuinely-async middleware frame
//          to suspend and heap-allocate its state machine + ExecutionContext capture, as happens in
//          production (real network I/O suspends). CPU here is dominated by the thread-pool hop and
//          is NOT a CPU signal -- only its allocation delta is meaningful.
//
// YARP-attributable cost = (scenario) - (routing-only baseline with the same route count). The
// baseline maps the identical route patterns to a trivial terminal endpoint, so DefaultHttpContext
// creation + ASP.NET routing cost cancels out.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO.Hashing;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Health;
using Yarp.ReverseProxy.LoadBalancing;
using Yarp.ReverseProxy.SessionAffinity;

namespace PipelineBench;

internal static class Program
{
    private static volatile int _sink;

    private static async Task<int> Main(string[] args)
    {
        var iters = GetArg(args, "--iters", 50_000);
        var trials = GetArg(args, "--trials", 12);
        var warmup = GetArg(args, "--warmup", 50_000);
        var label = GetArgString(args, "--label", "run");
        var only = GetArgString(args, "--only", null);

        Console.WriteLine($"# PipelineBench label={label}");
        Console.WriteLine($"runtime={System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
        Console.WriteLine($"os={System.Runtime.InteropServices.RuntimeInformation.OSDescription} arch={System.Runtime.InteropServices.RuntimeInformation.OSArchitecture}");
        Console.WriteLine($"gcServer={System.Runtime.GCSettings.IsServerGC} processors={Environment.ProcessorCount}");
        Console.WriteLine($"iters={iters} trials={trials} warmup={warmup}");
        Console.WriteLine();

        var scenarios = BuildScenarios();
        if (only is not null)
        {
            scenarios = scenarios.Where(s => s.Name.Contains(only, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        // Route-count -> routing-only baseline results (sync/async), built lazily.
        var results = new List<Result>();

        foreach (var scenario in scenarios)
        {
            Console.Error.WriteLine($"[running] {scenario.Name} ...");
            var (routes, clusters, reqPath, cookie) = BuildConfig(scenario);

            foreach (var forceAsync in new[] { false, true })
            {
                RequestDelegate pipeline;
                IServiceProvider provider;
                try
                {
                    (pipeline, provider) = BuildProxyPipeline(routes, clusters, forceAsync, scenario.Shape);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"FAILED to build pipeline for {scenario.Name} (async={forceAsync}):");
                    Console.WriteLine(ex);
                    return 2;
                }

                Func<HttpContext> makeCtx = () => MakeContext(provider, reqPath, cookie);

                // Correctness validation on first (sync) build.
                if (!forceAsync)
                {
                    var ok = await ValidateAsync(scenario.Name, pipeline, makeCtx, scenario);
                    if (!ok)
                    {
                        return 3;
                    }
                }

                var r = await MeasureAsync($"{scenario.Name}", forceAsync, makeCtx, pipeline, iters, trials, warmup);
                r.RouteCount = scenario.RouteCount;
                results.Add(r);
            }
        }

        // Routing-only baselines for each distinct route count present.
        var routeCounts = scenarios.Select(s => s.RouteCount).Distinct().OrderBy(x => x).ToList();
        var baselines = new Dictionary<(int, bool), Result>();
        foreach (var rc in routeCounts)
        {
            Console.Error.WriteLine($"[running] baseline routes={rc} ...");
            var (routes, _, reqPath, _) = BuildConfig(new Scenario($"baseline-routes{rc}", rc, 1, null, false, false, false));
            foreach (var forceAsync in new[] { false, true })
            {
                var (pipeline, provider) = BuildRoutingOnlyPipeline(routes, forceAsync);
                Func<HttpContext> makeCtx = () => MakeContext(provider, reqPath, null);
                var r = await MeasureAsync($"baseline-routes{rc}", forceAsync, makeCtx, pipeline, iters, trials, warmup);
                r.RouteCount = rc;
                r.IsBaseline = true;
                baselines[(rc, forceAsync)] = r;
                results.Add(r);
            }
        }

        PrintTable(results, baselines);
        return 0;
    }

    // ---- Scenario matrix -------------------------------------------------------------------

    private static List<Scenario> BuildScenarios() => new()
    {
        //           Name                 routes dest  lb                                     aff    affHit passive
        new Scenario("min-1r-1c-1d",           1,   1, null,                                  false, false, false),
        // Pipeline decomposition: same minimal config, different middleware chains, to attribute
        // the async-frame cost. Default = SA+LB+PassiveHealth; NoPH = SA+LB; Minimal = none.
        new Scenario("min-no-passivehealth",   1,   1, null,                                  false, false, false, PipelineShape.NoPassiveHealth),
        new Scenario("min-minimal-pipeline",   1,   1, null,                                  false, false, false, PipelineShape.Minimal),
        new Scenario("routes-100",           100,   1, null,                                  false, false, false),
        new Scenario("routes-1000",         1000,   1, null,                                  false, false, false),
        new Scenario("dest-8-roundrobin",      1,   8, LoadBalancingPolicies.RoundRobin,      false, false, false),
        new Scenario("dest-64-roundrobin",     1,  64, LoadBalancingPolicies.RoundRobin,      false, false, false),
        new Scenario("dest-8-p2c",             1,   8, LoadBalancingPolicies.PowerOfTwoChoices,false, false, false),
        new Scenario("dest-64-p2c",            1,  64, LoadBalancingPolicies.PowerOfTwoChoices,false, false, false),
        new Scenario("dest-5-healthyfilter",   1,   5, LoadBalancingPolicies.PowerOfTwoChoices,false, false, false),
        new Scenario("affinity-8-miss",        1,   8, LoadBalancingPolicies.PowerOfTwoChoices,true,  false, false),
        new Scenario("affinity-8-hit",         1,   8, LoadBalancingPolicies.PowerOfTwoChoices,true,  true,  false),
        new Scenario("passivehealth-1d",       1,   1, null,                                  false, false, true),
    };

    // ---- Config construction ---------------------------------------------------------------

    private static (RouteConfig[] routes, ClusterConfig[] clusters, string reqPath, string? cookie) BuildConfig(Scenario s)
    {
        var routes = new RouteConfig[s.RouteCount];
        var clusters = new ClusterConfig[s.RouteCount];

        for (var i = 0; i < s.RouteCount; i++)
        {
            var clusterId = "cluster" + i.ToString(CultureInfo.InvariantCulture);
            var path = s.RouteCount == 1 ? "/{**catchall}" : $"/svc{i}/{{**catchall}}";

            var dests = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase);
            for (var j = 0; j < s.DestCount; j++)
            {
                dests["dest" + j.ToString(CultureInfo.InvariantCulture)] =
                    new DestinationConfig { Address = $"http://127.0.0.1:5000/c{i}_d{j}" };
            }

            clusters[i] = new ClusterConfig
            {
                ClusterId = clusterId,
                LoadBalancingPolicy = s.LbPolicy,
                Destinations = dests,
                SessionAffinity = s.AffinityEnabled
                    ? new SessionAffinityConfig
                    {
                        Enabled = true,
                        Policy = SessionAffinityConstants.Policies.HashCookie,
                        FailurePolicy = SessionAffinityConstants.FailurePolicies.Redistribute,
                        AffinityKeyName = "yarp.affinity",
                    }
                    : null,
                HealthCheck = s.PassiveHealth
                    ? new HealthCheckConfig
                    {
                        Passive = new PassiveHealthCheckConfig
                        {
                            Enabled = true,
                            Policy = HealthCheckConstants.PassivePolicy.TransportFailureRate,
                            ReactivationPeriod = TimeSpan.FromSeconds(60),
                        },
                    }
                    : null,
            };

            routes[i] = new RouteConfig
            {
                RouteId = "route" + i.ToString(CultureInfo.InvariantCulture),
                ClusterId = clusterId,
                Match = new RouteMatch { Path = path },
            };
        }

        var reqPath = s.RouteCount == 1 ? "/" : $"/svc{s.RouteCount - 1}/x";

        // Affinity hit: reproduce HashCookieSessionAffinityPolicy.GetDestinationHash("dest0") using
        // only public inputs/APIs (XxHash64 of the upper-invariant destination id, hex, lowercased).
        string? cookie = null;
        if (s.AffinityEnabled && s.AffinityHit)
        {
            var hash = HashDestination("dest0");
            cookie = "yarp.affinity=" + hash;
        }

        return (routes, clusters, reqPath, cookie);
    }

    private static string HashDestination(string destinationId)
    {
        var bytes = Encoding.Unicode.GetBytes(destinationId.ToUpperInvariant());
        var hash = XxHash64.Hash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    // ---- Pipeline construction -------------------------------------------------------------

    private static (RequestDelegate, IServiceProvider) BuildProxyPipeline(
        RouteConfig[] routes, ClusterConfig[] clusters, bool forceAsync, PipelineShape shape)
    {
        var services = new ServiceCollection();
        AddCommonHostServices(services);
        services.AddReverseProxy().LoadFromMemory(routes, clusters);

        // Replace the real forwarder with a stub so we measure the pipeline, not forwarder internals.
        services.RemoveAll<IHttpForwarder>();
        services.AddSingleton<IHttpForwarder>(new StubForwarder(forceAsync));

        var provider = services.BuildServiceProvider();

        var app = new ApplicationBuilder(provider);
        app.UseRouting();
        app.UseEndpoints(endpoints =>
        {
            switch (shape)
            {
                case PipelineShape.Default:
                    // Exactly what the parameterless MapReverseProxy() installs.
                    endpoints.MapReverseProxy();
                    break;
                case PipelineShape.NoPassiveHealth:
                    endpoints.MapReverseProxy(proxy =>
                    {
                        proxy.UseSessionAffinity();
                        proxy.UseLoadBalancing();
                    });
                    break;
                case PipelineShape.Minimal:
                    endpoints.MapReverseProxy(proxy => { });
                    break;
            }
        });
        return (app.Build(), provider);
    }

    private static (RequestDelegate, IServiceProvider) BuildRoutingOnlyPipeline(RouteConfig[] routes, bool forceAsync)
    {
        var services = new ServiceCollection();
        AddCommonHostServices(services);
        services.AddRouting();
        var provider = services.BuildServiceProvider();

        RequestDelegate terminal = forceAsync
            ? static async ctx => { await Task.Yield(); ctx.Response.StatusCode = 200; }
            : static ctx => { ctx.Response.StatusCode = 200; return Task.CompletedTask; };

        var app = new ApplicationBuilder(provider);
        app.UseRouting();
        app.UseEndpoints(endpoints =>
        {
            foreach (var route in routes)
            {
                var pattern = string.IsNullOrEmpty(route.Match.Path) ? "/{**catchall}" : route.Match.Path!;
                endpoints.Map(pattern, terminal);
            }
        });
        return (app.Build(), provider);
    }

    private static void AddCommonHostServices(IServiceCollection services)
    {
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddMetrics();
        // EndpointRoutingMiddleware requires a DiagnosticListener (normally provided by the host).
        var listener = new DiagnosticListener("PipelineBench");
        services.AddSingleton(listener);
        services.AddSingleton<DiagnosticSource>(listener);
    }

    // ---- Request construction --------------------------------------------------------------

    private static HttpContext MakeContext(IServiceProvider provider, string path, string? cookie)
    {
        var ctx = new DefaultHttpContext();
        ctx.RequestServices = provider;
        var req = ctx.Request;
        req.Method = "GET";
        req.Scheme = "http";
        req.Host = new HostString("localhost");
        req.Path = path;
        req.Protocol = "HTTP/1.1";
        if (cookie is not null)
        {
            req.Headers.Cookie = cookie;
        }
        return ctx;
    }

    // ---- Measurement -----------------------------------------------------------------------

    private static async Task<Result> MeasureAsync(
        string name, bool forceAsync, Func<HttpContext> makeCtx, RequestDelegate pipeline,
        int iters, int trials, int warmup)
    {
        for (var i = 0; i < warmup; i++)
        {
            var ctx = makeCtx();
            await pipeline(ctx);
            _sink += ctx.Response.StatusCode;
        }

        var ns = new double[trials];
        var bytes = new double[trials];

        for (var t = 0; t < trials; t++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var alloc0 = GC.GetTotalAllocatedBytes(precise: true);
            var sw = Stopwatch.StartNew();
            for (var i = 0; i < iters; i++)
            {
                var ctx = makeCtx();
                await pipeline(ctx);
                _sink += ctx.Response.StatusCode;
            }
            sw.Stop();
            var alloc1 = GC.GetTotalAllocatedBytes(precise: true);

            ns[t] = sw.Elapsed.TotalNanoseconds / iters;
            bytes[t] = (double)(alloc1 - alloc0) / iters;
        }

        return new Result
        {
            Name = name,
            Async = forceAsync,
            NsPerReqMin = ns.Min(),
            NsPerReqMean = Mean(ns),
            NsPerReqStd = Std(ns),
            BytesPerReqMin = bytes.Min(),
            BytesPerReqMean = Mean(bytes),
            BytesPerReqStd = Std(bytes),
        };
    }

    private static async Task<bool> ValidateAsync(string name, RequestDelegate pipeline, Func<HttpContext> makeCtx, Scenario s)
    {
        try
        {
            var ctx = makeCtx();
            await pipeline(ctx);
            var status = ctx.Response.StatusCode;
            var endpoint = ctx.GetEndpoint();
            if (endpoint is null)
            {
                Console.WriteLine($"VALIDATION FAILED [{name}]: no endpoint matched (routing did not select a route).");
                return false;
            }
            if (status != 200)
            {
                Console.WriteLine($"VALIDATION FAILED [{name}]: expected status 200 from stub forwarder, got {status}. Endpoint='{endpoint.DisplayName}'.");
                return false;
            }
            Console.Error.WriteLine($"[validate] {name}: status={status} endpoint='{endpoint.DisplayName}' OK");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"VALIDATION EXCEPTION [{name}]:");
            Console.WriteLine(ex);
            return false;
        }
    }

    // ---- Output ----------------------------------------------------------------------------

    private static void PrintTable(List<Result> results, Dictionary<(int, bool), Result> baselines)
    {
        Console.WriteLine();
        Console.WriteLine("## Raw results (min of trials; mean +/- sample stddev)");
        Console.WriteLine();
        Console.WriteLine("| scenario | mode | ns/req min | ns/req mean±sd | B/req min | B/req mean±sd |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|");
        foreach (var r in results)
        {
            var mode = r.Async ? "async" : "sync";
            Console.WriteLine(
                $"| {r.Name} | {mode} | {r.NsPerReqMin,8:F1} | {r.NsPerReqMean,8:F1}±{r.NsPerReqStd,-5:F1} | " +
                $"{r.BytesPerReqMin,7:F1} | {r.BytesPerReqMean,7:F1}±{r.BytesPerReqStd,-4:F1} |");
        }

        Console.WriteLine();
        Console.WriteLine("## YARP-attributable delta vs routing-only baseline (same route count)");
        Console.WriteLine("(delta = scenario - baseline; isolates the proxy middleware chain from ASP.NET routing + DefaultHttpContext.)");
        Console.WriteLine();
        Console.WriteLine("| scenario | mode | Δ ns/req | Δ B/req |");
        Console.WriteLine("|---|---|--:|--:|");
        foreach (var r in results.Where(r => !r.IsBaseline))
        {
            if (!baselines.TryGetValue((r.RouteCount, r.Async), out var b))
            {
                continue;
            }
            var dns = r.NsPerReqMin - b.NsPerReqMin;
            var db = r.BytesPerReqMin - b.BytesPerReqMin;
            var mode = r.Async ? "async" : "sync";
            Console.WriteLine($"| {r.Name} | {mode} | {dns,8:F1} | {db,7:F1} |");
        }

        // Highlight the async-only extra (async delta minus sync delta) for the ubiquitous min path:
        // this quantifies the state-machine/ExecutionContext overhead the proxy chain adds over a
        // single-frame endpoint.
        Console.WriteLine();
        Console.WriteLine("## Async-frame overhead (proxy chain extra async cost over baseline)");
        Console.WriteLine("(= (scenarioAsync - scenarioSync) - (baselineAsync - baselineSync); ~ heap state machines + EC captures the proxy chain adds)");
        Console.WriteLine();
        Console.WriteLine("| scenario | Δ async-frame B/req |");
        Console.WriteLine("|---|--:|");
        var byName = results.Where(r => !r.IsBaseline).GroupBy(r => r.Name);
        foreach (var g in byName)
        {
            var sync = g.FirstOrDefault(r => !r.Async);
            var asyncR = g.FirstOrDefault(r => r.Async);
            if (sync is null || asyncR is null)
            {
                continue;
            }
            if (!baselines.TryGetValue((sync.RouteCount, false), out var bs) ||
                !baselines.TryGetValue((sync.RouteCount, true), out var ba))
            {
                continue;
            }
            var extra = (asyncR.BytesPerReqMin - sync.BytesPerReqMin) - (ba.BytesPerReqMin - bs.BytesPerReqMin);
            Console.WriteLine($"| {g.Key} | {extra,7:F1} |");
        }

        Console.WriteLine();
        Console.WriteLine($"(sink={_sink})");
    }

    // ---- helpers ---------------------------------------------------------------------------

    private static double Mean(double[] xs) => xs.Average();

    private static double Std(double[] xs)
    {
        if (xs.Length < 2)
        {
            return 0;
        }
        var m = xs.Average();
        var s = xs.Sum(x => (x - m) * (x - m));
        return Math.Sqrt(s / (xs.Length - 1));
    }

    private static int GetArg(string[] args, string name, int def)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name && int.TryParse(args[i + 1], out var v))
            {
                return v;
            }
        }
        return def;
    }

    private static string? GetArgString(string[] args, string name, string? def)
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

    private sealed class Scenario
    {
        public Scenario(string name, int routeCount, int destCount, string? lbPolicy, bool affinityEnabled, bool affinityHit, bool passiveHealth, PipelineShape shape = PipelineShape.Default)
        {
            Name = name;
            RouteCount = routeCount;
            DestCount = destCount;
            LbPolicy = lbPolicy;
            AffinityEnabled = affinityEnabled;
            AffinityHit = affinityHit;
            PassiveHealth = passiveHealth;
            Shape = shape;
        }

        public string Name { get; }
        public int RouteCount { get; }
        public int DestCount { get; }
        public string? LbPolicy { get; }
        public bool AffinityEnabled { get; }
        public bool AffinityHit { get; }
        public bool PassiveHealth { get; }
        public PipelineShape Shape { get; }
    }

    private enum PipelineShape
    {
        Default,
        NoPassiveHealth,
        Minimal,
    }

    private sealed class Result
    {
        public string Name { get; set; } = "";
        public bool Async { get; set; }
        public bool IsBaseline { get; set; }
        public int RouteCount { get; set; }
        public double NsPerReqMin { get; set; }
        public double NsPerReqMean { get; set; }
        public double NsPerReqStd { get; set; }
        public double BytesPerReqMin { get; set; }
        public double BytesPerReqMean { get; set; }
        public double BytesPerReqStd { get; set; }
    }

    private sealed class StubForwarder : IHttpForwarder
    {
        private readonly bool _forceAsync;

        public StubForwarder(bool forceAsync) => _forceAsync = forceAsync;

        public async ValueTask<ForwarderError> SendAsync(
            HttpContext context, string destinationPrefix, HttpMessageInvoker httpClient,
            ForwarderRequestConfig requestConfig, HttpTransformer transformer)
        {
            if (_forceAsync)
            {
                await Task.Yield();
            }
            context.Response.StatusCode = 200;
            return ForwarderError.None;
        }
    }
}
