// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using k8s.Models;
using Microsoft.Extensions.Logging;

namespace Yarp.Kubernetes.Controller.Certificates;

public class CertificateHelper : ICertificateHelper
{
    private const string TlsCertKey = "tls.crt";
    private const string TlsPrivateKeyKey = "tls.key";

    private readonly ILogger<CertificateHelper> _logger;

    public CertificateHelper(ILogger<CertificateHelper> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    public X509Certificate2 ConvertCertificate(NamespacedName namespacedName, V1Secret secret)
    {
        try
        {
            var cert = secret?.Data[TlsCertKey];
            var privateKey = secret?.Data[TlsPrivateKeyKey];

            if (cert == null || cert.Length == 0 || privateKey == null || privateKey.Length == 0)
            {
                _logger.LogWarning("TLS secret '{NamespacedName}' contains invalid data.", namespacedName);
                return null;
            }

            var certString = EnsurePemFormat(cert, "CERTIFICATE");
            var privateString = EnsurePemFormat(privateKey, "PRIVATE KEY");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Cert needs converting. Read https://github.com/dotnet/runtime/issues/23749#issuecomment-388231655
                using var convertedCertificate = X509Certificate2.CreateFromPem(certString, privateString);
                return new X509Certificate2(convertedCertificate.Export(X509ContentType.Pkcs12));
            }

            return X509Certificate2.CreateFromPem(certString, privateString);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to convert secret '{NamespacedName}'", namespacedName);
        }

        return null;
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
