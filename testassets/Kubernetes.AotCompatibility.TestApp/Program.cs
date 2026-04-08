// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// This app is used to test AOT compatibility of Yarp.Kubernetes.Controller.
// It exercises the main public API surface to ensure no AOT/trimming warnings
// are emitted at build or publish time.

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Yarp.Kubernetes.Protocol;

// Combined controller scenario (ingress controller + YARP reverse proxy)
{
    var builder = WebApplication.CreateBuilder(args);
    builder.WebHost.UseKubernetesReverseProxyCertificateSelector();
    builder.Services.AddKubernetesReverseProxy(builder.Configuration);
    _ = builder.Build();
}

// Monitor-only scenario (ingress monitor + dispatch controller)
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Services.AddKubernetesIngressMonitor(builder.Configuration);
    builder.Services.AddControllers().AddKubernetesDispatchController();
    _ = builder.Build();
}

// Receiver scenario (side-car that receives config from ingress monitor)
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Services.Configure<ReceiverOptions>(builder.Configuration.Bind);
    builder.Services.AddHostedService<Receiver>();
    builder.Services.AddReverseProxy().LoadFromMessages();
    _ = builder.Build();
}
