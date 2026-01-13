### Crank command to test against a local BenchmarkServer

1. Follow the [Crank Getting Started Guide](https://github.com/dotnet/crank/blob/master/docs/getting_started.md) to install Microsoft.Crank.Controller and Microsoft.Crank.Agent globally.
2. In one shell, run `crank-agent`
3. In another shell, run `crank` as follows:

```bash
crank `
     --config https://raw.githubusercontent.com/aspnet/Benchmarks/master/scenarios/proxy.benchmarks.yml `
     --scenario proxy-yarp `
     --profile local `
     --load.variables.duration 5 `
     --variable path=/?s=1024 `
     --variable serverScheme=https `
     --variable downstreamScheme=https `
     --load.variables.transport http2 `
     --downstream.variables.httpProtocol http2
```
