// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Hosting;

namespace Yarp.Kubernetes.Controller.Certificates;

internal partial class ServerCertificateSelector
    : BackgroundService
    , IServerCertificateSelector
{
    private readonly ConcurrentDictionary<NamespacedName, X509Certificate2> _certificates = new();
    private bool _hasBeenUpdated;

    private ImmutableX509CertificateCache _certificateStore = new(Array.Empty<X509Certificate2>());

    public void AddCertificate(NamespacedName certificateName, X509Certificate2 certificate)
    {
        _certificates[certificateName] = certificate;
        _hasBeenUpdated = true;
    }

    public X509Certificate2 GetCertificate(ConnectionContext connectionContext, string domainName)
    {
        return _certificateStore.GetCertificate(domainName);
    }

    public void RemoveCertificate(NamespacedName certificateName)
    {
        _ = _certificates.TryRemove(certificateName, out _);
        _hasBeenUpdated = true;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Poll every 10 seconds for updates to
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            if (_hasBeenUpdated)
            {
                _hasBeenUpdated = false;
                _certificateStore = new ImmutableX509CertificateCache(_certificates.Values);
            }
        }
    }
}


