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
    /// A tuple containing the leaf certificate (with its associated private key) and a collection of
    /// intermediate certificates, or <c>(null, null)</c> if the secret could not be parsed.
    /// </returns>
    public (X509Certificate2 leafCertfiicate, X509Certificate2Collection intermediateCertificates) ConvertCertificate(
        NamespacedName namespacedName, V1Secret secret);
}
