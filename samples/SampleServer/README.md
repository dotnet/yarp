# Sample Server

This is a simple web server implementation that can be used to test YARP proxy, by using it as the destination.

Functionality in this sample server includes:

## Echoing of Request Headers

Provided that the request URI path doesn't match other endpoints described below, then the request headers will be reported back as text in the response body. This enables you to quickly see what headers were sent, for example to analyze header transforms made by the reverse proxy.

## Healthcheck status endpoint

[HealthController](Controllers/HealthController.cs) implements an API endpoint for /api/health that will randomly return bad health status.

## WebSockets endpoint

[WebSocketsController](Controllers/WebSocketsController.cs) implements an endpoint for testing web sockets at /api/websockets.

## Usage

To run the sample server use: 
- ```dotnet run``` from the sample folder
- ```dotnet run SampleServer/SampleServer.csproj``` passing in the path to the .csproj file
- Build an executable using ```dotnet build SampleServer.csproj``` and then run the executable directly

The server will listen to http://localhost:5000 and https://localhost:5001 by default. The ports and interface can be changed using the urls option on the cmd line. For example ```dotnet run SampleServer/SampleServer.csproj --urls "https://localhost:10000;http://localhost:10010"```
