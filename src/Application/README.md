# YARP Container Application

A pre-built reverse proxy and static file host powered by [YARP](https://microsoft.github.io/reverse-proxy/). Configure it with JSON — no code required.

## Quick Start

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

```bash
# Run with a config file
yarp ./config.json

# Or use environment variables
StaticFiles__Enabled=true NavigationFallback__Path=/index.html yarp
```

## Container Usage

```yaml
services:
  yarp:
    image: yarp
    environment:
      - StaticFiles__Enabled=true
      - Compression__Enabled=true
      - NavigationFallback__Path=/index.html
    volumes:
      - ./config.json:/app/config.json
      - ./wwwroot:/app/wwwroot
    command: ["/app/config.json"]
```

## Configuration

Configuration is JSON-first, with environment variable support for simple toggles.
See [`yarp-config.schema.json`](yarp-config.schema.json) for the full schema with descriptions.

### Sections

| Section | Description |
|---|---|
| `StaticFiles` | Static file serving from wwwroot |
| `NavigationFallback` | SPA fallback for client-side routing |
| `Telemetry` | OpenTelemetry configuration |
| `ReverseProxy` | YARP reverse proxy routes and clusters |

### Static Files

```json
{
  "StaticFiles": {
    "Enabled": true
  }
}
```

### Navigation Fallback (SPA)

```json
{
  "NavigationFallback": {
    "Path": "/index.html"
  }
}
```

### Telemetry

OTLP export is configured via standard `OTEL_*` environment variables (e.g., `OTEL_EXPORTER_OTLP_ENDPOINT`).

```json
{
  "Telemetry": {
    "UnsafeAcceptAnyCertificate": true
  }
}
```

## Architecture

This is an opinionated, pre-built application — not an extensible framework.
Users who need custom behavior should use the [YARP library](https://microsoft.github.io/reverse-proxy/) directly in their own ASP.NET Core app.

### Project Structure

```
Configuration/          Config model (IConfiguration → strongly-typed POCOs)
  YarpAppConfig.cs      Root config object
  YarpAppConfigBinder.cs  Single IConfiguration → object model conversion
  *Options.cs           Per-feature options classes
  Rules.cs              HeaderRule, RedirectRule (shared match syntax)
Features/               Per-feature extension methods
  StaticFilesFeature.cs
  NavigationFallbackFeature.cs
  ReverseProxyFeature.cs
Program.cs              Pipeline ordering (explicit, one line per feature)
Extensions.cs           Service defaults (telemetry, health checks)
```

### Adding a Feature

1. Add options class: `Configuration/XxxOptions.cs`
2. Add property to `YarpAppConfig.cs`
3. Add bind line to `YarpAppConfigBinder.cs`
4. Add feature logic: `Features/XxxFeature.cs`
5. Add call to `Program.cs` in the correct pipeline position
6. Add section to `yarp-config.schema.json`

## Legacy Configuration

The following environment variables continue to work for backward compatibility:

| Legacy Key | Maps To |
|---|---|
| `YARP_ENABLE_STATIC_FILES` | `StaticFiles:Enabled` |
| `YARP_DISABLE_SPA_FALLBACK` | Disables `NavigationFallback:Path` |
| `YARP_UNSAFE_OLTP_CERT_ACCEPT_ANY_SERVER_CERTIFICATE` | `Telemetry:UnsafeAcceptAnyCertificate` |
