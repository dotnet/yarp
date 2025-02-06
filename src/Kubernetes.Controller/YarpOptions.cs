// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Yarp.Kubernetes.Controller;

public class YarpOptions
{
    /// <summary>
    /// Defines a name of the ingress controller. IngressClass ".spec.controller" field should match this.
    /// This field is required.
    /// </summary>
    public string ControllerClass { get; set; }

    public bool ServerCertificates { get; set; }

    public string DefaultSslCertificate { get; set; }

    /// <summary>
    /// Name of the Kubernetes Service the ingress controller is running in.
    /// This field is required.
    /// </summary>
    public string ControllerServiceName { get; set; }

    /// <summary>
    /// Namespace of the Kubernetes Service the ingress controller is running in.
    /// This field is required.
    /// </summary>
    public string ControllerServiceNamespace { get; set; }
}
