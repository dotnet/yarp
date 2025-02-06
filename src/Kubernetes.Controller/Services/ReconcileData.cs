// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Yarp.Kubernetes.Controller.Caching;

namespace Yarp.Kubernetes.Controller.Services;

/// <summary>
/// ReconcileData is the information returned from <see cref="ICache.TryGetReconcileData(Yarp.Kubernetes.Controller.NamespacedName, out ReconcileData)"/>
/// and needed by <see cref="IReconciler.ProcessAsync(System.Threading.CancellationToken)"/>.
/// </summary>
public struct ReconcileData
{
    public ReconcileData(IngressData ingress, List<ServiceData> services, List<Endpoints> endpoints)
    {
        Ingress = ingress;
        ServiceList = services;
        EndpointsList = endpoints;
    }

    public IngressData Ingress { get; }
    public List<ServiceData> ServiceList { get; }
    public List<Endpoints> EndpointsList { get; }
}
