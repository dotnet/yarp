# YARP Container Application

An opinionated reverse proxy built on ASP.NET Core and [YARP](https://microsoft.github.io/reverse-proxy/). JSON config, no code required.

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

```
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
      - ./yarp-config.json:/app/config.json
      - ./wwwroot:/app/wwwroot
    command: ["/app/config.json"]
```

Simple toggles work as environment variables. Complex config (proxy routes, etc.) goes in the JSON file.

## Configuration

All configuration goes through `IConfiguration` — JSON files, environment variables, or any other provider. See [`yarp-config.schema.json`](yarp-config.schema.json) for IDE autocomplete and validation.

### `StaticFiles`

Serve static files from `wwwroot/`.

```json
{ "StaticFiles": { "Enabled": true } }
```

### `NavigationFallback`

SPA fallback — serve a file (typically `index.html`) for unmatched routes so client-side routing works.

```json
{ "NavigationFallback": { "Path": "/index.html" } }
```

### `ReverseProxy`

YARP reverse proxy routes and clusters. See the [YARP configuration docs](https://microsoft.github.io/reverse-proxy/articles/config-files.html) for the full reference.

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

This is an opinionated, pre-built application — not an extensible framework. Users who need custom behavior should use the [YARP library](https://microsoft.github.io/reverse-proxy/) directly in their own ASP.NET Core app.

### Project Structure

```
Configuration/                  Config model (IConfiguration → POCOs)
  YarpAppConfig.cs              Root config object
  YarpAppConfigBinder.cs        Single conversion point + legacy key mapping
  StaticFilesOptions.cs         Per-feature options
  NavigationFallbackOptions.cs
  TelemetryOptions.cs
Features/                       Per-feature extension methods
  StaticFilesFeature.cs
  NavigationFallbackFeature.cs
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
|---|---|
| `YARP_ENABLE_STATIC_FILES` | `StaticFiles:Enabled` |
| `YARP_DISABLE_SPA_FALLBACK` | Disables `NavigationFallback:Path` |
| `YARP_UNSAFE_OLTP_CERT_ACCEPT_ANY_SERVER_CERTIFICATE` | `Telemetry:UnsafeAcceptAnyCertificate` |
