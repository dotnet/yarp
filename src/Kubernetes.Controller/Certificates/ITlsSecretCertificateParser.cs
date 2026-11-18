// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography.X509Certificates;
using k8s.Models;

namespace Yarp.Kubernetes.Controller.Certificates;

/// <summary>
/// A mechanism for parsing a Kubernetes TLS secret into its leaf and intermediate certificates.
/// </summary>
public interface ITlsSecretCertificateParser
{
    /// <summary>
    /// Parses the certificate chain contained in a Kubernetes TLS secret.
    /// </summary>
    /// <param name="namespacedName">An identifier for the secret.</param>
    /// <param name="secret">The TLS secret containing the certificate and private key data.</param>
    /// <returns>
    /// A <see cref="TlsCertificateChain"/> containing the leaf certificate (with its private key) and any
    /// intermediate certificates, or <see langword="null"/> if the secret is missing certificate data or
    /// cannot be parsed. The caller owns the returned instance and is responsible for disposing it.
    /// </returns>
    public TlsCertificateChain ConvertCertificate(NamespacedName namespacedName, V1Secret secret);
}
