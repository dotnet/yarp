// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using k8s.Models;

namespace Yarp.Kubernetes.Controller.Caching;

/// <summary>
/// Holds data needed from a <see cref="V1IngressClass"/> resource.
/// </summary>
public struct IngressClassData
{
    public IngressClassData(V1IngressClass ingressClass)
    {
        ArgumentNullException.ThrowIfNull(ingressClass);

        IngressClass = ingressClass;
        IsDefault = GetDefaultAnnotation(ingressClass);
    }

    public V1IngressClass IngressClass { get; }

    public bool IsDefault { get; }

    private static bool GetDefaultAnnotation(V1IngressClass ingressClass)
    {
        var annotation = ingressClass.GetAnnotation("ingressclass.kubernetes.io/is-default-class");
        return string.Equals("true", annotation, StringComparison.OrdinalIgnoreCase);
    }
}
