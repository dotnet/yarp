# YARP Code Extensibility Sample

This sample shows two common customizations via code of the YARP reverse proxy:

- ## Dynamic configuration from code

  YARP supports pulling configuration from a config file, but in many scenarios the configuration of the routes to use, and which destinations the requests should be sent to need to be programmatically fetched from another source. The extensibility of YARP makes it easy for you to fetch that data from where you need to, and then pass to the proxy as lists of objects.

  This sample shows the routes and destinations being created in code, and then passed to an in-memory provider. The role of the in-memory provider is to give change notifications to YARP for when the config has been changed and needs to be updated. YARP uses a snapshot model for its configuration, so that changes are applied as an atomic action, that will apply to subsequent requests after the change is applied. Existing requests that are already being processed will be completed using the configuration snapshot from the time that they were received.

  The ```IProxyConfig``` interface implemented in InMemoryConfigProvider includes a change token which is used to signal when a batch of changes to the configuration is complete, and the proxy should take a snapshot and update its internal configuration. Part of the snapshot processing is to create an optimized route table in ASP.NET, which can be a CPU intensive operation, for that reason we don't recommend signaling for updates more than once per 15 seconds. 

- ## Custom pipeline step

  YARP uses a pipeline model for the stages involved in processing each request:

  - Mapping the request path to a route and cluster of destinations
  - Pre-assigning servers based on existing session affinity headers in the request 
  - Filtering the destination list for servers that are not healthy
  - Load balancing between the remaining servers based on load etc
  - Storing session affinity if applicable
  - Transforming headers if required
  - Proxying the request/response to/from the destination server

  You can insert additional custom stages into the pipeline, or replace built-in steps with your own implementations.
  
  This sample adds an additional stage that will filter the destinations from a cluster based on a "debug" metadata attribute being included in the config data based. If a custom header "Debug:true" is present in the request, then destinations with the debug metadata will be retained, and others will be filtered out, or vice-versa.  

## Key Files

The following files are key to implementing the features described above:

- ### [Program.cs](Program.cs)
  Provides the initialization routines for ASP.NET and the reverse proxy. It:
  - sets up the proxy passing in the InMemoryConfigProvider instance. The sample routes and clusters definitions are created as part of this initialization. The config provider instance is used for the lifetime of the proxy.
  - sets up the request pipeline. As an additional step is added, the proxy pipeline is configured here.
  - ```MyCustomProxyStep``` is the implementation of the additional step. It finds the proxy functionality via features added to the HttpContext, and then filters the destinations based on the presence of a "Debug" header in the request.
