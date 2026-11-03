// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

#nullable enable
namespace Yarp.Kubernetes.Controller.Certificates;

internal partial class ServerCertificateSelector
    : BackgroundService
    , IServerCertificateSelector
{
    private readonly ConcurrentDictionary<NamespacedName, X509Certificate2> _certificates = new();
    private bool _hasBeenUpdated;
    private string? _defaultCertificate;
    private readonly IDisposable? _defaultCertificateSubscription;

    private ImmutableX509CertificateCache _certificateStore = new(null, []);

    public ServerCertificateSelector(IOptionsMonitor<YarpOptions> options)
    {
        _defaultCertificateSubscription = options.OnChange(x =>
        {
            if (_defaultCertificate != x.DefaultSslCertificate)
            {
                _defaultCertificate = x.DefaultSslCertificate;
                _hasBeenUpdated = true;
            }
        });
    }

    public override void Dispose()
    {
        if (_defaultCertificateSubscription is {} subscription)
        {
            subscription.Dispose();
        }
        base.Dispose();
    }

    public void AddCertificate(NamespacedName certificateName, X509Certificate2 certificate)
    {
        _certificates[certificateName] = certificate;
        _hasBeenUpdated = true;
    }

    public X509Certificate2? GetCertificate(ConnectionContext connectionContext, string? domainName)
    {
        if (string.IsNullOrEmpty(domainName))
        {
            return _certificateStore.DefaultCertificate();
        }
        return _certificateStore.GetCertificate(domainName);
    }

    public void RemoveCertificate(NamespacedName certificateName)
    {
        _ = _certificates.TryRemove(certificateName, out _);
        _hasBeenUpdated = true;
    }

    [GeneratedRegex("(?<namespace>[a-z0-9\\-\\.]*)/(?<certificateName>[a-z0-9\\-\\.]*)", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex DefaultCertificateNameParser();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Poll every 10 seconds for updates to
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            if (_hasBeenUpdated)
            {
                _hasBeenUpdated = false;

                X509Certificate2? defaultCert = null;
                if (_defaultCertificate is { } certificateName
                    && DefaultCertificateNameParser().Match(certificateName) is { Success: true } match)
                {
                    var namespaceName = match.Groups["namespace"].Value;
                    var name = match.Groups["certificateName"].Value;
                    var certificateNamespacedName = new NamespacedName(namespaceName, name);

                    _ = _certificates.TryGetValue(certificateNamespacedName, out defaultCert);
                }

                _certificateStore = new ImmutableX509CertificateCache(defaultCert, _certificates.Values);
            }
        }
    }
}


