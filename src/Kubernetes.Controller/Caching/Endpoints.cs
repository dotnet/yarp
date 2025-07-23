// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using k8s.Models;
using System;
using System.Collections.Generic;

namespace Yarp.Kubernetes.Controller.Caching;

/// <summary>
/// Holds data needed from a <see cref="V1Endpoints"/> resource.
/// </summary>
public struct Endpoints
{
    public Endpoints(V1Endpoints endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        Name = endpoints.Name();
        Subsets = endpoints.Subsets;
    }

    public string Name { get; set; }
    public IList<V1EndpointSubset> Subsets { get; }
}
