// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using k8s.Models;
using Microsoft.Extensions.Logging;

namespace Yarp.Kubernetes.Controller.Certificates;

internal class TlsSecretCertificateParser : ITlsSecretCertificateParser
{
    private const string TlsCertKey = "tls.crt";
    private const string TlsPrivateKeyKey = "tls.key";

    private readonly ILogger<TlsSecretCertificateParser> _logger;

    public TlsSecretCertificateParser(ILogger<TlsSecretCertificateParser> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    public TlsCertificateChain ConvertCertificate(
        NamespacedName namespacedName, V1Secret secret)
    {
        if (secret?.Data is null ||
            !secret.Data.TryGetValue(TlsCertKey, out var certificate) || certificate.Length == 0 ||
            !secret.Data.TryGetValue(TlsPrivateKeyKey, out var privateKey) || privateKey.Length == 0)
        {
            _logger.LogWarning("TLS secret '{NamespacedName}' contains invalid data.", namespacedName);
            return null;
        }

        // Only PEM is supported (https://kubernetes.io/docs/concepts/configuration/secret/#tls-secrets);
        // tls.crt may contain the full chain: leaf first, followed by intermediates.
        var certificateString = Encoding.UTF8.GetString(certificate);
        var privateKeyString = Encoding.UTF8.GetString(privateKey);

        X509Certificate2 finalLeafCertificate = null;
        // TODO: check the certificate file follows PEM format: 1) Leaf, 2) Intermediate, optional 3) Root, optional certificates.
        var allCertificates = new X509Certificate2Collection();
        try
        {
            finalLeafCertificate = X509Certificate2.CreateFromPem(certificateString, privateKeyString);
            if (OperatingSystem.IsWindows())
            {
                using var oldLeafCertificate = finalLeafCertificate;
                finalLeafCertificate = X509CertificateLoader.LoadPkcs12(
                    oldLeafCertificate.Export(X509ContentType.Pkcs12), password: null);
            }

            allCertificates.ImportFromPem(certificateString);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to convert secret '{NamespacedName}'", namespacedName);

            finalLeafCertificate?.Dispose();
            DisposeCertificateCollection(allCertificates);

            return null;
        }

        var finalIntermediateCertificates = new X509Certificate2Collection();
        // [0] is the key-less copy of the leaf; ownership of [1..] moved to finalIntermediateCertificates.
        for (var i = 0; i < allCertificates.Count; ++i)
        {
            if (i == 0)
            {
                allCertificates[i].Dispose();
                continue;
            }

            finalIntermediateCertificates.Add(allCertificates[i]);
        }

        return new TlsCertificateChain(finalLeafCertificate, finalIntermediateCertificates);
    }

    private static void DisposeCertificateCollection(X509Certificate2Collection certificateCollection)
    {
        foreach (var certificate in certificateCollection)
        {
            certificate.Dispose();
        }
    }
}
