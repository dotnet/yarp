// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;

namespace Yarp.Kubernetes.Controller.Dispatching;

/// <summary>
/// IDispatcher is a service interface to bridge outgoing data to the
/// current connections.
/// </summary>
public interface IDispatcher
{
    Task AttachAsync(IDispatchTarget target, CancellationToken cancellationToken);
    void Detach(IDispatchTarget target);
    Task SendAsync(byte[] utf8Bytes, CancellationToken cancellationToken);
}
