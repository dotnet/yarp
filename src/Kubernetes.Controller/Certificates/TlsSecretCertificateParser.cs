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

    public (X509Certificate2 leafCertfiicate, X509Certificate2Collection intermediateCertificates) ConvertCertificate(
        NamespacedName namespacedName, V1Secret secret)
    {
        if (secret?.Data is null ||
            !secret.Data.TryGetValue(TlsCertKey, out var certificate) || certificate.Length == 0 ||
            !secret.Data.TryGetValue(TlsPrivateKeyKey, out var privateKey) || privateKey.Length == 0)
        {
            return (null, null);
        }

        var certificateString = EnsurePemFormat(certificate, "CERTIFICATE");
        var privateKeyString = EnsurePemFormat(privateKey, "PRIVATE KEY");

        X509Certificate2 finalLeafCertificate = null;
        // TODO: check the certificate file follows PEM format: 1) Leaf, 2) Intermediate, optional 3) Root, optional certificates.
        var allCertificates = new X509Certificate2Collection();
        try
        {
            finalLeafCertificate = X509Certificate2.CreateFromPem(certificateString, privateKeyString);
            if (OperatingSystem.IsWindows())
            {
                using var oldLeafCertificate = finalLeafCertificate;
                finalLeafCertificate = new X509Certificate2(finalLeafCertificate.Export(X509ContentType.Pkcs12));
            }

            allCertificates.ImportFromPem(certificateString);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to convert secret '{NamespacedName}'", namespacedName);

            finalLeafCertificate?.Dispose();
            DisposeCertificateCollection(allCertificates);

            return (null, null);
        }

        var finalIntermediateCertificates = new X509Certificate2Collection();
        // [0] is the key-less copy of the leaf; ownership of [1..] moved to finalCertificateCollection.
        for (var i = 0; i < allCertificates.Count; ++i)
        {
            if (i == 0)
            {
                allCertificates[i].Dispose();
                continue;
            }

            finalIntermediateCertificates.Add(allCertificates[i]);
        }

        return (finalLeafCertificate, finalIntermediateCertificates);
    }

    private static void DisposeCertificateCollection(X509Certificate2Collection certificateCollection)
    {
        foreach (var certificate in certificateCollection)
        {
            certificate.Dispose();
        }
    }

    /// <summary>
    /// Kubernetes Secrets should be stored in base-64 encoded DER format (see https://kubernetes.io/docs/concepts/configuration/secret/#tls-secrets)
    /// but need can be imported into a <see cref="X509Certificate2"/> object via PEM. Before this type of secret existed, an Opaque secret would be
    /// used containing the full PEM format, so it's possible that the incorrect format would be used.
    /// Doing it this way means we are more tolerant in handling certs in the wrong format.
    /// </summary>
    /// <param name="data">The raw data.</param>
    /// <param name="pemType">The type for the PEM header.</param>
    /// <returns>The certificate data in PEM format.</returns>
    private static string EnsurePemFormat(byte[] data, string pemType)
    {
        var der = Encoding.ASCII.GetString(data);
        if (!der.StartsWith("---", StringComparison.Ordinal))
        {
            // Convert from encoded DER to PEM
            return $"-----BEGIN {pemType}-----\n{der}\n-----END {pemType}-----";
        }

        return der;
    }
}
