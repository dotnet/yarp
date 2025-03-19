# YARP Tunneling

> [!CAUTION]
> These are archived design discussions. Information may be outdated and inaccurate.

## Introduction
While many organizations are moving their computing to the cloud, there are occasions where you need to be able to run some services in a local datacenter. The problem then is if you need to be able to communicate with those services from the cloud. Creating a VPN network connection to Azure or other cloud provider is possible, but usually involves a lot of red tape and configuration complexity as the two networks need to be integrated.

If all that the cloud needs to access is resources that are exposed over http, then a simpler solution is to have a gateway that can route traffic to the remote resource. Additionally, outbound connections over http(s) are not usually blocked, so having a on-prem gateway make an outbound connection to the cloud, is the easiest way to establish the route. This is the basis behind the [Azure Relay](https://learn.microsoft.com/azure/azure-relay/relay-what-is-it) service offering.

That is the principle of the tunnel feature for YARP. You operate two instances of the YARP proxy service, configured as a tunnel. The advantage over Azure Relay is that using a reverse proxy as an on-prem gateway means that both cloud and back end services can be used without needing to update the application other than addresses. This is particularly useful for services that may have been written by a 3rd party, or are no longer under active development, and so making changes to the configuration is complicated and expensive. Relay requires the sender and receiver to be updated to use its connection protocol.

![Tunnel diagram](https://github.com/assets/95136/52d7491b-6e8a-4a2c-a51d-0734b3e41930)

In the on-prem data center, you run an instance of YARP, we'll call this the back-end proxy. This is configured with routes to the resources that should be externally accessible - only routes that are configured via this proxy will be exposed. The back-end proxy is configured to create a tunnel connection to the front-end instance by specifying the connection URL and security details for the connection.

The instance in the cloud, we'll refer to as the front-end, will be configured with a tunnel endpoint URL to be used by the on-prem proxy. The on-prem proxy will create a websocket connection to the tunnel endpoint, this will map the tunnel to a specific cluster. Routes can be directed to use the tunnel connection to the back-end by using the cluster that is used for the tunnel.

## Tunnel Protocol

The tunnel will establish a Websockets connection between the back-end and the front-end. The back-end will establish the connection so that it can more easily break through firewalls. Once the WSS connection is created, it will be treated as a stream over which HTTP/2 traffic will be routed. HTTP/2 is used so that multiple simultaneous requests can be multiplexed over a single connection. The HTTP/2 protocol is only used between the two proxies, the connections either side can be any protocol that the proxy supports. This means we don't put any specific capability requirement on the destination servers. 

If the tunnel connection is broken, the back-end will attempt to reconnect to the front-end:
- If the connection fails, then it will continue to reconnect every 30s until the connection is re-established.
- If the connection is refused with a 500 series error, then it will be retried at the next 30s timeout. 
- If the connection is refused with a 400 series error then further connections for that tunnel will not be made.

> Issue: Do we need an API for the tunnel? As its created from code on the back-end, the app could have additional logic for control over the duration. Does it have API for status, clean shutdown, etc.

> Issue: Will additional connections be created for scalability - H2 perf becomes limited after 100 simultaneous requests. How does the front-end know to pair a second back-end connection?

The Front End should keep the WSS connection alive by sending pings every 30s if there is no other traffic. This should be done at the WSS layer.

## Moving pieces

| Location | Name | Description |
| --- | --- | --- |
| front-end | EndPoint | The endpoint that the back-end proxy will connect to to create a tunnel. |
| front-end | Cluster | The cluster that will direct to back-end proxy(ies) that have created tunnels. |
| front-end | Routes | Routes need to be configured to route specific URLs to the tunnel, by using clusters that are a tunnel. | 
| back-end | Tunnel URL(s) | The URL(s) for the front-end endpoint that can be used to establish the tunnel. |
| back-end | Routes | The back-end needs to have routes defined that will direct traffic to local resources. |

## front-end 
The front-end is the proxy that will be called by clients to be able to access resources via the back-end proxy. It will route traffic over a tunnel created using a WSS connection from the back-end proxy. YARP needs a mechanism to know which requests will be routed via the tunnel. This will be achieved by extending the existing cluster concept in YARP - The request to create a tunnel will specify the name of a cluster. Once the tunnel is established, it will be treated as a dynmamically created destination for the named cluster. Routes will not need to be changed, they will point at the cluster, and the tunnels will be used in the same way as destinations. 

Tunnel services must be enabled by the proxy server:

``` C#
builder.Services.AddReverseProxy()
       .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddTunnelServices();
```

The front-end needs to have a tunnel endpoint that the back-end will connect to. The endpoint should be parameterized to include the name of the cluster as part of the URL, and a callback that is used to validate the connection is approved:
 - Including the ClusterId in the URL enables the same endpoint mechanism to be used for multiple clusters. 
 - Using a callback for authentication enables whatever scheme the proxy author(s) wish to use.
   - Trying to encode specific auth schemes will invariably miss a scenario that is needed.
   - The samples that we produce should be based around client certs as it is a good way to manage secure shared secrets.

``` C#
app.MapReversProxy();
app.MapTunnel("/tunnel/{ClusterId}", async (connectionContext, cluster) => {

    // Use the extensions feature https://github.com/dotnet/yarp/issues/1709 to add auth data for the tunnel
    var tunnelAuth = cluster.Extensions[typeof(TunnelAuth)]; 
    if (!context.Connection.ClientCertificate.Verify()) return false;
    foreach (var c in tunnelAuth.Certs)
    {
        if (c.ThumbPrint == context.Connection.ClientCertificate.Thumbprint) return true;
    }
    return false;
});
```

The front-end should have configuration for routes that direct to a cluster that is for the tunnel. The cluster must be marked as IsTunnel to enable tunnel capability, and must *not* include other destinations. The cluster's destinations will be supplied dynamically by back-ends creating tunnel connections.

In the following case it uses the Extensions feature to enable storing thumbprints for client certs that are used to authenticate tunnel connections. The Route will direct all traffic under the path `/OnPrem/*` to the tunnel.

``` json
{
    "ReverseProxy":
    {
        "Routes" : {
            "OnPrem" : {
                "Match" : {
                    "Path" : "/OnPrem/{**any}"
                },
                "ClusterId" : "MyTunnel1"
            }
        },
        "Clusters" : {
            "MyTunnel1" : {
                "IsTunnel" : true,
                "Extensions" : {
                    "TunnelAuth" : {
                        "Certs" : {
                            "name1" : "thumbprint1",
                            "name2" : "thumbprint2"
                        }
                    }
                }
            }
        }
    }
}
```

To ensure scalability, multiple back-end proxy instances should be able to create tunnel connections for the same cluster. When that happens, the load balancing rules for the cluster should apply, and balance between the active tunnels. Similarly multiple front-ends can be used to reduce the problems with a single point of failure.

## back-end

The back-end instance is the proxy that will reside on the same network as the resources that should be exposed. The back-end will need to be able to connect to those resources, and also be able to create a WebSocket connection to the front-end proxy server(s) via whatever firewalls are between them.

The back-end proxy is configured with routes and destinations that it wishes to expose to the front end. Security is maintained because only URLs matching its routes will be proxyable via it. This prevents attacks at the front-end having arbitrary access to other resources on the back-end network - they need to be explicitly included in the back-end route table.

The outbound connection to the front end needs to be explicitly made for each tunnel that the back-end wishes to create.

``` C#
builder.Services.AddReverseProxy()
       .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var url = builder.Configuration["Tunnel:Url"]!; // Eg https://Myfront-end.MyCorp.com/tunnel/MyTunnel1

// Setup additional details for the connection, auth and headers
var tunnelOptions = new TunnelOptions(){
       TunnelClient = new SocketsHttpHandler(),
       ClientCertificates = new X509CertificateCollection { cert }
       AuthCallback = AuthServer;
       };
tunnelOptions.Headers.Add("MyJWTToken", tokenString);

builder.WebHost.UseTunnelTransport(url, tunnelOptions);
```

The tunnel creation takes an options class that enables the HttpHandler, headers to be set for the tunnel creation to enable authentication flexibility by the app developers. It also should include a callback to enable validation of the server certificate.

The back-end configuration would use the standard routes and cluster definitions.

``` json
{
    "ReverseProxy":
    {
        "Routes": {
            "CNCMilling": {
                "Match": {
                    "Path": "/OnPrem/CNCMilling/{**any}"
                },
                "ClusterId": "Milling"
            }
            "3DPrinting" : {
                "Match": {
                    "Path": "/OnPrem/Extrusion/{**any}"
                },
                "ClusterId": "3dPrinting"
            }
        },
        "Clusters": {
            "Milling": {
                "Destinations": {
                    "Bay12" : "https://bay12-efd432/",
                    "Bay15" : "https://bay15-j377d3/"
                }
            }
            "3dPrinting": {
                "Destinations": {
                    "Bay41-controller" : "https://bay41-controller/"
                }
            }
        }
    }
}
```

In the above case, requests using the paths `/OnPrem/CNCMilling/*` and `/OnPrem/Extrusion/*` will be routed by the back-end to their respective services - other paths would result in an error.

Note: Active health checks probably don't make sense to be performed by the front-end against the back-end. Passive health checks will verify the overall condition of the tunnel.

## Scalability

In a large deployment, there needs to be the ability to have multiple front-end and back-end proxies:
- If the front-end receives multiple tunnel connections, then it should treat them as if the cluster has multiple destinations. The cluster can use the load balancing policy to select how it decides to route traffic to the back-end proxies.

> Note: The front-end proxy will not be aware of the actual destinations that serve resources - each back-end should have its own cluster definition for the actual destinations, and so can include multiple servers for any route/cluster combination.

- A back-end proxy should be able to create tunnels to multiple front-ends. The tunnels can be to related front-end proxies that are sharing the same load, or to front-ends in different cloud deployments. This enables the Front Ends to be very specific to particular deployments - and have constrained v-Lan configurations in the cloud.

## Authentication

The authentication options for ASP.NET are diverse, and IT departments will likely have their own conditions on what is required to be able to secure a tunnel. So rather than trying to implement the combinatorial matrix of what customers could need, we should use a callback so that the proxy author can decide.

Samples should be created that show best practices using a secure mechanism such as client a certificate.

*Issue:* Does the back-end need additional mechanisms to validate the connection to the front-end, or is TLS/SNI sufficient?

## Security

The purpose of the tunnel is to simplify service exposure by creating a tunnel through the firewall that enables external requests to be made to destination servers on the back-end network. There are a number of mitigations that reduces the risk of this feature:

* No endpoints are exposed via the firewall - it does not expose any new endpoints that could act as attack vectors. The tunnel is an outbound connection made between the back-end and the front-end.
* Traffic directed via the tunnel will need to have corresponding routes in the Back End configuration. Traffic will only be routed if there is a respective route and cluster configuration. Tunnel traffic can't specify arbitrary URLs that would be directed to a hostname not included in the back-end route table configuration.
* Tunnel connections should only be over HTTPs

## Metrics

? What telemetry and events are needed for this?

## Error conditions

| Condition | Description |
| --- | --- |
| No tunnel has connected | If the front end receives a request for a route that is backed by a tunnel and no tunnels have been created, then it should respond to those requests with a 502 "Bad Gateway" error|

## Web Transport

Web Transport is an interesting future protocol choice for the tunnel.


