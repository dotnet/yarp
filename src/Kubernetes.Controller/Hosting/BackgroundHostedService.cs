// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Yarp.Kubernetes.Controller.Hosting;

/// <summary>
/// Class BackgroundHostedService.
/// Implements the <see cref="IHostedService" />
/// Implements the <see cref="IDisposable" />.
/// </summary>
/// <seealso cref="IHostedService" />
/// <seealso cref="IDisposable" />
public abstract class BackgroundHostedService : IHostedService, IDisposable
{
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly CancellationTokenRegistration _hostApplicationStoppingRegistration;
    private readonly CancellationTokenSource _runCancellation = new CancellationTokenSource();
    private readonly string _serviceTypeName;
    private bool _disposedValue;
    private Task _runTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="BackgroundHostedService"/> class.
    /// </summary>
    /// <param name="hostApplicationLifetime">The host application lifetime.</param>
    /// <param name="logger">The logger.</param>
    protected BackgroundHostedService(
        IHostApplicationLifetime hostApplicationLifetime,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(hostApplicationLifetime);
        ArgumentNullException.ThrowIfNull(logger);
        _hostApplicationLifetime = hostApplicationLifetime;
        Logger = logger;

        // register the stoppingToken to become cancelled as soon as the
        // shutdown sequence is initiated.
        _hostApplicationStoppingRegistration = _hostApplicationLifetime.ApplicationStopping.Register(_runCancellation.Cancel);

        var serviceType = GetType();
        if (serviceType.IsGenericType)
        {
            _serviceTypeName = $"{serviceType.Name.Split('`').First()}<{string.Join(",", serviceType.GenericTypeArguments.Select(type => type.Name))}>";
        }
        else
        {
            _serviceTypeName = serviceType.Name;
        }
    }

    /// <summary>
    /// Gets or sets the logger.
    /// </summary>
    /// <value>The logger.</value>
    protected ILogger Logger { get; set; }

    /// <summary>
    /// Triggered when the application host is ready to start the service.
    /// </summary>
    /// <param name="cancellationToken">Indicates that the start process has been aborted.</param>
    /// <returns>Task.</returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // fork off a new async causality line beginning with the call to RunAsync
        _runTask = Task.Run(CallRunAsync, CancellationToken.None);

        // the rest of the startup sequence should proceed without delay
        return Task.CompletedTask;

        // entry-point to run async background work separated from the startup sequence
        async Task CallRunAsync()
        {
            // don't bother running in case of abnormally early shutdown
            _runCancellation.Token.ThrowIfCancellationRequested();

            try
            {
                Logger?.LogInformation(
                    new EventId(1, "RunStarting"),
                    "Calling RunAsync for {BackgroundHostedService}",
                    _serviceTypeName);

                try
                {
                    // call the overridden method
                    await RunAsync(_runCancellation.Token).ConfigureAwait(true);
                }
                finally
                {
                    Logger?.LogInformation(
                        new EventId(2, "RunComplete"),
                        "RunAsync completed for {BackgroundHostedService}",
                        _serviceTypeName);
                }
            }
            catch
            {
                if (!_hostApplicationLifetime.ApplicationStopping.IsCancellationRequested)
                {
                    // For any exception the application is instructed to tear down.
                    // this would normally happen if IHostedService.StartAsync throws, so it
                    // is a safe assumption the intent of an unhandled exception from background
                    // RunAsync is the same.
                    _hostApplicationLifetime.StopApplication();

                    Logger?.LogInformation(
                        new EventId(3, "RequestedStopApplication"),
                        "Called StopApplication for {BackgroundHostedService}",
                        _serviceTypeName);
                }

                throw;
            }
        }
    }

    /// <summary>
    /// stop as an asynchronous operation.
    /// </summary>
    /// <param name="cancellationToken">Indicates that the shutdown process should no longer be graceful.</param>
    /// <returns>A <see cref="Task" /> representing the result of the asynchronous operation.</returns>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            // signal for the RunAsync call to be completed
            _runCancellation.Cancel();

            // join the result of the RunAsync causality line back into the results of
            // this StopAsync call. this await statement will not complete until CallRunAsync
            // method has unwound and returned. if RunAsync completed by throwing an exception
            // it will be rethrown by this await. rethrown Exceptions will pass through
            // Hosting and may be caught by Program.Main.
            await _runTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // this exception is ignored - it's a natural result of cancellation token
        }
        finally
        {
            _runTask = null;
        }
    }

    /// <summary>
    /// Runs the asynchronous background work.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>A <see cref="Task" /> representing the result of the asynchronous operation.</returns>
    public abstract Task RunAsync(CancellationToken cancellationToken);

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                try
                {
                    _runCancellation.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    // ignore redundant exception to allow shutdown sequence to progress uninterrupted
                }

                try
                {
                    _hostApplicationStoppingRegistration.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    // ignore redundant exception to allow shutdown sequence to progress uninterrupted
                }
            }

            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
