// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Connections;

namespace Yarp.Kubernetes.Controller.Certificates;

/// <summary>
/// A mechanism for obtaining server certificates dynamically based on the SNI domain name.
/// </summary>
public interface IServerCertificateSelector
{
    /// <summary>
    /// Retrieve a certificate using the provided domain name.
    /// </summary>
    /// <param name="connectionContext">The connection context.</param>
    /// <param name="domainName">The domain name.</param>
    /// <returns>Either returns the specific certificate for the domain name, a wildcard certificates, or no certificate.</returns>
    X509Certificate2 GetCertificate(ConnectionContext connectionContext, string domainName);

    /// <summary>
    /// Adds a certificate to the selector.
    /// </summary>
    /// <param name="certificateName">An identifier for the certificate that can be used to remove it.</param>
    /// <param name="certificate">The server certificate.</param>
    void AddCertificate(NamespacedName certificateName, X509Certificate2 certificate);

    /// <summary>
    /// Removes a certificate from the selector.
    /// </summary>
    /// <param name="certificateName">An identifier for the certificate that can be used to remove it.</param>
    void RemoveCertificate(NamespacedName certificateName);
}
