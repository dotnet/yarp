// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using k8s;

namespace Yarp.Kubernetes.Controller.Client;

/// <summary>
/// Class KubernetesClientOptions.
/// </summary>
public class KubernetesClientOptions
{
    /// <summary>
    /// Gets or sets the configuration.
    /// </summary>
    /// <value>The configuration.</value>
    public KubernetesClientConfiguration Configuration { get; set; }
}
