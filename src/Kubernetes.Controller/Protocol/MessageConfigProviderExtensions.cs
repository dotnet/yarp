// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Extensions.DependencyInjection;
using Yarp.Kubernetes.Controller.Configuration;
using Yarp.ReverseProxy.Configuration;

namespace Yarp.Kubernetes.Protocol;

public static class MessageConfigProviderExtensions
{
    public static IReverseProxyBuilder LoadFromMessages(this IReverseProxyBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var provider = new KubernetesConfigProvider();
        builder.Services.AddSingleton<IProxyConfigProvider>(provider);
        builder.Services.AddSingleton<IUpdateConfig>(provider);
        return builder;
    }
}
