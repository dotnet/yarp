// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Xunit;
using Yarp.Kubernetes.Controller;
using Yarp.Kubernetes.Controller.Caching;
using Yarp.Kubernetes.Controller.Certificates;
using Yarp.Kubernetes.Controller.Converters;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Yarp.Kubernetes.Tests;

public class IngressConversionTests
{
    public IngressConversionTests()
    {
        JsonConvert.DefaultSettings = () => new JsonSerializerSettings()
        {
            NullValueHandling = NullValueHandling.Ignore,
            Converters = { new StringEnumConverter() }
        };
    }

    [Theory]
    [InlineData("basic-ingress")]
    [InlineData("multiple-endpoints-ports")]
    [InlineData("multiple-endpoints-same-port")]
    [InlineData("https")]
    [InlineData("https-service-port-protocol")]
    [InlineData("exact-match")]
    [InlineData("annotations")]
    [InlineData("mapped-port")]
    [InlineData("port-mismatch")]
    [InlineData("hostname-routing")]
    [InlineData("multiple-hosts")]
    [InlineData("multiple-ingresses")]
    [InlineData("multiple-ingresses-one-svc")]
    [InlineData("multiple-namespaces")]
    [InlineData("route-metadata")]
    [InlineData("route-queryparameters")]
    [InlineData("route-headers")]
    [InlineData("route-order")]
    [InlineData("route-methods")]
    [InlineData("missing-svc")]
    [InlineData("port-diff-name")]
    [InlineData("external-name-ingress")]
    public async Task ParsingTests(string name)
    {
        var ingressClass = KubeResourceGenerator.CreateIngressClass("yarp", "microsoft.com/ingress-yarp", true);
        var cache = await GetKubernetesInfo(name, ingressClass);
        var configContext = new YarpConfigContext();
        var ingresses = cache.GetIngresses().ToArray();

        foreach (var ingress in ingresses)
        {
            if (cache.TryGetReconcileData(new NamespacedName(ingress.Metadata.NamespaceProperty, ingress.Metadata.Name), out var data))
            {
                var ingressContext = new YarpIngressContext(ingress, data.ServiceList, data.EndpointsList);
                YarpParser.ConvertFromKubernetesIngress(ingressContext, configContext);
            }
        }
        var options = new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } };
        VerifyClusters(JsonSerializer.Serialize(configContext.BuildClusterConfig(), options), name);
        VerifyRoutes(JsonSerializer.Serialize(configContext.Routes, options), name);
    }

    private static void VerifyClusters(string clusterJson, string name)
    {
        VerifyJson(clusterJson, name, "clusters.json");
    }

    private static void VerifyRoutes(string routesJson, string name)
    {
        VerifyJson(routesJson, name, "routes.json");
    }

    private static string StripNullProperties(string json)
    {
        using var reader = new JsonTextReader(new StringReader(json));
        var sb = new StringBuilder();
        using var sw = new StringWriter(sb);
        using var writer = new JsonTextWriter(sw);
        while (reader.Read())
        {
            var token = reader.TokenType;
            var value = reader.Value;
            if (reader.TokenType == JsonToken.PropertyName)
            {
                reader.Read();
                if (reader.TokenType == JsonToken.Null)
                {
                    continue;
                }
                writer.WriteToken(token, value);
            }
            writer.WriteToken(reader.TokenType, reader.Value);
        }

        return sb.ToString();
    }

    private static void VerifyJson(string json, string name, string fileName)
    {
        var other = File.ReadAllText(Path.Combine("testassets", name, fileName));
        json = StripNullProperties(json);
        other = StripNullProperties(other);

        var actual = JToken.Parse(json);
        var jOther = JToken.Parse(other);

        Assert.True(JToken.DeepEquals(actual, jOther), $"Expected: {jOther}\nActual: {actual}");
    }

    private async Task<ICache> GetKubernetesInfo(string name, V1IngressClass ingressClass)
    {
        var mockLogger = new Mock<ILogger<IngressCache>>();
        var mockOptions = new Mock<IOptions<YarpOptions>>();
        var certificateSelector = new Mock<IServerCertificateSelector>();
        var loggerHelper = new Mock<ILogger<CertificateHelper>>();
        var certificateHelper = new CertificateHelper(loggerHelper.Object);

        mockOptions.SetupGet(o => o.Value).Returns(new YarpOptions { ControllerClass = "microsoft.com/ingress-yarp" });

        var cache = new IngressCache(mockOptions.Object, certificateSelector.Object, certificateHelper, mockLogger.Object);

        var typeMap = new Dictionary<string, Type>();
        typeMap.Add("networking.k8s.io/v1/Ingress", typeof(V1Ingress));
        typeMap.Add("v1/Service", typeof(V1Service));
        typeMap.Add("v1/Endpoints", typeof(V1Endpoints));

        if (ingressClass is not null)
        {
            cache.Update(WatchEventType.Added, ingressClass);
        }

        var kubeObjects = await KubernetesYaml.LoadAllFromFileAsync(Path.Combine("testassets", name, "ingress.yaml"), typeMap).ConfigureAwait(false);
        foreach (var obj in kubeObjects)
        {
            if (obj is V1Ingress ingress)
            {
                cache.Update(WatchEventType.Added, ingress);
            }
            else if (obj is V1Service service)
            {
                cache.Update(WatchEventType.Added, service);
            }
            else if (obj is V1Endpoints endpoints)
            {
                cache.Update(WatchEventType.Added, endpoints);
            }
        }

        return cache;
    }
}
