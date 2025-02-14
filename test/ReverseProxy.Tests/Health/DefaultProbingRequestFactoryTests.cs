// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net;
using System.Net.Http;
using Xunit;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.Forwarder;

namespace Yarp.ReverseProxy.Health.Tests;

public class DefaultProbingRequestFactoryTests
{
    [Theory]
    [InlineData("https://localhost:10000/", null, null, null, "https://localhost:10000/")]
    [InlineData("https://localhost:10000/", "https://localhost:20000/", null, null, "https://localhost:20000/")]
    [InlineData("https://localhost:10000/", null, "/api/health/", null, "https://localhost:10000/api/health/")]
    [InlineData("https://localhost:10000/", "https://localhost:20000/", "/api/health/", null, "https://localhost:20000/api/health/")]
    [InlineData("https://localhost:10000/api", "https://localhost:20000/", "/health/", null, "https://localhost:20000/health/")]
    [InlineData("https://localhost:10000/", "https://localhost:20000/api", "/health/", null, "https://localhost:20000/api/health/")]
    [InlineData("https://localhost:10000/", null, null, "?key=value", "https://localhost:10000/?key=value")]
    [InlineData("https://localhost:10000/", "https://localhost:20000/", null, "?key=value", "https://localhost:20000/?key=value")]
    [InlineData("https://localhost:10000/", null, "/api/health/", "?key=value", "https://localhost:10000/api/health/?key=value")]
    [InlineData("https://localhost:10000/", "https://localhost:20000/", "/api/health/", "?key=value", "https://localhost:20000/api/health/?key=value")]
    [InlineData("https://localhost:10000/api", "https://localhost:20000/", "/health/", "?key=value", "https://localhost:20000/health/?key=value")]
    [InlineData("https://localhost:10000/", "https://localhost:20000/api", "/health/", "?key=value", "https://localhost:20000/api/health/?key=value")]
    [InlineData("https://localhost:10000/", "https://localhost:20000/api", "/health?foo=bar", "?key=value", "https://localhost:20000/api/health%3Ffoo=bar?key=value")]
    public void CreateRequest_HealthEndpointIsNotDefined_UseDestinationAddress(string address, string health, string healthPath, string query, string expectedRequestUri)
    {
        var clusterModel = GetClusterConfig("cluster0",
            new ActiveHealthCheckConfig()
            {
                Enabled = true,
                Policy = "policy",
                Path = healthPath,
                Query = query,
            }, HttpVersion.Version20);
        var destinationModel = new DestinationModel(new DestinationConfig { Address = address, Health = health });
        var factory = new DefaultProbingRequestFactory();

        var request = factory.CreateRequest(clusterModel, destinationModel);

        Assert.Equal(expectedRequestUri, request.RequestUri.AbsoluteUri);
    }

    [Theory]
    [InlineData("1.0")]
    [InlineData(null)]
    public void CreateRequest_RequestVersionProperties(string versionString)
    {
        var version = versionString is not null ? Version.Parse(versionString) : null;
        var clusterModel = GetClusterConfig("cluster0",
            new ActiveHealthCheckConfig()
            {
                Enabled = true,
                Policy = "policy",
            }, version, HttpVersionPolicy.RequestVersionExact);
        var destinationModel = new DestinationModel(new DestinationConfig { Address = "https://localhost:10000/" });
        var factory = new DefaultProbingRequestFactory();

        var request = factory.CreateRequest(clusterModel, destinationModel);

        Assert.Equal(version ?? HttpVersion.Version20, request.Version);
        Assert.Equal(HttpVersionPolicy.RequestVersionExact, request.VersionPolicy);
    }

    private ClusterModel GetClusterConfig(string id, ActiveHealthCheckConfig healthCheckOptions, Version version, HttpVersionPolicy versionPolicy = HttpVersionPolicy.RequestVersionExact)
    {
        return new ClusterModel(
            new ClusterConfig
            {
                ClusterId = id,
                HealthCheck = new HealthCheckConfig()
                {
                    Active = healthCheckOptions,
                },
                HttpRequest = new ForwarderRequestConfig
                {
                    ActivityTimeout = TimeSpan.FromSeconds(60),
                    Version = version,
                    VersionPolicy = versionPolicy,
                }
            },
            new HttpMessageInvoker(new HttpClientHandler()));
    }
}
