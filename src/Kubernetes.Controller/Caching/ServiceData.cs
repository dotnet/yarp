// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using k8s.Models;
using System;

namespace Yarp.Kubernetes.Controller.Caching;

/// <summary>
/// Holds data needed from a <see cref="V1Service"/> resource.
/// </summary>
public struct ServiceData
{
    public ServiceData(V1Service service)
    {
        ArgumentNullException.ThrowIfNull(service);

        Spec = service.Spec;
        Metadata = service.Metadata;
    }

    public V1ServiceSpec Spec { get; set; }
    public V1ObjectMeta Metadata { get; set; }
}
