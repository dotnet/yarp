// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Xunit;
#nullable enable
namespace Yarp.Kubernetes.Controller.Certificates.Tests;

public class CertificateCacheTests
{

    private static readonly FakeCertificateCache Cache = new(
        new FakeCertificate("Acme", "mail.acme.com", "www.acme.com"),
        new FakeCertificate("Initech", "*.initech.com", "initech.com"),
        new FakeCertificate("Northwind", "*.northwind.com")
    );

    [Theory]
    [InlineData("www.acme.com", "Acme")]
    [InlineData("www.ACME.com", "Acme")]
    [InlineData("mail.acme.com", "Acme")]
    [InlineData("acme.com", null)]
    [InlineData("store.acme.com", null)]
    [InlineData("www.northwind.com", "Northwind")]
    [InlineData("mail.northwind.com", "Northwind")]
    [InlineData("northwind.com", null)]
    [InlineData("initech.com", "Initech")]
    [InlineData("www.initech.com", "Initech")]
    [InlineData("www.IniTech.coM", "Initech")]
    public void CertificateConversionFromPem(string requestedDomain, string? expectedCompanyName)
    {
        var certificate = Cache.GetCertificate(requestedDomain);
        if (expectedCompanyName != null)
        {
            Assert.Equal(expectedCompanyName, certificate?.Name);
        }
        else
        {
            Assert.Null(certificate?.Name);
        }
    }

    private record FakeCertificate(string Name, params string[] Domains);

    private class FakeCertificateCache(params IEnumerable<FakeCertificate> certificates)
        : ImmutableCertificateCache<FakeCertificate>(certificates, static cert => cert.Domains)
    {
        protected override FakeCertificate? GetDefaultCertificate()
        {
            return null;
        }
    }
}



