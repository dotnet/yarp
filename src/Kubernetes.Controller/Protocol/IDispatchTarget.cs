// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;

namespace Yarp.Kubernetes.Controller.Dispatching;

/// <summary>
/// IDispatchTarget is what an <see cref="IDispatcher"/> will use to
/// dispatch information.
/// </summary>
public interface IDispatchTarget
{
    public Task SendAsync(byte[] utf8Bytes, CancellationToken cancellationToken);
}
