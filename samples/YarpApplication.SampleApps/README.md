# YARP Application sample apps

These samples are small, realistic app layouts for the YARP application static-hosting and routing-rule features. They are not .NET projects; each sample is just an `appsettings.json` file plus any static `wwwroot` assets it needs.

Run any sample from the repository root:

```bash
export DOTNET_ROOT="$PWD/.dotnet"
export DOTNET_MULTILEVEL_LOOKUP=0
export PATH="$PWD/.dotnet:$PATH"

ASPNETCORE_URLS=http://127.0.0.1:5000 \
  dotnet run --no-launch-profile --project src/Application/Yarp.Application.csproj -- \
  samples/YarpApplication.SampleApps/01-marketing-site/appsettings.json
```

Then open `http://127.0.0.1:5000`. Samples that include `ReverseProxy` use realistic placeholder backend addresses such as `http://catalog-api:8080/`; point those at your own local services when trying them.

| Sample app | Demonstrates |
| --- | --- |
| `01-marketing-site` | A static marketing site with SPA-style campaign fallback and long-lived asset caching headers. |
| `02-docs-site` | A documentation site with old URL redirects, "current docs" rewrites, and docs-specific headers. |
| `03-dashboard-spa` | A dashboard SPA that falls back to `index.html` while forwarding `/api` traffic to a backend. |
| `04-commerce-errors` | A commerce frontend with branded exact and wildcard custom error pages. |
| `05-edge-composition` | A composed edge frontend using rewrites, redirects, static assets, proxy routes, SPA fallback exclusions, and custom error pages together. |

