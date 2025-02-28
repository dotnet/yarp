# Basic YARP Sample

This sample shows how to consume the YARP Library to produce a simple reverse proxy server. 
The proxy server is implemented as a plugin component for ASP.NET Core applications. ASP.NET Core servers like Kestrel provide the front end for the proxy by listening for http requests and then passing them to the proxy for paths that the proxy has registered. The proxy handles the requests by:
- Mapping the request URL path to a route in proxy configuration.
- Routes are mapped to clusters which are a collection of destination endpoints.
- The destinations are filtered based on health status, and session affinity (not used in this sample).
- From the remaining destinations, one is selected using a load balancing algorithm.
- The request is proxied to that destination.

This sample reads its configuration from the [appsettings.json](appsettings.json) file which defines 2 routes and clusters:

- *AnExample* - this route has a path of *{\*\*catch-all}* which means that it will match any path, unless there is another more specific route.
   It routes to a cluster named *example* which has a single destination of http://example.com
- *route2* - this route matches a path of */something/{\*any}* which means that it will match any path that begins with "/something/". 
    As its a more specific route, it will match before the route above, even though it is listed second. 
    This routes to a cluster named *cluster2* with 2 destinations. 
    It will load balance between those 2 destinations using a Power of two choices algorithm. 
    That algorithm is best with more than 2 choices, but shows how to specify an algorithm in config.

**Note:** The destination addresses used in the sample are using DNS names rather than IP addresses, this is so that the sample can be run and used without further changes. In a typical deployment, the destination servers should be specified with protocol, IP & ports, such as "https://123.4.5.6:7890"

The proxy will listen to HTTP requests on port 5000, and HTTPS on port 5001. These are changeable via the URLs property in config, and can be limited to just one protocol if required.

## Files
- [BasicYarpSample.csproj](BasicYarpSample.csproj) - A C# project file (conceptually similar to a make file) that tells it to target the .NET 8 runtime, and to reference the proxy library from [nuget](https://www.nuget.org/packages/Yarp.ReverseProxy/) (.NET's package manager).
- [Program.cs](Program.cs) - Provides the main entrypoint for .NET which uses a WebApplication to initialize the server which listens for http requests. This is also used to configure and control how http requests are handled by the server. In this sample, it does the bare minimum of:
  - Adding proxy functionality to the services collection.
  - Specifying that the proxy configuration will come from the config file (alternatively it could be specified via code).
  - Telling ASP.NET to use its routing service, to register the routes from YARP into its routing table, and use YARP to handle those requests.
- [appsettings.json](appsettings.json) - The configuration file for the .NET app, including sections for Kestrel, logging and the YARP proxy configuration. 
- [Properties/launchSettings.json](Properties/launchSettings.json) - A configuration file used by Visual Studio to tell it how to start the app when debugging.

## Getting started

### Command line

* Download and install the .NET SDK (free) from https://dotnet.microsoft.com/download if not already installed. Versions are available for Windows, Linux and MacOS.
* Clone or extract a zip of the sample files.
* Use ```dotnet run``` either within the sample folder or passing in the path to the .csproj file to start the server.
* File change notification is used for the appsettings.json file so changes can be made on the fly.


### Visual Studio Code
* Download and install Visual Studio Code (free) from https://code.visualstudio.com/ - versions are available for Windows, Linux and MacOS.
* Download and install the .NET SDK from https://dotnet.microsoft.com/download if not already installed. Versions are available for Windows, Linux and MacOS.
* Open the folder for the sample in VS Code (File->Open Folder).
* Press F5 to debug, or Ctrl + F5 to run the sample without debugging.

### Visual Studio

* Download and install Visual Studio from https://visualstudio.microsoft.com/ - versions are available for Windows and MacOS, including a free community edition.
* Open the project file.
* Press F5 to debug, or Ctrl + F5 to run the sample without debugging.

## Things to try
- Change the ports the proxy listens on using the URLs property in configuration or on the command line.
- Change the routes and destinations used by the proxy.
- A web server sample is available in the [SampleServer](../SampleServer) folder. It will output the request headers as part of the response body so they can be examined with a browser.
    - The URLs the server listens to can be changed on the command line, so that multiple instances can be run. 
     eg ```dotnet run --project ../SampleServer --Urls "http://localhost:10000;https://localhost:10010"```
