# perf-hunt — YARP `MapReverseProxy` pipeline perf harness

Durable micro/E2E benchmarks used to find and guard a per-request allocation win in the full
`MapReverseProxy` request pipeline (routing → pipeline initializer → session affinity → load
balancing → passive health → limits → forwarder).

These projects are **intentionally isolated** from the product build:

* Not referenced by `YARP.slnx`, so `./build.sh` / `dotnet build YARP.slnx` never build them.
* A local `Directory.Build.props`/`Directory.Build.targets` stops the Arcade walk-up, so the harness
  builds as a plain SDK project. Each project `ProjectReference`s `src/ReverseProxy` (built in its own
  Arcade context).

Build/run with the repo-pinned SDK: `./.dotnet/dotnet run -c Release --project perf-hunt/<proj> -- <args>`.

## PipelineBench (allocation + CPU A/B, in-process)

Drives a fresh `DefaultHttpContext` through the **real** public pipeline
(`UseRouting()` → `UseEndpoints(e => e.MapReverseProxy())`) with `IHttpForwarder` replaced by a stub,
so it measures the YARP pipeline **around** the forwarder (not forwarder internals). It reports exact
allocations/request via `GC.GetTotalAllocatedBytes(precise:true)` and ns/request via `Stopwatch`
(min + mean ± sample stddev over T trials), in two modes:

* **sync** — stub completes synchronously (clean CPU signal; non-async allocations).
* **async** — stub `await Task.Yield()` once, forcing the genuinely-async frames to suspend and
  heap-allocate their state machines, as happens in production (network I/O suspends).

YARP-attributable cost = scenario − routing-only baseline (same route count), which cancels
`DefaultHttpContext` creation + ASP.NET routing. Scenarios cover: 1 / 100 / 1000 routes; 8 / 64
destinations; round-robin and power-of-two; session affinity hit/miss; healthy-destination filtering;
passive health enabled; plus pipeline-decomposition shapes (`min-no-passivehealth`, `min-minimal`).

```bash
./.dotnet/dotnet run -c Release --project perf-hunt/PipelineBench -- --iters 50000 --trials 12 --warmup 50000
```

## E2EBench (throughput + latency, real Kestrel)

One process per measurement: a real Kestrel backend, optionally a real YARP proxy
(`MapReverseProxy` + `LoadFromMemory`), and an in-process async load client. `--target direct` isolates
framework/Kestrel cost; `--target proxy` adds YARP. Supports HTTP/1.1 and HTTP/2 (h2c). Reports req/s,
latency percentiles, process-wide allocations/request, working set, and GC counts. Because the client
and backend are byte-identical across a before/after comparison, the before−after allocation delta
attributes to the proxy.

```bash
./.dotnet/dotnet run -c Release --project perf-hunt/E2EBench -- --target proxy --http 1 --duration 10 --warmup 4 --connections 32
```

## Finding this harness produced

`PassiveHealthCheckMiddleware.Invoke` was an `async` method that always awaited `_next`, forcing a
**~112 B/request async state-machine allocation on every proxied request through the default pipeline**,
even though passive health checks are **disabled by default**. Short-circuiting to `return _next(context)`
when passive health is not engaged (read at entry, like `SessionAffinityMiddleware`/`LimitsMiddleware`)
removes it; outcome recording still re-reads the cluster/destination after `_next`, preserving
`ReassignProxyRequest` semantics.

Measured on Apple M-series (arm64), .NET 9.0.2:

| Measurement | Before | After | Δ |
| --- | ---: | ---: | ---: |
| PipelineBench `min-1r-1c-1d` async B/req | 1856 | 1744 | **−112** |
| …all default-pipeline shapes (routes/dests/LB/affinity) | — | — | **−112** each |
| PipelineBench `passivehealth-1d` (enabled) | 1856 | 1856 | 0 (unchanged) |
| E2E proxy HTTP/1.1 alloc/req | ~4372 | ~4259 | **−112** |
| E2E proxy HTTP/2 alloc/req | ~5900 | ~5795 | **−106** |
| E2E proxy HTTP/1.1 & HTTP/2 throughput/latency | — | — | flat (no regression) |
