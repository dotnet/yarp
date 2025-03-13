# ReverseProxy.Metrics.Sample

This sample demonstrates how to use the ReverseProxy.Telemetry.Consumption library to listen to telemetry data from YARP.
In this case it uses the events to create a per-request data structure with detailed timings for each operation that takes place as part of the proxy operation.

Internally YARP uses EventSource to collect telemetry events and metrics from a number of subsystems that are used to process the requests.
The YARP telemetry library provides wrapper classes that collect these events metrics and make them available for Consumption.
To listen for the metrics you register classes with DI that implement an interface for each subsystem.

The subsystems are:
- **Proxy** which represents the overall proxy operation, and success or failure. 

  Events include:
    - When proxy requests are started and stopped
    - When request/response bodies are processed

  Metrics include:
    - Number of requests started
    - Number of request in flight
    - Number of requests that have failed

- **Kestrel** which is the web server that handles incoming requests.

  Events include:
    - When requests are started/stopped or fail
    
  Metrics include:
    - Connection Rate - how many connections are opened a second
    - Total number of connections
    - Number of TLS handshakes
    - Incoming queue length

- **Http** which is the HttpClient which makes outgoing requests to the destination servers. 

  Events include:
    - When connections are created
    - When requests are started/stopped or fail
    - When headers/contents are sent/received
    - When requests are dequeued as connections become available

  Metrics include:
    - Number of outgoing requests started
    - Number of requests failed
    - Number of active requests
    - Number of outbound connections

- **Sockets** which includes events around connection attempts & metrics about the amount of data sent and received

- **NameResolution** which includes events around name resolution attempts & metrics about DNS lookups of destinations

- **NetSecurity** which includes events around SslStream handshakes & metrics about the number and latency of handshakes per protocol

## Key Files

The following files are key to implementing the features described above:

### Program.cs

Performs registrtion of the proxy, the listener classes and a custom ASP.NET middleware step that starts per-request telemetry and reports the results when complete

### ProxyTelemetryConsumer.cs

Listens to events from the proxy telemetry and records timings and info about the high level processing involved in proxying a request.

### HttpTelemetryConsumer.cs

Listens to events from the HttpClient telemetry and records timings and info about the outbound request and response from the destination server.

### PerRequestMetrics.cs

Class to store the metrics on a per request basis. Instances are stored in AsyncLocal storage for the duration of the request. 

### PerRequestYarpMetricCollectionMiddleware.cs

ASP.NET Core middleware that is the first and last thing called as part of the ASP.NET handling of the request. It initializes the per-request metrics and logs the results at the end of the request.
