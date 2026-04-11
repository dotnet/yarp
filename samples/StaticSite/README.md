# Static Site Sample

A minimal static site served by the YARP container application.

## Run

```bash
# From the repo root
dotnet run --project src/Application/Yarp.Application.csproj -- samples/StaticSite/yarp-config.json
```

Then open http://localhost:5000

## What's in the box

```
wwwroot/
  index.html                        # Home page
  404.html                          # Custom error page
  _astro/main.a1b2c3.css            # Hashed asset (simulates Astro build output)
  docs/getting-started/index.html   # Nested page with directory default document
yarp-config.json                    # YARP container configuration
```
