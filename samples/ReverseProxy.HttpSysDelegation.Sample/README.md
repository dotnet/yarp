# Http.sys Delegation Sample
This sample shows how to use YARP to delegate requests to other Http.sys request queues instead of or in addition to proxying requests. Using Http.sys delegation requires hosting YARP on [ASP.NET Core's Http.sys server](https://docs.microsoft.com/aspnet/core/fundamentals/servers/httpsys) and requests can only be delegated to other processes which use Http.sys for request processing (e.g. ASP.NET Core using Http.sys server or IIS).

**Note:** delegation only works for ASP.NET Core 6+ running on new versions of Windows

## Sample Projects
There are two projects as part of this sample. A sample Http.sys server where traffic will be delegated to and a YARP example which both proxies and delegates request depending on the route. Both projects use the minimal API style but this isn't a requirement.

### ReverseProxy Delegation

There are four parts to enable YARP delegation support:
- Use the ASP.NET Core Http.sys server
```c#
builder.WebHost.UseHttpSys();
```
- Add YARP services
```c#
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
```
- Add YARP to the request pipeline. 

You need to use the overload that allows you to define the middleware used in the YARP pipeline. 
```c#
app.MapReverseProxy(proxyPipeline =>
{
    // Add the three middleware YARP adds by default plus the Http.sys delegation middleware
    proxyPipeline.UseSessionAffinity(); // Has no affect on delegation destinations because the response doesn't go through YARP
    proxyPipeline.UseLoadBalancing();
    proxyPipeline.UsePassiveHealthChecks();
    proxyPipeline.UseHttpSysDelegation();
});
```
- Add a ReverseProxy section to appsettings.json. 

Configuration is almost identical to how YARP in typically configured. The only difference is, for destinations which should use delegation, they have metadata which indicates the Http.sys queue name to delegate the request to.
```json
"Destinations": {
  "SampleHttpSysServer": {
    "Address": "http://localhost:5600/",
    "Metadata": {
      "HttpSysDelegationQueue": "SampleHttpSysServerQueue"
    }
  }
}
```

## Usage
To run the sample:
1. Start the SampleHttpSysServer project `dotnet run --project SampleHttpSysServer\SampleHttpSysServer.csproj`
2. Start the ReverseProxy.HttpSysDelegation.Sample project `dotnet run --project ReverseProxy\ReverseProxy.HttpSysDelegation.Sample.csproj`

By default, the SampleHttpSysServer will listen to http://localhost:5600 and the ReverseProxy will listen to http://localhost:5500. The ReverseProxy will delegate any requests under the path http://localhost:5500/delegate to the SampleHttpSysServer. Any other path will be proxied to https://httpbin.org/.
