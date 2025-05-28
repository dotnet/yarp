# Config based proxy apps

> [!CAUTION]
> These are archived design discussions. Information may be outdated and inaccurate.

RE: https://github.com/dotnet/yarp/issues/9

Config based proxies are common and we'll need to support at least basic proxy scenarios from config. Here are some initial considerations:

- Config sources and systems
- Define routes based on host and/or path
- List multiple back-ends per route for load balancing
- A restart should not be needed to pick up config changes
- You should be able to augment a route's configuration in code. Kestrel does something similar using named endpoints.

## Config systems:

We have three relevant components that already have config systems: Kestrel, UrlRewrite, and ReverseProxy.

- [Kestrel](https://learn.microsoft.com/aspnet/core/fundamentals/servers/kestrel/endpoints)
- [UrlRewrite](https://github.com/dotnet/aspnetcore/blob/f4d81e3af2b969744a57d76d4d622036ac514a6a/src/Middleware/Rewrite/sample/UrlRewrite.xml#L1-L11)
- [ReverseProxy](https://github.com/dotnet/yarp/blob/b2cf5bdddf7962a720672a75f2e93913d16dfee7/samples/IslandGateway.Sample/appsettings.json#L10-L34)

Proposals:
- The Kestrel config and the Proxy/Gateway config should remain adjacent, not merged. Inbound and outbound are distinct concerns. As long as both are available in the same broader config system then that's close enough.
- UrlRewrite should also remain as is. It's not ideal that it's in a separate file and format from the rest of the config, but we'll wait and see if that is a long term blocker.

## Route config:

The proxy has a [config mechanism](https://github.com/dotnet/yarp/blob/b2cf5bdddf7962a720672a75f2e93913d16dfee7/samples/IslandGateway.Sample/appsettings.json#L26-L32) to define routes and map those to back end groups.
```
      "Routes": [
        {
          "RouteId": "backend1/route1",
          "BackendId": "backend1",
          "Rule": "Host('localhost') && Path('/{**catchall}')"
        }
      ]
```
This maps to a [ProxyRoute](https://github.com/dotnet/yarp/blob/b2cf5bdddf7962a720672a75f2e93913d16dfee7/src/IslandGateway.Core/Abstractions/RouteDiscovery/Contract/GatewayRoute.cs) type.

This basic structure is useful though the "Rule" [system](https://github.com/dotnet/yarp/blob/b2cf5bdddf7962a720672a75f2e93913d16dfee7/src/IslandGateway.Core/Service/Config/RuleParsing/RuleParser.cs) seems overly complex. Need to circle back with DavidN on this. We may be able to simplify that down to independent keys for matching Host, Path, Header, etc.. It's not clear that the additional `&&` or `||` aspects are necessary here. If we used separate properties then it would be implicitly `&&` based. To achieve `||` you'd define additional routes. This is also an area where augmenting with code defined constraints could be useful to handle the more complex scenarios. 

The ProxyRoute.Metadata dictionary may be able to be replaced or supplemented by giving direct access to the config node for that route. Compare to Kestrel's [EndpointConfig.ConfigSection](https://github.com/dotnet/aspnetcore/blob/f4d81e3af2b969744a57d76d4d622036ac514a6a/src/Servers/Kestrel/Core/src/Internal/ConfigurationReader.cs#L168-L175) property. That would allow for augmenting an endpoint with additional complex custom entries that the app code can reference for additional config actions.

Update: The custom rule system was modified by [#24](https://github.com/dotnet/yarp/pull/24) so that the config now looks like this:
```
       "Routes": [
         {
           "RouteId": "backend1/route1",
           "BackendId": "backend1",
           "Match": {
             "Methods": [ "GET", "POST" ],
             "Host": "localhost",
             "Path": "/{**catchall}"
           }
         }
       ]
```

## Backend configuration

The proxy code defines the types [Backend](https://github.com/dotnet/yarp/blob/b2cf5bdddf7962a720672a75f2e93913d16dfee7/src/IslandGateway.Core/Abstractions/BackendDiscovery/Contract/Backend.cs) and [BackendEndpoint](https://github.com/dotnet/yarp/blob/b2cf5bdddf7962a720672a75f2e93913d16dfee7/src/IslandGateway.Core/Abstractions/BackendEndpointDiscovery/Contract/BackendEndpoint.cs) and allows these to be defined via config and referenced by name from routes.

A BackendEndpoint defines a specific service instance with an id, address, and associated metadata.

A Backend is a collection of one or more BackendEndpoints and a set of policies for choosing which endpoint to rout each request to (load balancing, circuit breakers, health checks, affinities, etc.). This seems a bit monolithic compared to our initial design explorations. We anticipate wanting to break these policies up into distinct steps in a pipeline to make them more replaceable. That said, we'll still need a config model for the default set of components and it may look very much like what's already here.

Question: Why are the backends and the endpoints listed separately in config rather than nested? Object model links endpoints 1:1 with backends, so there doesn't seem to be a reason to list them separately.

Existing:
```
      "Backends": [
        {
          "BackendId": "backend1"
        },
        {
          "BackendId": "backend2"
        }
      ],
      "Endpoints": {
        "backend1": [
          {
            "EndpointId": "backend1/endpoint1",
            "Address": "https://localhost:10000/"
          }
        ],
        "backend2": [
          {
            "EndpointId": "backend2/endpoint1",
            "Address": "https://localhost:10001/"
          }
        ]
      },
```
Nested:
```
      "Backends": [
        {
          "BackendId": "backend1",
          "Endpoints": [
            {
              "EndpointId": "backend1/endpoint1",
              "Address": "https://localhost:10000/"
            }
          ],
        },
        {
          "BackendId": "backend2"
          "Endpoints": [
            {
              "EndpointId": "backend2/endpoint1",
              "Address": "https://localhost:10001/"
            }
          ],
        }
      ],
```
Additional feedback: Why is it using arrays instead of objects? These items are not order sensitive, and they already have id properties anyways.
```
      "Backends": {
        "backend1" : {
          "Endpoints": [
            "endpoint1": {
              "Address": "https://localhost:10000/"
            }
          },
        },
        "backend2": {
          "Endpoints": {
            "endpoint1": {
              "Address": "https://localhost:10001/"
            }
          },
        }
      },
```

Update: The backend and endpoint layout has been modified to the following:
```

    "Backends": {
      "backend1": {
        "Endpoints": {
          "backend1/endpoint1": {
            "Address": "https://localhost:10000/"
          }
        }
      },
      "backend2": {
        "Endpoints": {
          "backend2/endpoint1": {
            "Address": "https://localhost:10001/"
          }
        }
      }
    },
```

## Config reloading

Config reloading is not yet a blocking requirement but we do expect to need it in the future. This design needs to factor in how reloading might work when it does get added.

** NOTE ** The proxy code has a concept of Signals that is used to convey config change. We need to see how this integrates with change notifications from our config sources and flows through the system.

The Extensions config and options systems have support for change detection and reloading but very few components take advantage of it. Logging is the primary consumer today.

One concern is that some change notification sources like files can trigger multiple times for a single event. The config system does not have built in handling for this, it's up to consumers to 'debounce' and filter out redundant notifications.

Kestrel support for reloading config is tracked by https://github.com/dotnet/aspnetcore/issues/19376.

Reloading proxy config will need to happen atomically and avoid disrupting requests already in flight. We may need to rebuild portions of the app pipeline and swap them out for new requests, drain the old requests, and clean up the old pipelines. We also want to avoid a full reset for small config changes where possible. E.g. if only one route changes then ideally we'd only rebuild that route.

Reloading should be something you can opt into or out of. Right now this is only possible at the config level by opting in or out for a config source, but that affects the whole app.

Updates:

Config reload for proxy routes, backends, and endpoints already works. You edit appsettings.json and it automatically reloads and reconfigures the routes. Note the config change notification usually gets fired twice and logs "Applying proxy configs" each time, but the change diff logic prevents an unnecessary update the second time. We may still want to do some debounce detection to prevent extra config diffs, but that's lower priority.

@halter73 raised the question of how much effort we put in to make the kestrel reload atomic with the routing reload? Conceptually it makes sense to keep the two in sync, but programmatically it's quite difficult as there's no connection between the two systems. They'd be reacting to the same change notification event in serial and requests in flight may see one set of changes without the other. We discussed this in the weekly sync and decided that since kestrel endpoint changes will be a less common scenario we won't initually worry about the atomicity here until we have customer feedback that demonstrates issues.

Also, when a kestrel endpoint is modified or removed should existing connections on that endpoint be eagerly drained and closed, or should they be allowed to run a normal lifecycle? Kestrel does not currently track active connection per endpoint so additional tracking would be needed if we wanted to shut them down.

Updates:

The config reload code has been moved from the sample into the product assemblies.

## Augmenting config via code

Some things are easier to do in code and we want to be able to support that while still pulling more transient data from config. [Kestrel](https://github.com/dotnet/aspnetcore/blob/aff01ebd7f82d641a4cfbd4a34954300311d9c2b/src/Servers/Kestrel/samples/SampleApp/Startup.cs#L138-L147) has a model where endpoints are named in config and then can be reference by name in code for additional configuration.
```
{
  "Kestrel": {
    "Endpoints": {
      "NamedEndpoint": { "Url": "http://*:6000" },
      "NamedHttpsEndpoint": {
        "Url": "https://*:6443",
      }
    }
  }
}
```
```
    options.Endpoint("NamedEndpoint", opt =>
    {

    })
    .Endpoint("NamedHttpsEndpoint", opt =>
    {
        opt.HttpsOptions.SslProtocols = SslProtocols.Tls12;
    });
```

The proxy code already has named routes, backends, backend endpoints, etc., so we should be able to build a similar code augmentation for those.

Reloadable config complicates this pattern. The code augmentation actions will need to be captured for the lifetime of the app rather than just for startup so they can be re-run later.
