# YARP Direct Proxy Example

Some customers who have an existing custom proxy for HTTP/1.1 are looking at YARP for a solution to handle more complex requests, such as HTTP/2, gRPC, WebSockets in future QUIC and HTTP/3. These applications have their own means of routing, load balancing, affinity, etc. and only need to forward a specific request to a specific destination. To make it easier to integrate YARP into these scenarios, the component that proxies requests is exposed via IHttpForwarder which can be called directly, and has few dependencies on the rest of YARP's infrastructure.

This example shows how to use IHttpForwarder to proxy a request to/from a specified destination.

The operation of the proxy can be thought of as:

```text
+-------------------+           +-------------------+           +-------------------+
|      Client       |  ──(a)──► |      Proxy        |  ──(b)──► |    Destination    |
|                   | ◄──(d)──  |                   | ◄──(c)──  |                   |
+-------------------+           +-------------------+           +-------------------+
```

(a) and (b) show the *request* path, going from the client to the destination.
(c) and (d) show the *response* path, going from the destination back to the client.

Normal proxying comprises the following steps:

| \# | Step | Direction |
| -- | ---- | --------- |
| 1 | Disable ASP .NET Core limits for streaming requests | |
| 2 | Create outgoing HttpRequestMessage | |
| 3 | Setup copy of request body (background) | Client --► Proxy --► Destination |
| 4 | Copy request headers | Client --► Proxy --► Destination |
| 5 | Send the outgoing request using HttpMessageInvoker | Client --► Proxy --► Destination |
| 6 | Copy response status line | Client ◄-- Proxy ◄-- Destination |
| 7 | Copy response headers | Client ◄-- Proxy ◄-- Destination |
| 8.1 | Check for a 101 upgrade response, this takes care of WebSockets as well as any other upgradeable protocol. | |
| 8.1.1 | Upgrade client channel | Client ◄--- Proxy ◄--- Destination |
| 8.1.2 | Copy duplex streams and return | Client ◄--► Proxy ◄--► Destination |
| 8.2 | Copy (normal) response body | Client ◄-- Proxy ◄-- Destination |
| 9 | Copy response trailer headers and finish response | Client ◄-- Proxy ◄-- Destination |
| 10 | Wait for completion of step 2: copying request body | Client --► Proxy --► Destination |

To enable control over mapping request and response fields and headers between the client and destination (steps 4 and 7 above), the HttpForwarder.ProxyAsync method takes a HttpTransformer. Your implementation can modify the request url, method, protocol version, response status code, or decide which headers are copied, modify them, or insert additional headers as required.

**Note:** When using the HttpForwarder class directly there are some [header transforms](https://learn.microsoft.com/aspnet/core/fundamentals/servers/yarp/transforms) included by default, such as adding ```X-Forwarded-For``` and removing the original Host header. This is the same set of transforms included by default in the YARP pipeline model (see BasicYarpSample).

## Files

The key functionality for this sample is all included in [Program.cs](Program.cs).
