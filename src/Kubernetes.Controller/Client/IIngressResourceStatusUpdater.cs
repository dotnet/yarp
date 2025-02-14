// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;

namespace Yarp.Kubernetes.Controller.Client;

public interface IIngressResourceStatusUpdater
{
    /// <summary>
    /// Updates the status of cached ingresses.
    /// </summary>
    Task UpdateStatusAsync(CancellationToken cancellationToken);
}
