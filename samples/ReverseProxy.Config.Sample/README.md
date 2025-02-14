# Configuration Sample

This sample shows off the properties that can be supplied to YARP via configuration. In this case its using appsettings.json, but the same configuration properties could be supplied through code instead. See [ReverseProxy.Code.Sample](../ReverseProxy.Code.Sample) for an example

The [configuration file](appsettings.json) includes all the settings that are currently supported by YARP.

The configuration shows two routes and two clusters:
- minimalRoute which will map to any URL
  - Routes to cluster "minimalCluster" which has one destination "www.example.com"
- allRouteProps route
  - Which includes further restrictions:
    - Path must be /download/*
    - Host must be localhost, www.aaaaa.com or www.bbbbb.com
    - Http Method must be GET or POST
    - Must have a header "MyCustomHeader" with a value of "value1", "value2" or "another value"
    - A "MyHeader" header will be added with the value "MyValue"
    - Must have a query parameter "MyQueryParameter" with a value of "value1", "value2" or "another value"
  - This will route to cluster "allClusterProps" which has 2 destinations - https://dotnet.microsoft.com and https://10.20.30.40 
    - Requests will be [load balanced](https://learn.microsoft.com/aspnet/core/fundamentals/servers/yarp/load-balancing) between destinations using a "PowerOfTwoChoices" algorithm, which picks two destinations at random, then uses the least loaded of the two.
    - It includes [session affinity](https://learn.microsoft.com/aspnet/core/fundamentals/servers/yarp/session-affinity) using a cookie which will ensure subsequent requests from the same client go to the same host.
    - It is configured to have both active and passive [health checks](https://learn.microsoft.com/aspnet/core/fundamentals/servers/yarp/dests-health-checks) - note the second destination will timeout for active checks (unless you have a host with that IP on your network)
    - It includes [HttpClient configuration](https://learn.microsoft.com/aspnet/core/fundamentals/servers/yarp/http-client-config) setting outbound connection properties
    - HttpRequest properties defaulting to HTTP/2 with a 2min timout

The other files in the sample are the same as the getting started instructions.

To make a request that would be successful against the second route, you will need a client request similar to:

```
curl -v -k -X GET -H "MyCustomHeader: value1" https://localhost:5001/download?MyQueryParameter=value1
```

