// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;
using Yarp.Kubernetes.Controller.Hosting;

namespace Yarp.Kubernetes.Tests.Hosting.Fakes;

public class FakeBackgroundHostedService : BackgroundHostedService
{
    private readonly TestLatches _context;

    public FakeBackgroundHostedService(
        TestLatches context,
        IHostApplicationLifetime hostApplicationLifetime,
        ILogger<FakeBackgroundHostedService> logger)
        : base(hostApplicationLifetime, logger)
    {
        _context = context;
    }

    public override async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            _context.RunEnter.Signal();
            await _context.RunResult.WhenSignalAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _context.RunExit.Signal();
        }
    }
}
