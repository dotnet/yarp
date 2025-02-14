# Samples

**Warning: Breaking Changes**

The samples in this folder are in sync with the main branch for YARP. If there have been breaking changes to the API or configuration, they may not match what is published on Nuget. To avoid disappointment, if using the samples with the YARP library published to Nuget, please change to the branch to match the latest release or preview, either using the branch dropdown or these links:

**[Samples folder in latest release/preview](https://github.com/dotnet/yarp/tree/release/latest/samples)**

**[Source zip for latest release, including samples](https://github.com/dotnet/yarp/releases/latest)**

----

The following samples are provided:

| Name | Description |
|------- | ----- |
| [Basic Yarp Sample](BasicYarpSample) | A simple sample that shows how to add YARP to the empty ASP.NET sample to create a fully functioning reverse proxy. | 
| [Configuration](ReverseProxy.Config.Sample) | Shows all the options that are available in the YARP config file |
| [Minimal](ReverseProxy.Minimal.Sample) | Shows a minimal config-based YARP application using .NET 6's [Minimal Hosting for ASP.NET Core](https://devblogs.microsoft.com/aspnet/asp-net-core-updates-in-net-6-preview-4/#introducing-minimal-apis) |
| [Http.sys Delegation](ReverseProxy.HttpSysDelegation.Sample) | Shows an example of using YARP to do Http.sys queue delegation in addtion to proxying. |
| [Transforms](ReverseProxy.Transforms.Sample) | Shows how to transform headers as part of the proxy operation | 
| [Code extensibility](ReverseProxy.Code.Sample) | Shows how you can extend YARP using a custom configuration provider, and a middleware component as part of the YARP pipeline |
| [Authentication & Authorization](ReverseProxy.Auth.Sample) | Shows how to add authentication and authorization for routes to the proxy |
| [Configuration Filter](ReverseProxy.ConfigFilter.Sample) | Shows how to use extensibility to modify configuration as its loaded from the configuration file. This sample implements an indirection to enable config values to be pulled from environment variables which can be useful in a cloud environment.  |
| [Metrics](ReverseProxy.Metrics.Sample) | Shows how to consume YARP telemetry. This sample collects detailed timings for the sub-operations involved in the proxy process. |
| [Using IHttpProxy Directly](ReverseProxy.Direct.Sample) | Shows how to use IHttpProxy, which performs the proxy operation, directly without using YARP's configuration, pipeline etc. |
| [Lets Encrypt](ReverseProxy.LetsEncrypt.Sample) | Shows how to use a certificate authority such as Lets Encrypt to set up TLS termination in YARP. |
| [Kubernetes Ingress](KubernetesIngress.Sample)  | Shows how to use YARP as a Kubernetes ingress controller  |
| [Prometheus](Prometheus) | Shows how to consume the YARP telemetry library and export metrics to external telemetry such as Prometheus |
