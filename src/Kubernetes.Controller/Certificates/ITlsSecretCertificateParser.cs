// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography.X509Certificates;
using k8s.Models;

namespace Yarp.Kubernetes.Controller.Certificates;

public interface ITlsSecretCertificateParser
{
    public (X509Certificate2 leafCertfiicate, X509Certificate2Collection intermediateCertificates) ConvertCertificate(
        NamespacedName namespacedName, V1Secret secret);
}
