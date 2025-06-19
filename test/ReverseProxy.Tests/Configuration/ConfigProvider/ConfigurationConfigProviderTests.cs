// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Json.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.LoadBalancing;

namespace Yarp.ReverseProxy.Configuration.ConfigProvider.Tests;

public class ConfigurationConfigProviderTests
{
    private static readonly JsonSchema s_yarpSchema = JsonSchema.FromText(File.ReadAllText("ConfigurationSchema.json"));
    private static readonly EvaluationOptions s_schemaOptions = new()
    {
        OutputFormat = OutputFormat.List,
        RequireFormatValidation = true
    };

    #region JSON test configuration

    private readonly ConfigurationSnapshot _validConfigurationData = new ConfigurationSnapshot()
    {
        Clusters =
        {
            {
                new ClusterConfig
                {
                    ClusterId = "cluster1",
                    Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
                    {
                        {
                            "destinationA",
                            new DestinationConfig
                            {
                                Address = "https://localhost:10000/destA",
                                Health = "https://localhost:20000/destA",
                                Metadata = new Dictionary<string, string> { { "destA-K1", "destA-V1" }, { "destA-K2", "destA-V2" } },
                                Host = "localhost"
                            }
                        },
                        {
                            "destinationB",
                            new DestinationConfig
                            {
                                Address = "https://localhost:10000/destB",
                                Health = "https://localhost:20000/destB",
                                Metadata = new Dictionary<string, string> { { "destB-K1", "destB-V1" }, { "destB-K2", "destB-V2" }, { "foo", "42" }, { "bar", "True" } },
                                Host = "localhost"
                            }
                        }
                    },
                    HealthCheck = new HealthCheckConfig
                    {
                        Passive = new PassiveHealthCheckConfig
                        {
                            Enabled = true,
                            Policy = "FailureRate",
                            ReactivationPeriod = TimeSpan.FromMinutes(5)
                        },
                        Active = new ActiveHealthCheckConfig
                        {
                            Enabled = true,
                            Interval = TimeSpan.FromSeconds(4),
                            Timeout = TimeSpan.FromSeconds(6),
                            Policy = "Any5xxResponse",
                            Path = "healthCheckPath",
                            Query = "?key=value"
                        },
                        AvailableDestinationsPolicy = "HealthyOrPanic"
                    },
                    LoadBalancingPolicy = LoadBalancingPolicies.Random,
                    SessionAffinity = new SessionAffinityConfig
                    {
                        Enabled = true,
                        FailurePolicy = "Return503Error",
                        Policy = "Cookie",
                        AffinityKeyName = "Key1",
                        Cookie = new SessionAffinityCookieConfig
                        {
                            Domain = "localhost",
                            Expiration = TimeSpan.FromHours(3),
                            HttpOnly = true,
                            IsEssential = true,
                            MaxAge = TimeSpan.FromDays(1),
                            Path = "mypath",
                            SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict,
                            SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.None
                        }
                    },
                    HttpClient = new HttpClientConfig
                    {
                        SslProtocols = SslProtocols.Tls11 | SslProtocols.Tls12,
                        MaxConnectionsPerServer = 10,
                        DangerousAcceptAnyServerCertificate = true,
                        EnableMultipleHttp2Connections = true,
                    },
                    HttpRequest = new ForwarderRequestConfig()
                    {
                        ActivityTimeout = TimeSpan.FromSeconds(60),
                        Version = Version.Parse("1.0"),
                        VersionPolicy = HttpVersionPolicy.RequestVersionExact,
                        AllowResponseBuffering = true
                    },
                    Metadata = new Dictionary<string, string> { { "cluster1-K1", "cluster1-V1" }, { "cluster1-K2", "cluster1-V2" } }
                }
            },
            {
                new ClusterConfig
                {
                    ClusterId = "cluster2",
                    Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "destinationC", new DestinationConfig { Address = "https://localhost:10001/destC", Host = "localhost" } },
                        { "destinationD", new DestinationConfig { Address = "https://localhost:10000/destB", Host = "remotehost" } }
                    },
                    LoadBalancingPolicy = LoadBalancingPolicies.RoundRobin
                }
            }
        },
        Routes =
        {
            new RouteConfig
            {
                RouteId = "routeA",
                ClusterId = "cluster1",
                AuthorizationPolicy = "Default",
                RateLimiterPolicy = "Default",
                TimeoutPolicy = "Default",
                Timeout = TimeSpan.Zero,
                CorsPolicy = "Default",
                Order = -1,
                MaxRequestBodySize = -1,
                Match = new RouteMatch
                {
                    Hosts = new List<string> { "host-A" },
                    Methods = new List<string> { "GET", "POST", "DELETE" },
                    Path = "/apis/entities",
                    Headers = new[]
                    {
                        new RouteHeader
                        {
                            Name = "header1",
                            Values = new[] { "value1" },
                            IsCaseSensitive = true,
                            Mode = HeaderMatchMode.HeaderPrefix
                        }
                    },
                    QueryParameters = new[]
                    {
                        new RouteQueryParameter
                        {
                            Name = "queryparam1",
                            Values = new[] { "value1" },
                            IsCaseSensitive = true,
                            Mode = QueryParameterMatchMode.Contains
                        }
                    }
                },
                Transforms = new[]
                {
                    new Dictionary<string, string>
                    {
                        { "RequestHeadersCopy", "true" }
                    },
                    new Dictionary<string, string>
                    {
                        { "PathRemovePrefix", "/apis" },
                    },
                    new Dictionary<string, string>
                    {
                        { "PathPrefix", "/apis" }
                    },
                    new Dictionary<string, string>
                    {
                        { "RequestHeader", "header1" },
                        { "Append", "foo" }
                    }
                },
                Metadata = new Dictionary<string, string> { { "routeA-K1", "routeA-V1" }, { "routeA-K2", "routeA-V2" } }
            },
            new RouteConfig
            {
                RouteId = "routeB",
                ClusterId = "cluster2",
                Order = 2,
                MaxRequestBodySize = 1,
                Match = new RouteMatch
                {
                    Hosts = new List<string> { "host-B" },
                    Methods = new List<string> { "GET" },
                    Path = "/apis/users",
                    Headers = new[]
                    {
                        new RouteHeader
                        {
                            Name = "header2",
                            Values = new[] { "value2" },
                            IsCaseSensitive = false,
                            Mode = HeaderMatchMode.ExactHeader
                        }
                    },
                    QueryParameters = new[]
                    {
                        new RouteQueryParameter
                        {
                            Name = "queryparam2",
                            Values = new[] { "value2" },
                            IsCaseSensitive = true,
                            Mode = QueryParameterMatchMode.Contains
                        }
                    }
                }
            }
        }
    };

    private const string ValidJsonConfig =
        """
        {
            "Clusters": {
                "cluster1": {
                    "LoadBalancingPolicy": "Random",
                    "SessionAffinity": {
                        "Enabled": true,
                        "Policy": "Cookie",
                        "FailurePolicy": "Return503Error",
                        "AffinityKeyName": "Key1",
                        "Cookie": {
                            "Domain": "localhost",
                            "Expiration": "03:0:00",
                            "HttpOnly": true,
                            "IsEssential": "True",
                            "MaxAge": "1.00:00:0",
                            "Path": "mypath",
                            "SameSite": "Strict",
                            "SecurePolicy": "None"
                        }
                    },
                    "HealthCheck": {
                        "Passive": {
                            "Enabled": true,
                            "Policy": "FailureRate",
                            "ReactivationPeriod": "0:05:00"
                        },
                        "Active": {
                            "Enabled": true,
                            "Interval": "00:00:04",
                            "Timeout": "00:00:06",
                            "Policy": "Any5xxResponse",
                            "Path": "healthCheckPath",
                            "Query": "?key=value"
                        },
                        "AvailableDestinationsPolicy": "HealthyOrPanic"
                    },
                    "HttpClient": {
                        "SslProtocols": [
                            "Tls11",
                            "Tls12"
                        ],
                        "DangerousAcceptAnyServerCertificate": true,
                        "MaxConnectionsPerServer": 10,
                        "EnableMultipleHttp2Connections": true,
                        "RequestHeaderEncoding": "utf-8",
                        "ResponseHeaderEncoding": "utf-8",
                        "WebProxy": {
                            "Address": "http://localhost:8080",
                            "BypassOnLocal": true,
                            "UseDefaultCredentials": true
                        }
                    },
                    "HttpRequest": {
                        "ActivityTimeout": "00:01:00",
                        "Version": "1",
                        "VersionPolicy": "RequestVersionExact",
                        "AllowResponseBuffering": true
                    },
                    "Destinations": {
                        "destinationA": {
                            "Address": "https://localhost:10000/destA",
                            "Health": "https://localhost:20000/destA",
                            "Host": "localhost",
                            "Metadata": {
                                "destA-K1": "destA-V1",
                                "destA-K2": "destA-V2"
                            }
                        },
                        "destinationB": {
                            "Address": "https://localhost:10000/destB",
                            "Health": "https://localhost:20000/destB",
                            "Host": "localhost",
                            "Metadata": {
                                "destB-K1": "destB-V1",
                                "destB-K2": "destB-V2",
                                "foo": 42,
                                "bar": true
                            }
                        }
                    },
                    "Metadata": {
                        "cluster1-K1": "cluster1-V1",
                        "cluster1-K2": "cluster1-V2"
                    }
                },
                "cluster2": {
                    "LoadBalancingPolicy": "RoundRobin",
                    "SessionAffinity": null,
                    "HealthCheck": null,
                    "HttpClient": null,
                    "Destinations": {
                        "destinationC": {
                            "Address": "https://localhost:10001/destC",
                            "Host": "localhost",
                            "Metadata": null
                        },
                        "destinationD": {
                            "Address": "https://localhost:10000/destB",
                            "Host": "remotehost",
                            "Metadata": null
                        }
                    },
                    "Metadata": null
                }
            },
            "Routes": {
                "routeA" : {
                    "Match": {
                        "Methods": [
                            "GET",
                            "POST",
                            "DELETE"
                        ],
                        "Hosts": [
                            "host-A"
                        ],
                        "Path": "/apis/entities",
                        "Headers": [
                          {
                            "Name": "header1",
                            "Values": [ "value1" ],
                            "IsCaseSensitive": true,
                            "Mode": "HeaderPrefix"
                          }
                        ],
                        "QueryParameters": [
                          {
                            "Name": "queryparam1",
                            "Values": [ "value1" ],
                            "IsCaseSensitive": "true",
                            "Mode": "Contains"
                          }
                        ]
                    },
                    "Order": -1,
                    "MaxRequestBodySize": -1,
                    "ClusterId": "cluster1",
                    "AuthorizationPolicy": "Default",
                    "RateLimiterPolicy": "Default",
                    "OutputCachePolicy": "Default",
                    "CorsPolicy": "Default",
                    "TimeoutPolicy": "Default",
                    "Timeout": "00:00:01",
                    "Metadata": {
                        "routeA-K1": "routeA-V1",
                        "routeA-K2": "routeA-V2"
                    },
                    "Transforms": [
                        {
                            "RequestHeadersCopy": true
                        },
                        {
                            "PathRemovePrefix": "/apis"
                        },
                        {
                            "PathPrefix": "/apis"
                        },
                        {
                            "RequestHeader": "header1",
                            "Append": "foo"
                        }
                    ]
                },
                "routeB" : {
                    "Match": {
                        "Methods": [
                            "GET"
                        ],
                        "Hosts": [
                            "host-B"
                        ],
                        "Path": "/apis/users",
                        "Headers": [
                          {
                            "Name": "header2",
                            "Values": [ "value2" ]
                          }
                        ],
                        "QueryParameters": [
                          {
                            "Name": "queryparam2",
                            "Values": [ "value2" ],
                            "IsCaseSensitive": true,
                            "Mode": "Contains"
                          }
                        ]
                    },
                    "Order": 2,
                    "MaxRequestBodySize": 1,
                    "ClusterId": "cluster2",
                    "AuthorizationPolicy": null,
                    "RateLimiterPolicy": null,
                    "OutputCachePolicy": null,
                    "CorsPolicy": null,
                    "Metadata": null,
                    "Transforms": null
                }
            }
        }
        """;

    #endregion

    private static ConfigurationConfigProvider CreateProvider(string json)
    {
        var builder = new ConfigurationBuilder();
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var configuration = builder.AddJsonStream(stream).Build();
        var logger = new Mock<ILogger<ConfigurationConfigProvider>>();

        return new ConfigurationConfigProvider(logger.Object, configuration);
    }

    [Fact]
    public void GetConfig_ValidSerializedConfiguration_ConvertToAbstractionsSuccessfully()
    {
        var provider = CreateProvider(ValidJsonConfig);
        var abstractConfig = provider.GetConfig();

        VerifyValidAbstractConfig(_validConfigurationData, abstractConfig);
    }

    [Fact]
    public void GetConfig_ValidConfiguration_AllAbstractionsPropertiesAreSet()
    {
        var provider = CreateProvider(ValidJsonConfig);
        var abstractConfig = (ConfigurationSnapshot)provider.GetConfig();

        var abstractionsNamespace = typeof(ClusterConfig).Namespace;
        // Removed incompletely filled out instances.
        abstractConfig.Clusters = abstractConfig.Clusters.Where(c => c.ClusterId == "cluster1").ToList();
        abstractConfig.Routes = abstractConfig.Routes.Where(r => r.RouteId == "routeA").ToList();

        VerifyAllPropertiesAreSet(abstractConfig);

        void VerifyFullyInitialized(object obj, string name)
        {
            switch (obj)
            {
                case null:
                    Assert.Fail($"Property {name} is not initialized.");
                    break;
                case Enum m:
                    Assert.NotEqual(0, (int)(object)m);
                    break;
                case string str:
                    Assert.NotEmpty(str);
                    break;
                case ValueType v:
                    var equals = Equals(Activator.CreateInstance(v.GetType()), v);
                    Assert.False(equals, $"Property {name} is not initialized.");
                    if (v.GetType().Namespace == abstractionsNamespace)
                    {
                        VerifyAllPropertiesAreSet(v);
                    }
                    break;
                case IDictionary d:
                    Assert.NotEmpty(d);
                    foreach (var value in d.Values)
                    {
                        VerifyFullyInitialized(value, name);
                    }
                    break;
                case IEnumerable e:
                    Assert.NotEmpty(e);
                    foreach (var item in e)
                    {
                        VerifyFullyInitialized(item, name);
                    }

                    var type = e.GetType();
                    if (!type.IsArray && type.Namespace == abstractionsNamespace)
                    {
                        VerifyAllPropertiesAreSet(e);
                    }
                    break;
                case object o:
                    if (o.GetType().Namespace == abstractionsNamespace)
                    {
                        VerifyAllPropertiesAreSet(o);
                    }
                    break;
            }
        }

        void VerifyAllPropertiesAreSet(object obj)
        {
            var properties = obj.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public).Cast<PropertyInfo>();
            foreach (var property in properties)
            {
                VerifyFullyInitialized(property.GetValue(obj), $"{property.DeclaringType.Name}.{property.Name}");
            }
        }
    }

    private void VerifyValidAbstractConfig(IProxyConfig validConfig, IProxyConfig abstractConfig)
    {
        Assert.NotNull(abstractConfig);
        Assert.Equal(2, abstractConfig.Clusters.Count);

        var cluster1 = validConfig.Clusters.First(c => c.ClusterId == "cluster1");
        Assert.Single(abstractConfig.Clusters, c => c.ClusterId == "cluster1");
        var abstractCluster1 = abstractConfig.Clusters.Single(c => c.ClusterId == "cluster1");
        Assert.Equal(cluster1.Destinations["destinationA"].Address, abstractCluster1.Destinations["destinationA"].Address);
        Assert.Equal(cluster1.Destinations["destinationA"].Health, abstractCluster1.Destinations["destinationA"].Health);
        Assert.Equal(cluster1.Destinations["destinationA"].Metadata, abstractCluster1.Destinations["destinationA"].Metadata);
        Assert.Equal(cluster1.Destinations["destinationA"].Host, abstractCluster1.Destinations["destinationA"].Host);
        Assert.Equal(cluster1.Destinations["destinationB"].Address, abstractCluster1.Destinations["destinationB"].Address);
        Assert.Equal(cluster1.Destinations["destinationB"].Health, abstractCluster1.Destinations["destinationB"].Health);
        Assert.Equal(cluster1.Destinations["destinationB"].Metadata, abstractCluster1.Destinations["destinationB"].Metadata);
        Assert.Equal(cluster1.Destinations["destinationB"].Host, abstractCluster1.Destinations["destinationB"].Host);
        Assert.Equal(cluster1.HealthCheck.AvailableDestinationsPolicy, abstractCluster1.HealthCheck.AvailableDestinationsPolicy);
        Assert.Equal(cluster1.HealthCheck.Passive.Enabled, abstractCluster1.HealthCheck.Passive.Enabled);
        Assert.Equal(cluster1.HealthCheck.Passive.Policy, abstractCluster1.HealthCheck.Passive.Policy);
        Assert.Equal(cluster1.HealthCheck.Passive.ReactivationPeriod, abstractCluster1.HealthCheck.Passive.ReactivationPeriod);
        Assert.Equal(cluster1.HealthCheck.Active.Enabled, abstractCluster1.HealthCheck.Active.Enabled);
        Assert.Equal(cluster1.HealthCheck.Active.Interval, abstractCluster1.HealthCheck.Active.Interval);
        Assert.Equal(cluster1.HealthCheck.Active.Timeout, abstractCluster1.HealthCheck.Active.Timeout);
        Assert.Equal(cluster1.HealthCheck.Active.Policy, abstractCluster1.HealthCheck.Active.Policy);
        Assert.Equal(cluster1.HealthCheck.Active.Path, abstractCluster1.HealthCheck.Active.Path);
        Assert.Equal(cluster1.HealthCheck.Active.Query, abstractCluster1.HealthCheck.Active.Query);
        Assert.Equal(LoadBalancingPolicies.Random, abstractCluster1.LoadBalancingPolicy);
        Assert.Equal(cluster1.SessionAffinity.Enabled, abstractCluster1.SessionAffinity.Enabled);
        Assert.Equal(cluster1.SessionAffinity.FailurePolicy, abstractCluster1.SessionAffinity.FailurePolicy);
        Assert.Equal(cluster1.SessionAffinity.Policy, abstractCluster1.SessionAffinity.Policy);
        Assert.Equal(cluster1.SessionAffinity.AffinityKeyName, abstractCluster1.SessionAffinity.AffinityKeyName);
        Assert.Equal(cluster1.SessionAffinity.Cookie.Domain, abstractCluster1.SessionAffinity.Cookie.Domain);
        Assert.Equal(cluster1.SessionAffinity.Cookie.Expiration, abstractCluster1.SessionAffinity.Cookie.Expiration);
        Assert.Equal(cluster1.SessionAffinity.Cookie.HttpOnly, abstractCluster1.SessionAffinity.Cookie.HttpOnly);
        Assert.Equal(cluster1.SessionAffinity.Cookie.IsEssential, abstractCluster1.SessionAffinity.Cookie.IsEssential);
        Assert.Equal(cluster1.SessionAffinity.Cookie.MaxAge, abstractCluster1.SessionAffinity.Cookie.MaxAge);
        Assert.Equal(cluster1.SessionAffinity.Cookie.Path, abstractCluster1.SessionAffinity.Cookie.Path);
        Assert.Equal(cluster1.SessionAffinity.Cookie.SameSite, abstractCluster1.SessionAffinity.Cookie.SameSite);
        Assert.Equal(cluster1.SessionAffinity.Cookie.SecurePolicy, abstractCluster1.SessionAffinity.Cookie.SecurePolicy);
        Assert.Equal(cluster1.HttpClient.MaxConnectionsPerServer, abstractCluster1.HttpClient.MaxConnectionsPerServer);
        Assert.Equal(cluster1.HttpClient.EnableMultipleHttp2Connections, abstractCluster1.HttpClient.EnableMultipleHttp2Connections);
        Assert.Equal(Encoding.UTF8.WebName, abstractCluster1.HttpClient.RequestHeaderEncoding);
        Assert.Equal(Encoding.UTF8.WebName, abstractCluster1.HttpClient.ResponseHeaderEncoding);
        Assert.Equal(SslProtocols.Tls11 | SslProtocols.Tls12, abstractCluster1.HttpClient.SslProtocols);
        Assert.Equal(cluster1.HttpRequest.ActivityTimeout, abstractCluster1.HttpRequest.ActivityTimeout);
        Assert.Equal(HttpVersion.Version10, abstractCluster1.HttpRequest.Version);
        Assert.Equal(cluster1.HttpRequest.VersionPolicy, abstractCluster1.HttpRequest.VersionPolicy);
        Assert.Equal(cluster1.HttpRequest.AllowResponseBuffering, abstractCluster1.HttpRequest.AllowResponseBuffering);
        Assert.Equal(cluster1.HttpClient.DangerousAcceptAnyServerCertificate, abstractCluster1.HttpClient.DangerousAcceptAnyServerCertificate);
        Assert.Equal(cluster1.Metadata, abstractCluster1.Metadata);

        var cluster2 = validConfig.Clusters.First(c => c.ClusterId == "cluster2");
        Assert.Single(abstractConfig.Clusters, c => c.ClusterId == "cluster2");
        var abstractCluster2 = abstractConfig.Clusters.Single(c => c.ClusterId == "cluster2");
        Assert.Equal(cluster2.Destinations["destinationC"].Address, abstractCluster2.Destinations["destinationC"].Address);
        Assert.Equal(cluster2.Destinations["destinationC"].Metadata, abstractCluster2.Destinations["destinationC"].Metadata);
        Assert.Equal(cluster2.Destinations["destinationC"].Host, abstractCluster2.Destinations["destinationC"].Host);
        Assert.Equal(cluster2.Destinations["destinationD"].Address, abstractCluster2.Destinations["destinationD"].Address);
        Assert.Equal(cluster2.Destinations["destinationD"].Metadata, abstractCluster2.Destinations["destinationD"].Metadata);
        Assert.Equal(cluster2.Destinations["destinationD"].Host, abstractCluster2.Destinations["destinationD"].Host);
        Assert.Equal(LoadBalancingPolicies.RoundRobin, abstractCluster2.LoadBalancingPolicy);

        Assert.Equal(2, abstractConfig.Routes.Count);

        VerifyRoute(validConfig, abstractConfig, "routeA");
        VerifyRoute(validConfig, abstractConfig, "routeB");
    }

    private void VerifyRoute(IProxyConfig validConfig, IProxyConfig abstractConfig, string routeId)
    {
        var route = validConfig.Routes.Single(c => c.RouteId == routeId);
        Assert.Single(abstractConfig.Routes, r => r.RouteId == routeId);
        var abstractRoute = abstractConfig.Routes.Single(c => c.RouteId == routeId);
        Assert.Equal(route.ClusterId, abstractRoute.ClusterId);
        Assert.Equal(route.Order, abstractRoute.Order);
        Assert.Equal(route.MaxRequestBodySize, abstractRoute.MaxRequestBodySize);
        Assert.Equal(route.Match.Hosts, abstractRoute.Match.Hosts);
        Assert.Equal(route.Match.Methods, abstractRoute.Match.Methods);
        Assert.Equal(route.Match.Path, abstractRoute.Match.Path);
        var header = route.Match.Headers.Single();
        var expectedHeader = abstractRoute.Match.Headers.Single();
        Assert.Equal(header.Name, expectedHeader.Name);
        Assert.Equal(header.Mode, expectedHeader.Mode);
        Assert.Equal(header.IsCaseSensitive, expectedHeader.IsCaseSensitive);

        var queryparam = route.Match.QueryParameters.Single();
        var expectedQueryParam = abstractRoute.Match.QueryParameters.Single();
        Assert.Equal(queryparam.Name, expectedQueryParam.Name);
        Assert.Equal(queryparam.Mode, expectedQueryParam.Mode);
        Assert.Equal(queryparam.IsCaseSensitive, expectedQueryParam.IsCaseSensitive);

        if (route.Transforms is null)
        {
            Assert.Null(abstractRoute.Transforms);
        }
        else
        {
            Assert.NotNull(abstractRoute.Transforms);
            Assert.Equal(route.Transforms.Count, abstractRoute.Transforms.Count);

            for (var i = 0; i < route.Transforms.Count; i++)
            {
                var transform = route.Transforms[i];
                var expectedTransform = abstractRoute.Transforms[i];

                Assert.Equal(transform.Count, expectedTransform.Count);
                foreach (var kv in transform)
                {
                    Assert.True(expectedTransform.TryGetValue(kv.Key, out var value));

                    if (value == "True")
                    {
                        value = "true";
                    }

                    Assert.Equal(kv.Value, value);
                }
            }
        }
    }

    [Fact]
    public void ValidateSchema_ValidInput()
    {
        var results = s_yarpSchema.Evaluate(JsonNode.Parse(
            $$"""
            {
              "ReverseProxy": {{ValidJsonConfig}}
            }
            """),
            s_schemaOptions);

        var errors = results.Details
            .Where(d => d.HasErrors)
            .SelectMany(d => d.Errors!.Select(error => $"Path:${d.InstanceLocation} {error.Key}:{error.Value}"))
            .ToArray();

        Assert.True(results.IsValid);
        Assert.False(results.HasErrors);
        Assert.True(results.Details.Count > 300);
    }

    [Fact]
    public async Task ValidateSchema_Samples()
    {
        string repoRoot = Path.Combine(Environment.CurrentDirectory, "../../../../../");
        repoRoot = Path.GetFullPath(repoRoot);

        foreach (string file in Directory.EnumerateFiles(repoRoot, "*.json", SearchOption.AllDirectories))
        {
            if (file.Contains("\\obj\\", StringComparison.Ordinal) || file.Contains("/obj/", StringComparison.Ordinal))
            {
                continue;
            }

            if (file.Contains("appsettings", StringComparison.OrdinalIgnoreCase))
            {
                var contents = await File.ReadAllTextAsync(file);
                var document = JsonDocument.Parse(contents, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
                var results = s_yarpSchema.Evaluate(document.RootElement, s_schemaOptions);

                if (results.IsValid)
                {
                    Assert.False(results.HasErrors);

                    if (contents.Contains("\"ReverseProxy\"", StringComparison.OrdinalIgnoreCase))
                    {
                        Assert.True(results.Details.Count > 5, $"No details for '{file}'");
                    }
                }
                else
                {
                    Assert.Fail($"Json errors reported for '{file}'");
                }
            }
        }
    }
}
