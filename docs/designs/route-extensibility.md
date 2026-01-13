## Problem Statement

> [!CAUTION]
> These are archived design discussions. Information may be outdated and inaccurate.

Today if you want to extend the route or clusters, you can only do it through the metadata property on each, which is a Dictionary<string, string>. If you want to be able to have structured data its not possible without you forcing it into a string and then parsing it when needed. There are scenarios like A/B testing, or authenticating with back end servers (not pass thru) where you want to be able to store a structure of data in config, and have it available at runtime on the route/cluster objects.

If we want there to be pre-built extensions to YARP (#1714), then there needs to be a way for each of the extensions to have its own config data on routes and clusters, and for them to not step on each others toes.

## Why is this important to you?

Taking a canonical example of A/B testing. In this scenario you want to be able to direct traffic to multiple clusters with the traffic patterns determined based on additional criteria. For example, You may want to be able to have a collection of clusters that are used for a route, together with percentages. So the config could look something like:

```json
{
  "ReverseProxy": {
    "Routes": {
      "route1": {
        "ClusterId": "ignore",
        "Match": {
          "Path": "{**catch-all}"
        },
        "Extensions": {
          "A-B": [
            {
              "ClusterId": "c1",
              "Load": 0.4
            },
            {
              "ClusterId": "c2",
              "Load": 0.6
            },
            {
              "ClusterId": "experimental",
              "Load": 0.01
            },
            {
              "ClusterId": "TelemetrySample",
              "Load": 0.01
            }
          ]
        }
      },
```
The above example is adding an "A-B" extension to the route with its own data. There could be other extensions each of which have their own configuration data.

## What would this look like

### Requirements
* Multiple extensions can be added to YARP, each of which can have their own config state.
* Be able to have arbitrary data as part of config for routes and/or clusters
  * YARP should not dictate a specific data structure for the extension config 
* Be able to access that data as objects from middleware directly off the route and clusters
* Be able to remote that data in the case of a distributed configuration server

## Proposal

* Add an Extensions collection to Route and Cluster. This should follow the same pattern as http features, using an `IReadOnlyDictionary<Type, object>` and be accessed based on the type of the extension. This would then be accessible in proxy middleware from the route or cluster objects via an `Extensions` property:

```c#
public void Configure(IApplicationBuilder app, IProxyStateLookup lookup)
{
    app.UseRouting();
    app.UseEndpoints(endpoints =>
    {
        // We can customize the proxy pipeline and add/remove/replace steps
        endpoints.MapReverseProxy(proxyPipeline =>
        {
            // Use a custom proxy middleware, defined below
            proxyPipeline.Use((context, next) =>
            {
                var proxyFeature = context.Features.Get<IReverseProxyFeature>();
                var abstate = proxyFeature.Route.Extensions[typeof(ABState)];
                var newClusterName = abstate.SelectSlice(new Random().NextDouble());
                if (lookup?.TryGetCluster(newClusterName, out var cluster))
                {
                    context.ReassignProxyRequest(cluster);
                }
                return next();
            });
            proxyPipeline.UseSessionAffinity();
            proxyPipeline.UseLoadBalancing();
        });
    });
}
```

Using a dictionary based on object type enables easy access to the object at runtime, and a strongly typed result.

* There needs to be a way for YARP to construct these strongly typed extensions based on the `IConfiguration` provider. There needs to be a mapping between the key in config, and the type that will be used to store the data. That mapping is handled by enabling factories that can be registered:
```c#
services.AddReverseProxy()
    .LoadFromConfig(Configuration.GetSection("ReverseProxy"))
    .AddRouteExtension("A-B", (section, _, _) => new ABState(section));
```
  > Along with a similar mechanism for clusters. The extension registration would be something like:
```c#
static IReverseProxyBuilder AddRouteExtension(this IReverseProxyBuilder builder, string sectionName, Func<IConfigurationSection, RouteConfig, ExtensionType, ExtensionType> factory)  
```

> Where the factory is passed:
  * The IConfigurationSection for the extension
  * The route object that is being extended
  * The existing extension instance in the case of configuration updates
>
> When the configuration is parsed, the factory is called based on the configuration key name, and the resultant object is added to the route/cluster objects.
>
> Regular YARP config will do a diff merge to handle config changes, and create new objects if applicable. The same mechanism needs to be used for extensions. If the configuration is updated, and an existing instance of the extension exists, then it will be passed to the factory. The factory can compare the current instance and re-use it, or copy its data across to a new instance based on the changes. Instances must stable, so existing instances shouldn't be modified if it would affect existing in-flight requests. YARP can't really enforce rules on how the objects are changed as we want the types to be user defined. 

* When using a custom config provider, the Extensions collection can be populated by the custom provider directly, or the provider can expose an `IConfigurationSection` implementation and use the factory as described below.

* When YARP is integrated with 3rd party config systems, such as K8s or ServiceFabric, those systems typically have a way of expressing custom properties, some of which will be used by YARP for the definition of routes/ clusters etc. To facilitate the ability for route and cluster extensions to be expressed within those systems, the integration provider should expose an `IConfigurationSection` implementation that maps between the integrated persistence format and YARP. 
  `IConfigurationSection` is essentially a name/value lookup API, it should map pretty reasonably to the YAML or JSON formats used by the configuration systems, and not be an undue burden to implement on these integrations.

* Integration with 3rd party config systems can involve a remote process that YARP can pull its configuration from. I am [proposing another feature](https://github.com/dotnet/yarp/issues/1710), that we formalize this pattern and have the ability to create a central YARP config provider, to which multiple YARP proxies can bind. This enables scalability in terms of being able to push config to multiple instances of YARP at once.

* To support this scenario, we should serialize the IConfiguration data and pass that across to the proxy instances.
