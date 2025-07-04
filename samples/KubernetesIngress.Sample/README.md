# Kubernetes Ingress Samples

These samples show how to deploy the YARP Kubernetes Ingress Controller into a Kubernetes cluster.

An [Ingress Controller](https://kubernetes.io/docs/concepts/services-networking/ingress-controllers/) monitors for [Ingress resources](https://kubernetes.io/docs/concepts/services-networking/ingress/) and routes traffic to services.

There are three parts to these samples:
- [Backend](./backend/README.md)
- [Combined Ingress Controller](./Combined/README.md)
- [Separate Ingress Controller and Monitor](./Ingress/README.md)

The "Backend" is a Dockerized ASP.NET Core application that returns dummy information in web requests. This project contains Kubernetes manifest files for deploying the application and an Ingress resource into a cluster.

The Ingress Controller can be deployed either as:
- a single deployable (see the Combined sample), or
- as two separate deployables where one (the "monitor") watches the Ingress resources and the other (the "ingress") retrieves the YARP configuration from the "monitor" and handles the routing

Both of these controllers utilize the `Yarp.Kubernetes.Controller` project.

## Ingress Resource

The `Yarp.Kubernetes.Controller` project currently supports the following Ingress features:

- [Ingress rules](https://kubernetes.io/docs/concepts/services-networking/ingress/#ingress-rules) for host name and path-based routing to backend services
- [Ingress class](https://kubernetes.io/docs/concepts/services-networking/ingress/#ingress-class) for multiple, independent instances of the controller (cluster scope only)
- [Default ingress class](https://kubernetes.io/docs/concepts/services-networking/ingress/#default-ingress-class) for simplifying Ingress resource configuration

The `Yarp.Kubernetes.Controller` project does not support:
- The [TLS specification](https://kubernetes.io/docs/concepts/services-networking/ingress/#tls) for Ingress resources (coming soon), though you could combined with the LetsEncrypt.Sample.
- The [deprecated annotation](https://kubernetes.io/docs/concepts/services-networking/ingress/#deprecated-annotation) for ingress resources.

### Annotations

The `Yarp.Kubernetes.Controller` project supports a number of **optional** annotations on Ingress resources for functionality provided by YARP.

These annotations would be specified like this:
```
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: minimal-ingress
  namespace: default
  annotations:
    yarp.ingress.kubernetes.io/authorization-policy: authzpolicy
    yarp.ingress.kubernetes.io/rate-limiter-policy: ratelimiterpolicy
    yarp.ingress.kubernetes.io/output-cache-policy: outputcachepolicy
    yarp.ingress.kubernetes.io/transforms: |
      - PathRemovePrefix: "/apis"
    yarp.ingress.kubernetes.io/route-headers: |
      - Name: the-header-key
        Values:
        - the-header-value
        Mode: Contains
        IsCaseSensitive: false
      - Name: another-header-key
        Values:
        - another-header-value
        Mode: Contains
        IsCaseSensitive: false
    yarp.ingress.kubernetes.io/route-queryparameters: |
      - Name: the-queryparameters-key
        Values:
        - the-queryparameters-value
        Mode: Contains
        IsCaseSensitive: false
      - Name: another-queryparameters-key
        Values:
        - another-queryparameters-value
        Mode: Contains
        IsCaseSensitive: false
    yarp.ingress.kubernetes.io/route-methods: |
      - GET
      - POST
spec:
  rules:
    - http:
        paths:
          - path: /foo
            pathType: Prefix
            backend:
              service:
                name: frontend
                port:
                  number: 80
```

The table below lists the available annotations.

|Annotation|Data Type|
|---|---|
|yarp.ingress.kubernetes.io/authorization-policy|string|
|yarp.ingress.kubernetes.io/rate-limiter-policy|string|
|yarp.ingress.kubernetes.io/output-cache-policy|string|
|yarp.ingress.kubernetes.io/backend-protocol|string|
|yarp.ingress.kubernetes.io/cors-policy|string|
|yarp.ingress.kubernetes.io/health-check|[ActivateHealthCheckConfig](https://learn.microsoft.com/dotnet/api/yarp.reverseproxy.configuration.activehealthcheckconfig)|
|yarp.ingress.kubernetes.io/http-client|[HttpClientConfig](https://learn.microsoft.com/dotnet/api/yarp.reverseproxy.configuration.httpclientconfig)|
|yarp.ingress.kubernetes.io/http-request|[ForwarderRequestConfig](https://learn.microsoft.com/en-us/dotnet/api/yarp.reverseproxy.forwarder.forwarderrequestconfig)|
|yarp.ingress.kubernetes.io/load-balancing|string|
|yarp.ingress.kubernetes.io/route-metadata|Dictionary<string, string>|
|yarp.ingress.kubernetes.io/session-affinity|[SessionAffinityConfig](https://learn.microsoft.com/dotnet/api/yarp.reverseproxy.configuration.sessionaffinityconfig)|
|yarp.ingress.kubernetes.io/transforms|List<Dictionary<string, string>>|
|yarp.ingress.kubernetes.io/route-headers|List<[RouteHeader](https://learn.microsoft.com/dotnet/api/yarp.reverseproxy.configuration.routeheader)>|
|yarp.ingress.kubernetes.io/route-queryparameters|List<[RouteQueryParameter](https://learn.microsoft.com/dotnet/api/yarp.reverseproxy.configuration.routequeryparameter)>|
|yarp.ingress.kubernetes.io/route-order|int|
|yarp.ingress.kubernetes.io/route-methods|List<string>|

#### Authorization Policy

See https://learn.microsoft.com/aspnet/core/fundamentals/servers/yarp/authn-authz for a list of available policies, or how to add your own custom policies.

`yarp.ingress.kubernetes.io/authorization-policy: anonymous`

#### RateLimiter Policy

See https://learn.microsoft.com/aspnet/core/fundamentals/servers/yarp/rate-limiting for a list of available policies, or how to add your own custom policies.

`yarp.ingress.kubernetes.io/rate-limiter-policy: mypolicy`

#### Output Cache Policy

`yarp.ingress.kubernetes.io/output-cache-policy: mycachepolicy`

#### Backend Protocol

Specifies the protocol of the backend service. Defaults to http.

`yarp.ingress.kubernetes.io/backend-protocol: "https"`

#### CORS Policy

See https://learn.microsoft.com/aspnet/core/fundamentals/servers/yarp/cors for the list of available policies, or how to add your own custom policies.

`yarp.ingress.kubernetes.io/cors-policy: mypolicy`

#### Health Check

Proactively monitors destination health by sending periodic probing requests to designated health endpoints and analyzing responses.

See https://learn.microsoft.com/aspnet/core/fundamentals/servers/yarp/dests-health-checks.

```
yarp.ingress.kubernetes.io/health-check |
  Active:
  Enabled: true
  Interval: '00:00:10'
  Timeout: '00:00:10'
  Policy: ConsecutiveFailures
  Path: "/api/health"
```

#### HTTP Client

Configures the HTTP client that will be used for the destination service.

See https://learn.microsoft.com/aspnet/core/fundamentals/servers/yarp/http-client-config.

```
yarp.ingress.kubernetes.io/http-client: |
  SslProtocols: Ssl3
  MaxConnectionsPerServer: 2
  DangerousAcceptAnyServerCertificate: true
```

#### HTTP Request

Configures the HTTP request that will be sent to the destination service.

See https://learn.microsoft.com/dotnet/api/yarp.reverseproxy.forwarder.forwarderrequestconfig.

```
yarp.ingress.kubernetes.io/http-request: |
  ActivityTimeout: '00:01:00'
  Version: '2.0'
  VersionPolicy: 'RequestVersionExact'
  AllowResponseBuffering: false
```

#### Load Balancing

See https://learn.microsoft.com/aspnet/core/fundamentals/servers/yarp/load-balancing for a list of the available options.

`yarp.ingress.kubernetes.io/load-balancing: Random`

#### Route Metadata

See https://learn.microsoft.com/dotnet/api/yarp.reverseproxy.configuration.routeconfig.metadata#yarp-reverseproxy-configuration-routeconfig-metadata.

```
yarp.ingress.kubernetes.io/route-metadata: |
  Custom: "orange"
  Tenant: "12345"
```

#### Session Affinity

See https://learn.microsoft.com/aspnet/core/fundamentals/servers/yarp/session-affinity.

```
yarp.ingress.kubernetes.io/session-affinity: |
  Enabled: true
  Policy: Cookie
  FailurePolicy: Redistribute
  AffinityKeyName: Key1
  Cookie:
    Domain: localhost
    Expiration:
    HttpOnly: true
    IsEssential: true
    MaxAge:
    Path: mypath
    SameSite: Strict
    SecurePolicy: Always
```

#### Transforms

Transforms use the YAML key-value pairs as per the YARP [Request Transforms](https://learn.microsoft.com/aspnet/core/fundamentals/servers/yarp/transforms#request-transforms)

```
yarp.ingress.kubernetes.io/transforms: |
  - PathPrefix: "/apis"
  - RequestHeader: header1
    Append: bar
```

#### Route Headers

`route-headers` are the YAML representation of YARP [Header Based Routing](https://learn.microsoft.com/aspnet/core/fundamentals/servers/yarp/header-routing).

See https://learn.microsoft.com/dotnet/api/yarp.reverseproxy.configuration.routeheader.

```
yarp.ingress.kubernetes.io/route-headers: |
  - Name: the-header-key
    Values:
    - the-header-value
    Mode: Contains
    IsCaseSensitive: false
  - Name: another-header-key
    Values:
    - another-header-value
    Mode: Contains
    IsCaseSensitive: false
```

#### Route QueryParameters

`route-queryparameters` are the YAML representation of YARP [Parameter Based Routing](https://learn.microsoft.com/aspnet/core/fundamentals/servers/yarp/queryparameter-routing).

See https://learn.microsoft.com/dotnet/api/yarp.reverseproxy.configuration.routequeryparameter.

```
yarp.ingress.kubernetes.io/route-queryparameters: |
  - Name: the-queryparameter-name
    Values:
    - the-queryparameter-value
    Mode: Contains
    IsCaseSensitive: false
  - Name: another-queryparameter-name
    Values:
    - another-queryparameter-value
    Mode: Contains
    IsCaseSensitive: false
```

#### Route Order

See https://learn.microsoft.com/dotnet/api/yarp.reverseproxy.configuration.routeconfig.order#yarp-reverseproxy-configuration-routeconfig-order.

```
yarp.ingress.kubernetes.io/route-order: '10'
```

#### Route Methods

See https://learn.microsoft.com/dotnet/api/yarp.reverseproxy.configuration.routematch.methods#yarp-reverseproxy-configuration-routematch-methods.

```
yarp.ingress.kubernetes.io/route-methods: |
  - GET
  - POST
```
