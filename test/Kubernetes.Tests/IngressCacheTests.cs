// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Yarp.Kubernetes.Controller;
using Yarp.Kubernetes.Controller.Caching;
using Yarp.Kubernetes.Controller.Certificates;
using Yarp.Kubernetes.Tests.Utils;

namespace Yarp.Kubernetes.Tests;

public class IngressCacheTests
{
    private readonly Mock<IOptions<YarpOptions>> _mockOptions;
    private readonly Mock<IServerCertificateSelector> _certificateSelector;
    private readonly Mock<ICertificateHelper> _certificateHelper;
    private readonly IngressCache _cacheUnderTest;
    private readonly X509Certificate2 _localhostCertificate;

    public IngressCacheTests(ITestOutputHelper output)
    {
        var logger = new TestLogger<IngressCache>(output);
        _mockOptions = new Mock<IOptions<YarpOptions>>();
        _certificateSelector = new Mock<IServerCertificateSelector>();
        _certificateHelper = new Mock<ICertificateHelper>();

        _mockOptions.SetupGet(o => o.Value).Returns(new YarpOptions { ControllerClass = "microsoft.com/ingress-yarp", DefaultSslCertificate = "default/yarp-ingress-tls" });

        _cacheUnderTest = new IngressCache(_mockOptions.Object, _certificateSelector.Object, _certificateHelper.Object, logger);

        // Generate a certificate for testing
        var ecdsa = ECDsa.Create();
        var req = new CertificateRequest("cn=localhost", ecdsa, HashAlgorithmName.SHA256);
        _localhostCertificate = req.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddYears(5));
    }

    [Theory]
    [InlineData("yarp", "microsoft.com/ingress-yarp", false, 1)]
    [InlineData("yarp", "microsoft.com/ingress-yarp", true, 1)]
    [InlineData("yarp-internal", "microsoft.com/ingress-yarp-internal", false, 0)]
    [InlineData("yarp-internal", "microsoft.com/ingress-yarp-internal", true, 0)]
    [InlineData(null, null, false, 0)]
    public void IngressWithClassAnnotationTests(string ingressClassName, string controllerName, bool? isDefault, int expectedIngressCount)
    {
        // Arrange
        if (controllerName is not null)
        {
            var ingressClass = KubeResourceGenerator.CreateIngressClass(ingressClassName, controllerName, isDefault);
            _cacheUnderTest.Update(WatchEventType.Added, ingressClass);
        }

        var ingress = KubeResourceGenerator.CreateIngress("ingress-with-class", "ns-test", "yarp");

        // Act
        _cacheUnderTest.Update(WatchEventType.Added, ingress);

        // Assert
        var ingresses = _cacheUnderTest.GetIngresses().ToArray();

        Assert.Equal(expectedIngressCount, ingresses.Length);
    }

    [Theory]
    [InlineData("yarp", "microsoft.com/ingress-yarp", true, 1)]
    [InlineData("yarp", "microsoft.com/ingress-yarp", false, 0)]
    [InlineData("yarp-internal", "microsoft.com/ingress-yarp-internal", false, 0)]
    [InlineData("yarp-internal", "microsoft.com/ingress-yarp-internal", true, 0)]
    [InlineData(null, null, false, 0)]
    public void IngressWithoutClassAnnotationTests(string ingressClassName, string controllerName, bool? isDefault, int expectedIngressCount)
    {
        // Arrange
        if (controllerName is not null)
        {
            var ingressClass = KubeResourceGenerator.CreateIngressClass(ingressClassName, controllerName, isDefault);
            _cacheUnderTest.Update(WatchEventType.Added, ingressClass);
        }

        var ingress = KubeResourceGenerator.CreateIngress("ingress-without-class", "ns-test", null);

        // Act
        _cacheUnderTest.Update(WatchEventType.Added, ingress);

        // Assert
        var ingresses = _cacheUnderTest.GetIngresses().ToArray();

        Assert.Equal(expectedIngressCount, ingresses.Length);
    }

    [Fact]
    public void IngressModifiedToRemoveClass()
    {
        // Arrange
        var ingressClass = KubeResourceGenerator.CreateIngressClass("yarp", "microsoft.com/ingress-yarp", false);
        _cacheUnderTest.Update(WatchEventType.Added, ingressClass);

        var ingress = KubeResourceGenerator.CreateIngress("ingress-with-class", "ns-test", "yarp");
        _cacheUnderTest.Update(WatchEventType.Added, ingress);

        // Act
        ingress.Spec.IngressClassName = null;
        _cacheUnderTest.Update(WatchEventType.Modified, ingress);

        // Assert
        var ingresses = _cacheUnderTest.GetIngresses().ToArray();
        Assert.Empty(ingresses);
    }

    [Fact]
    public void IngressClassModifiedToAddDefault()
    {
        // Arrange
        var ingressClass = KubeResourceGenerator.CreateIngressClass("yarp", "microsoft.com/ingress-yarp", false);
        var ingress = KubeResourceGenerator.CreateIngress("ingress-with-class", "ns-test", "yarp");

        _cacheUnderTest.Update(WatchEventType.Added, ingressClass);

        // Act
        ingressClass.Metadata.Annotations.Add("ingressclass.kubernetes.io/is-default-class", "true");

        _cacheUnderTest.Update(WatchEventType.Modified, ingressClass);
        _cacheUnderTest.Update(WatchEventType.Added, ingress);

        // Assert
        var ingresses = _cacheUnderTest.GetIngresses().ToArray();
        Assert.Single(ingresses);
    }

    [Fact]
    public void IngressClassDeleted()
    {
        // Arrange
        var ingressClass = KubeResourceGenerator.CreateIngressClass("yarp", "microsoft.com/ingress-yarp", true);
        var ingress = KubeResourceGenerator.CreateIngress("ingress-with-class", "ns-test", "yarp");

        _cacheUnderTest.Update(WatchEventType.Added, ingressClass);

        // Act
        _cacheUnderTest.Update(WatchEventType.Deleted, ingressClass);
        _cacheUnderTest.Update(WatchEventType.Added, ingress);

        // Assert
        var ingresses = _cacheUnderTest.GetIngresses().ToArray();
        Assert.Empty(ingresses);
    }

    [Fact]
    public void SecretNotMatchDefaultNameIgnored()
    {
        // Arrange
        var secret = KubeResourceGenerator.CreateSecret("yarp", "not-my-tls");

        // Act
        _cacheUnderTest.Update(WatchEventType.Added, secret);

        // Assert
        _certificateHelper.Verify(h => h.ConvertCertificate(It.IsAny<NamespacedName>(), It.IsAny<V1Secret>()), Times.Never);
        _certificateSelector.Verify(s => s.AddCertificate(It.IsAny<NamespacedName>(), It.IsAny<X509Certificate2>()), Times.Never);
    }

    [Fact]
    public void SecretMatchDefaultNameAdded()
    {
        // Arrange
        var secret = KubeResourceGenerator.CreateSecret("yarp-ingress-tls", "default");
        _certificateHelper
            .Setup(h => h.ConvertCertificate(It.Is<NamespacedName>(n => n.Name == "yarp-ingress-tls" && n.Namespace == "default"), It.Is<V1Secret>(s => s == secret)))
            .Returns(_localhostCertificate);

        // Act
        _cacheUnderTest.Update(WatchEventType.Added, secret);

        // Assert
        _certificateHelper.Verify(h => h.ConvertCertificate(It.Is<NamespacedName>(n => n.Name == "yarp-ingress-tls" && n.Namespace == "default"), It.Is<V1Secret>(s => s == secret)), Times.Once);
        _certificateSelector.Verify(s => s.AddCertificate(It.Is<NamespacedName>(n => n.Name == "yarp-ingress-tls" && n.Namespace == "default"), It.Is<X509Certificate2>(c => c == _localhostCertificate)), Times.Once);
    }

    [Fact]
    public void SecretMatchDefaultNameModified()
    {
        // Arrange
        var secret = KubeResourceGenerator.CreateSecret("yarp-ingress-tls", "default");
        _certificateHelper
            .Setup(h => h.ConvertCertificate(It.Is<NamespacedName>(n => n.Name == "yarp-ingress-tls" && n.Namespace == "default"), It.Is<V1Secret>(s => s == secret)))
            .Returns(_localhostCertificate);

        // Act
        _cacheUnderTest.Update(WatchEventType.Modified, secret);

        // Assert
        _certificateHelper.Verify(h => h.ConvertCertificate(It.Is<NamespacedName>(n => n.Name == "yarp-ingress-tls" && n.Namespace == "default"), It.Is<V1Secret>(s => s == secret)), Times.Once);
        _certificateSelector.Verify(s => s.AddCertificate(It.Is<NamespacedName>(n => n.Name == "yarp-ingress-tls" && n.Namespace == "default"), It.Is<X509Certificate2>(c => c == _localhostCertificate)), Times.Once);
    }

    [Fact]
    public void SecretMatchDefaultNameRemoved()
    {
        // Arrange
        var secret = KubeResourceGenerator.CreateSecret("yarp-ingress-tls", "default");
        _certificateHelper
            .Setup(h => h.ConvertCertificate(It.Is<NamespacedName>(n => n.Name == "yarp-ingress-tls" && n.Namespace == "default"), It.Is<V1Secret>(s => s == secret)))
            .Returns(_localhostCertificate);

        // Act
        _cacheUnderTest.Update(WatchEventType.Deleted, secret);

        // Assert
        _certificateHelper.Verify(h => h.ConvertCertificate(It.Is<NamespacedName>(n => n.Name == "yarp-ingress-tls" && n.Namespace == "default"), It.Is<V1Secret>(s => s == secret)), Times.Once);
        _certificateSelector.Verify(s => s.RemoveCertificate(It.Is<NamespacedName>(n => n.Name == "yarp-ingress-tls" && n.Namespace == "default")), Times.Once);
    }

    [Fact]
    public void SecretMatchDefaultNameCantConvertNotAdded()
    {
        // Arrange
        var secret = KubeResourceGenerator.CreateSecret("yarp-ingress-tls", "default");
        _certificateHelper
            .Setup(h => h.ConvertCertificate(It.Is<NamespacedName>(n => n.Name == "yarp-ingress-tls" && n.Namespace == "default"), It.Is<V1Secret>(s => s == secret)))
            .Returns((X509Certificate2)null);

        // Act
        _cacheUnderTest.Update(WatchEventType.Added, secret);

        // Assert
        _certificateHelper.Verify(h => h.ConvertCertificate(It.Is<NamespacedName>(n => n.Name == "yarp-ingress-tls" && n.Namespace == "default"), It.Is<V1Secret>(s => s == secret)), Times.Once);
        _certificateSelector.Verify(s => s.AddCertificate(It.IsAny<NamespacedName>(), It.IsAny<X509Certificate2>()), Times.Never);
    }
}
