// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using k8s;
using k8s.Models;

namespace Yarp.Kubernetes.Controller.Client;

/// <summary>
/// Provides a mechanism for <see cref="IResourceInformer{TResource}"/> to constrain search based on fields in the resource.
/// </summary>
public class ResourceSelector<TResource>
    where TResource : class, IKubernetesObject<V1ObjectMeta>, new()
{
    public ResourceSelector(string fieldSelector)
    {
        FieldSelector = fieldSelector;
    }

    public string FieldSelector { get; }
}
