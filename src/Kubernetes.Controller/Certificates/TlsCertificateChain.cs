// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Security.Cryptography.X509Certificates;

namespace Yarp.Kubernetes.Controller.Certificates;

/// <summary>
/// A leaf certificate (with its private key) and its intermediate certificates parsed from a Kubernetes TLS secret.
/// This instance owns the certificates and disposes them when it is disposed.
/// </summary>
public sealed class TlsCertificateChain : IDisposable
{
    public X509Certificate2 LeafCertificate { get; }
    public X509Certificate2Collection IntermediateCertificates { get; }

    public TlsCertificateChain(X509Certificate2 leafCertificate, X509Certificate2Collection intermediateCertificates)
    {
        ArgumentNullException.ThrowIfNull(leafCertificate);
        ArgumentNullException.ThrowIfNull(intermediateCertificates);

        LeafCertificate = leafCertificate;
        IntermediateCertificates = intermediateCertificates;
    }

    public void Dispose()
    {
        LeafCertificate.Dispose();
        foreach (var certificate in IntermediateCertificates)
        {
            certificate.Dispose();
        }
    }
}
