// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

namespace Yarp.Kubernetes.Controller.Hosting;

/// <summary>
/// Delegates start and stop calls to service-specific interface.
/// </summary>
/// <typeparam name="TService">The service interface to delegate onto.</typeparam>
public class HostedServiceAdapter<TService> : IHostedService
    where TService : IHostedService
{
    private readonly TService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="HostedServiceAdapter{TInterface}" /> class.
    /// </summary>
    /// <param name="service">The service interface to delegate onto.</param>
    public HostedServiceAdapter(TService service) => _service = service;

    public Task StartAsync(CancellationToken cancellationToken) => _service.StartAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => _service.StopAsync(cancellationToken);
}
