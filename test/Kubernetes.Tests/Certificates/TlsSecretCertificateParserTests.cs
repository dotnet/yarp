// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Yarp.Kubernetes.Tests;

namespace Yarp.Kubernetes.Controller.Certificates.Tests;

public class TlsSecretCertificateParserTests
{
    private readonly NullLogger<TlsSecretCertificateParser> _logger = new NullLogger<TlsSecretCertificateParser>();
    private readonly ITlsSecretCertificateParser _tlsSecretCertificateParser;
    private readonly byte[] _pemPrivateKey;
    private readonly byte[] _pemCert;
    private readonly byte[] _pemFullChainCert;
    private readonly byte[] _pemFullChainPrivateKey;
    private readonly byte[] _derPrivateKey;
    private readonly byte[] _derCert;

    public TlsSecretCertificateParserTests()
    {
        _tlsSecretCertificateParser = new TlsSecretCertificateParser(_logger);
        _pemCert = ReadManifestData(".Certificates.cert.pem");
        _pemPrivateKey = ReadManifestData(".Certificates.key.pem");
        _pemFullChainCert = ReadManifestData(".Certificates.fullChainCert.pem");
        _pemFullChainPrivateKey = ReadManifestData(".Certificates.fullChainKey.pem");
        _derCert = ReadManifestData(".Certificates.cert.der");
        _derPrivateKey = ReadManifestData(".Certificates.key.der");
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(false, false, false)]
    public void CertificateConversionFromFullChainPem(bool loadCert, bool loadKey, bool expectCert)
    {
        // Arrange
        var cert = loadCert ? _pemFullChainCert : null;
        var key = loadKey ? _pemFullChainPrivateKey : null;

        var secret = KubeResourceGenerator.CreateSecret("yarp-ingress-tls", "default", cert, key);
        var namespacedName = NamespacedName.From(secret);

        // Act
        var tlsCertificateChain = _tlsSecretCertificateParser.ConvertCertificate(namespacedName, secret);

        // Assert
        if (expectCert)
        {
            Assert.NotNull(tlsCertificateChain.LeafCertificate);
            Assert.NotNull(tlsCertificateChain.IntermediateCertificates);
            Assert.NotEmpty(tlsCertificateChain.IntermediateCertificates);
            Assert.Single(tlsCertificateChain.IntermediateCertificates);
        }
        else
        {
            Assert.Null(tlsCertificateChain);
        }
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(false, false, false)]
    public void CertificateConversionFromPem(bool loadCert, bool loadKey, bool expectCert)
    {
        // Arrange
        var cert = loadCert ? _pemCert : null;
        var key = loadKey ? _pemPrivateKey : null;

        var secret = KubeResourceGenerator.CreateSecret("yarp-ingress-tls", "default", cert, key);
        var namespacedName = NamespacedName.From(secret);

        // Act
        var tlsCertificateChain = _tlsSecretCertificateParser.ConvertCertificate(namespacedName, secret);

        // Assert
        if (expectCert)
        {
            Assert.NotNull(tlsCertificateChain.LeafCertificate);
            Assert.NotNull(tlsCertificateChain.IntermediateCertificates);
            Assert.Empty(tlsCertificateChain.IntermediateCertificates);
        }
        else
        {
            Assert.Null(tlsCertificateChain);
        }
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void CertificateConversionFromHeaderlessDerIsRejected(bool loadCert, bool loadKey)
    {
        // Arrange
        var cert = loadCert ? _derCert : null;
        var key = loadKey ? _derPrivateKey : null;

        var secret = KubeResourceGenerator.CreateSecret("yarp-ingress-tls", "default", cert, key);
        var namespacedName = NamespacedName.From(secret);

        // Act
        var tlsCertificateChain = _tlsSecretCertificateParser.ConvertCertificate(namespacedName, secret);

        // Assert
        Assert.Null(tlsCertificateChain);
    }

    private static byte[] ReadManifestData(string ending)
    {
        var assembly = typeof(CertificateHelperTests).Assembly;
        var resourceName = assembly.GetManifestResourceNames().Single(str => str.EndsWith(ending));
        var manifestStream = assembly.GetManifestResourceStream(resourceName);

        using var reader = new StreamReader(manifestStream);
        return Encoding.UTF8.GetBytes(reader.ReadToEnd());
    }
}
