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

YARP reverse proxy routes and clusters. See the [YARP configuration docs](https://learn.microsoft.com/aspnet/core/fundamentals/servers/yarp/config-files) for the full reference.

### `Telemetry`

OTLP export uses standard `OTEL_*` environment variables. This section covers YARP-specific telemetry options.

```json
{ "Telemetry": { "UnsafeAcceptAnyCertificate": true } }
```

### `Log`

Controls console output. Set it in the config file or with the `Log__Level` environment variable.

```json
{ "Log": { "Level": "requests" } }
```

| Level | Behavior |
|---|---|
| `default` | Startup banner and warnings/errors. No per-request output. A failed proxy request is reported as one condensed line instead of the full stack trace. |
| `requests` | `default` plus one line per request: method, path (no query string), what handled it (`static`, `proxy`, `fallback`, or blank when nothing did), status, content length when known, and duration. |
| `debug` | Full framework logging, one line per request, and the resolved application config (`StaticFiles`, `NavigationFallback`, `Telemetry`, `Log`) as JSON at startup. The `ReverseProxy` section is not printed. |

Invalid values (including numbers) stop the app at startup with an error.

```text
GET  /                        → static   200  510b   3ms
GET  /api/users               → proxy    200         45ms
GET  /dashboard               → fallback 200  510b   2ms
GET  /missing.js              →          404          0ms
GET  /api/users               → proxy    502         74ms  route=api dest=http://localhost:5100/ Request: Connection refused (localhost:5100)
```

Request and error lines are written straight to the console, like the startup banner. They are not `ILogger` events, so the `Logging` settings below don't filter them.

## Logging

By default, the console shows a clean startup banner and warnings/errors only — no per-request noise. Framework logs (DataProtection, Hosting.Lifetime, etc.) are suppressed on console but still flow to other providers (OTEL). The verbose forwarding error from `HttpForwarder` is also hidden on the console in `default` and `requests` modes, because the condensed line replaces it; other providers still receive it.

To re-enable framework logs for debugging, use `"Log": { "Level": "debug" }` or the standard `Logging:LogLevel` / `Logging:Console:LogLevel` config. A category entry overrides the console defaults above when it names the same category or a more specific one (for example `Microsoft.AspNetCore`); as usual, a parent such as `Microsoft` or `Default` does not override a more specific default, and `Logging:Console:LogLevel` takes precedence over `Logging:LogLevel`. Setting `Logging:Console:FormatterName` keeps your formatter and the standard forwarding error log:

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
  StaticFilesOptions.cs         Per-feature options
  NavigationFallbackOptions.cs
  TelemetryOptions.cs
  LogOptions.cs                 Log.Level (LogMode)
  YarpAppConfigJsonContext.cs   Source-generated JSON for the debug config dump
Features/                       Per-feature extension methods
  StaticFilesFeature.cs
  NavigationFallbackFeature.cs
  ReverseProxyFeature.cs
  LoggingFeature.cs
  RequestLoggingFeature.cs
  CondensedConsoleFormatter.cs
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
