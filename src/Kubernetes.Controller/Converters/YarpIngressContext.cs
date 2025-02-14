// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Yarp.Kubernetes.Controller.Caching;

namespace Yarp.Kubernetes.Controller.Converters;

internal sealed class YarpIngressContext
{
    public YarpIngressContext(IngressData ingress, List<ServiceData> services, List<Endpoints> endpoints)
    {
        Ingress = ingress;
        Services = services;
        Endpoints = endpoints;
    }

    public YarpIngressOptions Options { get; set; } = new YarpIngressOptions();
    public IngressData Ingress { get; }
    public List<ServiceData> Services { get; }
    public List<Endpoints> Endpoints { get; }
}
