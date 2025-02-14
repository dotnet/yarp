// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using k8s.Models;
using System;

namespace Yarp.Kubernetes.Controller.Caching;

/// <summary>
/// Holds data needed from a <see cref="V1Ingress"/> resource.
/// </summary>
public struct IngressData
{
    public IngressData(V1Ingress ingress)
    {
        if (ingress is null)
        {
            throw new ArgumentNullException(nameof(ingress));
        }

        Spec = ingress.Spec;
        Metadata = ingress.Metadata;
    }

    public V1IngressSpec Spec { get; set; }
    public V1ObjectMeta Metadata { get; set; }
}
