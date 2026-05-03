# YARP Container Application

An opinionated web server and reverse proxy built on ASP.NET Core and [YARP](https://dotnet.github.io/yarp/). JSON config, no code required.

## Quick Start

Create a `yarp-config.json` next to your `wwwroot/` directory:

```json
{
  "$schema": "./yarp-config.schema.json",

  "StaticFiles": {
    "Enabled": true
  },
  "NavigationFallback": {
    "Path": "/index.html"
  },
  "ReverseProxy": {
    "Routes": {
      "api": {
        "ClusterId": "backend",
        "Match": { "Path": "/api/{**catch-all}" }
      }
    },
    "Clusters": {
      "backend": {
        "Destinations": {
          "d1": { "Address": "http://backend:5000" }
        }
      }
    }
  }
}
```

Run it:

```bash
yarp ./yarp-config.json
```

```text
YARP

  Config:         ./yarp-config.json
  Static files:   ./wwwroot
  SPA fallback:   /index.html

  Listening on:   http://localhost:5000
```

## Container Usage

```yaml
services:
  yarp:
    image: yarp
    environment:
      - StaticFiles__Enabled=true
      - NavigationFallback__Path=/index.html
    volumes:
      - ./yarp-config.json:/etc/yarp-config.json
      - ./wwwroot:/etc/wwwroot
    command: ["/etc/yarp-config.json"]
    ports:
      - "5000:5000"
```

Mount the config file and `wwwroot/` into the same directory. The app uses the config file directory as the content root and serves static files from its `wwwroot/` subdirectory.

Simple toggles work as environment variables. Complex config (proxy routes, etc.) goes in the JSON file.

## Configuration

All configuration goes through `IConfiguration` — JSON files, environment variables, or any other provider. See [`yarp-config.schema.json`](yarp-config.schema.json) for IDE autocomplete and validation.

### Request pipeline

Every request flows through the pipeline below in this fixed order. Knowing the order is usually enough to reason about which feature wins for a given URL.

```text
┌─────────────────────────────────────────┐
│ 1. Rewrites           (pre-routing)     │  Regex-based path rewrite. Mutates Request.Path.
├─────────────────────────────────────────┤
│ 2. Error pages        (wraps everything)│  Re-execute against a configured page on 4xx/5xx.
├─────────────────────────────────────────┤
│ 3. Routing match      (endpoint chosen) │  ASP.NET selects an endpoint, but doesn't run it yet.
├─────────────────────────────────────────┤
│ 4. Redirects          (short-circuit)   │  If a redirect endpoint matched, send 30x and stop.
├─────────────────────────────────────────┤
│ 5. Static files       (special)         │  If a file at Request.Path exists in wwwroot, serve it.
├─────────────────────────────────────────┤
│ 6. Headers            (response phase)  │  Apply Header rules to static-file & SPA-fallback responses.
├─────────────────────────────────────────┤
│ 7. Reverse proxy      (endpoint)        │  YARP routes that matched in step 3 run here.
├─────────────────────────────────────────┤
│ 8. Fallback exclude   (endpoint)        │  Listed paths return 404 instead of falling back.
├─────────────────────────────────────────┤
│ 9. SPA fallback       (endpoint)        │  Otherwise serve NavigationFallback.Path (e.g. index.html).
└─────────────────────────────────────────┘
```

A few consequences worth internalizing:

- **Rewrites run first**, so every later stage sees the rewritten path. Use them to canonicalize URLs before anything else makes a decision.
- **Redirects beat static files and the proxy.** If a redirect rule matches, the response is a 30x — the file or upstream is never consulted.
- **Static files beat fallback exclusions and the SPA fallback.** A real file in `wwwroot` always wins over routed fallback endpoints, even though those fallbacks were chosen by `UseRouting` first. (This is preserved by clearing/restoring the selected endpoint around `UseFileServer`.)
- **`Headers` only apply to static-file and SPA-fallback responses** — not to redirects, not to proxy responses. Use YARP response transforms for proxy headers.
- **Fallback exclusions and the SPA fallback are real routed endpoints**, so reverse-proxy routes (and any `MapGet`/`MapPost` registered earlier) can still claim a path before either fires.

### Match syntax

Routed features (`Headers`, `Redirects`, `NavigationFallback.Exclude`) match using ASP.NET route templates — `/blog/{slug}`, `/api/{**catch-all}`. The same engine that powers `MapGet`. Captures from `Match.Path` are available in `Destination` as `{name}` substitutions.

`Rewrites` use a different syntax — regex with `$n` capture groups — because they delegate to the standard ASP.NET [URL rewrite middleware](https://learn.microsoft.com/aspnet/core/fundamentals/url-rewriting). This is intentional: routed features are routes (so they use route-template syntax), and rewrites are rewrites (so they use the existing rewrite syntax). No new syntax is introduced.

### `StaticFiles`

Serve static files from `wwwroot/`.

```json
{ "StaticFiles": { "Enabled": true } }
```

### `NavigationFallback`

SPA fallback — serve a file (typically `index.html`) for unmatched routes so client-side routing works.

```json
{
  "NavigationFallback": {
    "Path": "/index.html",
    "Exclude": [
      { "Path": "/api/{**catch-all}" },
      { "Path": "/.well-known/{**catch-all}" }
    ]
  }
}
```

### `Headers`

Response header rules for static-file and SPA-fallback responses. All matching rules are applied.

```json
{
  "Headers": [
    {
      "Match": {
        "Path": "/{**path}"
      },
      "Set": {
        "X-Content-Type-Options": "nosniff"
      }
    },
    {
      "Match": {
        "Path": "/_astro/{**path}"
      },
      "Set": {
        "Cache-Control": "public, max-age=31536000, immutable"
      }
    }
  ]
}
```

### `Redirects`

Declarative redirects. Rules are evaluated in order and the first match wins.

```json
{
  "Redirects": [
    {
      "Match": {
        "Path": "/old-page"
      },
      "Destination": "/new-page",
      "StatusCode": 301
    },
    {
      "Match": {
        "Path": "/install.sh"
      },
      "Destination": "https://aka.ms/install.sh",
      "StatusCode": 302
    }
  ]
}
```

`Destination` can reference route values captured by `Match.Path`, such as `{slug}`.

### `Rewrites`

URL rewrites applied **before routing**, so every downstream stage (routing, static files, redirects, SPA fallback, reverse proxy) sees the rewritten path. Uses the standard [ASP.NET rewrite middleware](https://learn.microsoft.com/aspnet/core/fundamentals/url-rewriting) — regex pattern + `$n` capture-group substitution. By default, the first matching rule wins.

```json
{
  "Rewrites": [
    {
      "Regex": "^blog/(.*)$",
      "Replacement": "posts/$1"
    },
    {
      "Regex": "^legacy/(.*)$",
      "Replacement": "$1"
    }
  ]
}
```

Set `SkipRemainingRules: false` to allow subsequent rules to also evaluate against the rewritten path.

### `ErrorPages`

Custom error pages for 4xx/5xx responses. The request is internally re-executed against the configured path while the original status code is preserved (the browser still sees `404`, `500`, etc.). Built on top of ASP.NET's [`UseStatusCodePages`](https://learn.microsoft.com/aspnet/core/fundamentals/error-handling#usestatuscodepageswithreexecute) — only fires when the response has not started and the body is empty.

```json
{
  "ErrorPages": {
    "404": "/404.html",
    "503": "/maintenance.html",
    "4xx": "/client-error.html",
    "5xx": "/server-error.html"
  }
}
```

Keys are either a 3-digit HTTP status code (`"404"`) or a class wildcard (`"4xx"`, `"5xx"`). Values must be rooted request paths that start with `/`. **Exact codes win over wildcards**, so in the example above:

| Status | Page |
| --- | --- |
| 400, 403 | `/client-error.html` |
| 404 | `/404.html` |
| 500, 502 | `/server-error.html` |
| 503 | `/maintenance.html` |

### `ReverseProxy`

YARP reverse proxy routes and clusters. See the [YARP configuration docs](https://learn.microsoft.com/aspnet/core/fundamentals/servers/yarp/config-files) for the full reference.

### `Telemetry`

OTLP export uses standard `OTEL_*` environment variables. This section covers YARP-specific telemetry options.

```json
{ "Telemetry": { "UnsafeAcceptAnyCertificate": true } }
```

## Logging

By default, the console shows a clean startup banner and warnings/errors only — no per-request noise. Framework logs (DataProtection, Hosting.Lifetime, etc.) are suppressed on console but still flow to other providers (OTEL).

To re-enable framework logs for debugging, use the standard `Logging` config:

```json
{
  "Logging": {
    "Console": {
      "LogLevel": {
        "Microsoft.AspNetCore": "Information"
      }
    }
  }
}
```

## Architecture

This is an opinionated, pre-built application — not an extensible framework. Users who need custom behavior should use the [YARP library](https://dotnet.github.io/yarp/) directly in their own ASP.NET Core app.

### Project Structure

```text
Configuration/                  Config model (IConfiguration → POCOs)
  YarpAppConfig.cs              Root config object
  YarpAppConfigBinder.cs        Single conversion point + legacy key mapping
  RequestMatch.cs               Shared route-style match object
  StaticFilesOptions.cs         Per-feature options
  NavigationFallbackOptions.cs
  HeaderRule.cs
  RedirectRule.cs
  RewriteRule.cs
  TelemetryOptions.cs
Features/                       Per-feature extension methods
  RequestMatchEvaluator.cs      Route-template-based match evaluation
  StaticFilesFeature.cs
  NavigationFallbackFeature.cs
  NavigationFallbackExclusionsFeature.cs
  RedirectsFeature.cs
  RewritesFeature.cs
  ErrorPagesFeature.cs
  StaticHostHeadersFeature.cs
  ReverseProxyFeature.cs
  LoggingFeature.cs
Program.cs                      Pipeline ordering
Extensions.cs                   Service defaults (telemetry, health checks)
yarp-config.schema.json         JSON Schema for IDE support
```

### Adding a Feature

1. Add options class: `Configuration/XxxOptions.cs`
2. Add property to `YarpAppConfig.cs`
3. Add bind line to `YarpAppConfigBinder.cs`
4. Add feature logic: `Features/XxxFeature.cs`
5. Add call to `Program.cs` in the correct pipeline position
6. Add section to `yarp-config.schema.json`

## Legacy Environment Variables

These continue to work for backward compatibility:

| Legacy Key | Maps To |
| --- | --- |
| `YARP_ENABLE_STATIC_FILES` | `StaticFiles:Enabled` |
| `YARP_DISABLE_SPA_FALLBACK` | Disables `NavigationFallback:Path` |
| `YARP_UNSAFE_OLTP_CERT_ACCEPT_ANY_SERVER_CERTIFICATE` | `Telemetry:UnsafeAcceptAnyCertificate` |
