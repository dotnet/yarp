// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Yarp.Kubernetes.Tests;

namespace Yarp.Kubernetes.Controller.Certificates.Tests;

public class CertificateHelperTests
{
    private readonly Mock<ILogger<CertificateHelper>> _mockLogger;
    private readonly ICertificateHelper _certificateHelper;
    private readonly byte[] _pemPrivateKey;
    private readonly byte[] _pemCert;
    private readonly byte[] _derPrivateKey;
    private readonly byte[] _derCert;

    public CertificateHelperTests()
    {
        _mockLogger = new Mock<ILogger<CertificateHelper>>();

        _certificateHelper = new CertificateHelper(_mockLogger.Object);

        _pemCert = ReadManifestData(".Certificates.cert.pem");
        _pemPrivateKey = ReadManifestData(".Certificates.key.pem");
        _derCert = ReadManifestData(".Certificates.cert.der");
        _derPrivateKey = ReadManifestData(".Certificates.key.der");
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(false, false, false)]
    public void CertificateConversionFromPem(bool loadCert, bool loadKey, bool expectCert)
    {
        // Arrange
        var cert = loadCert ? _pemCert : (byte[])null;
        var key = loadKey ? _pemPrivateKey : (byte[])null;

        var secret = KubeResourceGenerator.CreateSecret("yarp-ingress-tls", "default", cert, key);
        var namespacedName = NamespacedName.From(secret);

        // Act
        var actualCertificate = _certificateHelper.ConvertCertificate(namespacedName, secret);

        // Assert
        if (expectCert)
        {
            Assert.NotNull(actualCertificate);
        }
        else
        {
            Assert.Null(actualCertificate);
        }
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(false, false, false)]
    public void CertificateConversionFromDer(bool loadCert, bool loadKey, bool expectCert)
    {
        // Arrange
        var cert = loadCert ? _derCert : (byte[])null;
        var key = loadKey ? _derPrivateKey : (byte[])null;

        var secret = KubeResourceGenerator.CreateSecret("yarp-ingress-tls", "default", cert, key);
        var namespacedName = NamespacedName.From(secret);

        // Act
        var actualCertificate = _certificateHelper.ConvertCertificate(namespacedName, secret);

        // Assert
        if (expectCert)
        {
            Assert.NotNull(actualCertificate);
        }
        else
        {
            Assert.Null(actualCertificate);
        }
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
