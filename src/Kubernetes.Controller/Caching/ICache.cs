// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using k8s;
using k8s.Models;
using System.Collections.Generic;
using System.Collections.Immutable;
using Yarp.Kubernetes.Controller.Services;

namespace Yarp.Kubernetes.Controller.Caching;

/// <summary>
/// ICache service interface holds onto the least amount of data necessary
/// for <see cref="IReconciler"/> to process work.
/// </summary>
public interface ICache
{
    void Update(WatchEventType eventType, V1IngressClass ingressClass);
    bool Update(WatchEventType eventType, V1Ingress ingress);
    ImmutableList<string> Update(WatchEventType eventType, V1Service service);
    ImmutableList<string> Update(WatchEventType eventType, V1Endpoints endpoints);
    void Update(WatchEventType eventType, V1Secret secret);
    bool TryGetReconcileData(NamespacedName key, out ReconcileData data);
    void GetKeys(List<NamespacedName> keys);
    IEnumerable<IngressData> GetIngresses();
}
