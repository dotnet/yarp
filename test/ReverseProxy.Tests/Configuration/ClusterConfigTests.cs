// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;
using Yarp.ReverseProxy.LoadBalancing;
using Yarp.ReverseProxy.Forwarder;

namespace Yarp.ReverseProxy.Configuration.Tests;

public class ClusterConfigTests
{
    [Fact]
    public void Equals_Same_Value_Returns_True()
    {
        var config1 = new ClusterConfig
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
                        Metadata = new Dictionary<string, string> { { "destA-K1", "destA-V1" }, { "destA-K2", "destA-V2" } }
                    }
                },
                {
                    "destinationB",
                    new DestinationConfig
                    {
                        Address = "https://localhost:10000/destB",
                        Health = "https://localhost:20000/destB",
                        Metadata = new Dictionary<string, string> { { "destB-K1", "destB-V1" }, { "destB-K2", "destB-V2" } }
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
                    Path = "healthCheckPath"
                }
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
                    SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest
                }
            },
            HttpClient = new HttpClientConfig
            {
                SslProtocols = SslProtocols.Tls11 | SslProtocols.Tls12,
                MaxConnectionsPerServer = 10,
                DangerousAcceptAnyServerCertificate = true,
                RequestHeaderEncoding = Encoding.UTF8.WebName,
                ResponseHeaderEncoding = Encoding.UTF8.WebName
            },
            HttpRequest = new ForwarderRequestConfig
            {
                ActivityTimeout = TimeSpan.FromSeconds(60),
                Version = Version.Parse("1.0"),
                VersionPolicy = HttpVersionPolicy.RequestVersionExact,
            },
            Metadata = new Dictionary<string, string> { { "cluster1-K1", "cluster1-V1" }, { "cluster1-K2", "cluster1-V2" } }
        };

        var config2 = new ClusterConfig
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
                        Metadata = new Dictionary<string, string> { { "destA-K1", "destA-V1" }, { "destA-K2", "destA-V2" } }
                    }
                },
                {
                    "destinationB",
                    new DestinationConfig
                    {
                        Address = "https://localhost:10000/destB",
                        Health = "https://localhost:20000/destB",
                        Metadata = new Dictionary<string, string> { { "destB-K1", "destB-V1" }, { "destB-K2", "destB-V2" } }
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
                    Path = "healthCheckPath"
                }
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
                    SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest
                }
            },
            HttpClient = new HttpClientConfig
            {
                SslProtocols = SslProtocols.Tls11 | SslProtocols.Tls12,
                MaxConnectionsPerServer = 10,
                DangerousAcceptAnyServerCertificate = true,
                RequestHeaderEncoding = Encoding.UTF8.WebName,
                ResponseHeaderEncoding = Encoding.UTF8.WebName
            },
            HttpRequest = new ForwarderRequestConfig
            {
                ActivityTimeout = TimeSpan.FromSeconds(60),
                Version = Version.Parse("1.0"),
                VersionPolicy = HttpVersionPolicy.RequestVersionExact,
            },
            Metadata = new Dictionary<string, string> { { "cluster1-K1", "cluster1-V1" }, { "cluster1-K2", "cluster1-V2" } }
        };

        var config3 = config1 with { }; // Clone

        Assert.True(config1.Equals(config2));
        Assert.True(config1.Equals(config3));
        Assert.Equal(config1.GetHashCode(), config2.GetHashCode());
        Assert.Equal(config1.GetHashCode(), config3.GetHashCode());
    }

    [Fact]
    public void Equals_Different_Value_Returns_False()
    {
        var config1 = new ClusterConfig
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
                        Metadata = new Dictionary<string, string> { { "destA-K1", "destA-V1" }, { "destA-K2", "destA-V2" } }
                    }
                },
                {
                    "destinationB",
                    new DestinationConfig
                    {
                        Address = "https://localhost:10000/destB",
                        Health = "https://localhost:20000/destB",
                        Metadata = new Dictionary<string, string> { { "destB-K1", "destB-V1" }, { "destB-K2", "destB-V2" } }
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
                    Path = "healthCheckPath"
                }
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
                    SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest
                }
            },
            HttpClient = new HttpClientConfig
            {
                SslProtocols = SslProtocols.Tls11 | SslProtocols.Tls12,
                MaxConnectionsPerServer = 10,
                DangerousAcceptAnyServerCertificate = true,
            },
            HttpRequest = new ForwarderRequestConfig
            {
                ActivityTimeout = TimeSpan.FromSeconds(60),
                Version = Version.Parse("1.0"),
                VersionPolicy = HttpVersionPolicy.RequestVersionExact,
            },
            Metadata = new Dictionary<string, string> { { "cluster1-K1", "cluster1-V1" }, { "cluster1-K2", "cluster1-V2" } }
        };

        Assert.False(config1.Equals(config1 with { ClusterId = "different" }));
        Assert.False(config1.Equals(config1 with { Destinations = new Dictionary<string, DestinationConfig>() }));
        Assert.False(config1.Equals(config1 with { HealthCheck = new HealthCheckConfig() }));
        Assert.False(config1.Equals(config1 with { LoadBalancingPolicy = "different" }));
        Assert.False(config1.Equals(config1 with
        {
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
                    Path = "newpath",
                    SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict,
                    SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest
                }
            }
        }));
        Assert.False(config1.Equals(config1 with
        {
            HttpClient = new HttpClientConfig
            {
                SslProtocols = SslProtocols.Tls12,
                MaxConnectionsPerServer = 10,
                DangerousAcceptAnyServerCertificate = true,
            }
        }));
        Assert.False(config1.Equals(config1 with { HttpRequest = new ForwarderRequestConfig() { } }));
        Assert.False(config1.Equals(config1 with { Metadata = null }));
    }

    [Fact]
    public void Equals_Second_Null_Returns_False()
    {
        var config1 = new ClusterConfig();

        var equals = config1.Equals(null);

        Assert.False(equals);
    }

    [Fact]
    public void Cluster_CanBeJsonSerialized()
    {
        var cluster1 = new ClusterConfig
        {
            ClusterId = "cluster1",
            LoadBalancingPolicy = LoadBalancingPolicies.Random,
            SessionAffinity = new SessionAffinityConfig
            {
                Enabled = true,
                FailurePolicy = "Return503Error",
                Policy = "Cookie",
                AffinityKeyName = "Key1",
                Cookie = new SessionAffinityCookieConfig
                {
                    Domain = "domain",
                    Expiration = TimeSpan.FromDays(1),
                    HttpOnly = true,
                    IsEssential = true,
                    MaxAge = TimeSpan.FromHours(1),
                    Path = "/",
                    SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Unspecified,
                    SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.None
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
                    Path = "healthCheckPath"
                }
            },
            HttpClient = new HttpClientConfig
            {
                EnableMultipleHttp2Connections = true,
                SslProtocols = SslProtocols.Tls11 | SslProtocols.Tls12,
                MaxConnectionsPerServer = 10,
                DangerousAcceptAnyServerCertificate = true,
                WebProxy = new WebProxyConfig
                {
                    Address = new Uri("http://proxy"),
                    BypassOnLocal = false,
                    UseDefaultCredentials = false,
                }
            },
            HttpRequest = new ForwarderRequestConfig
            {
                ActivityTimeout = TimeSpan.FromSeconds(60),
                Version = Version.Parse("1.0"),
                VersionPolicy = HttpVersionPolicy.RequestVersionExact,
            },
            Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
            {
                {
                    "destinationA",
                    new DestinationConfig
                    {
                        Address = "https://localhost:10000/destA",
                        Health = "https://localhost:20000/destA",
                        Metadata = new Dictionary<string, string> { { "destA-K1", "destA-V1" }, { "destA-K2", "destA-V2" } }
                    }
                },
                {
                    "destinationB",
                    new DestinationConfig
                    {
                        Address = "https://localhost:10000/destB",
                        Health = "https://localhost:20000/destB",
                        Metadata = new Dictionary<string, string> { { "destB-K1", "destB-V1" }, { "destB-K2", "destB-V2" } }
                    }
                }
            },
            Metadata = new Dictionary<string, string> { { "cluster1-K1", "cluster1-V1" }, { "cluster1-K2", "cluster1-V2" } }
        };

        var json = JsonSerializer.Serialize(cluster1);
        var cluster2 = JsonSerializer.Deserialize<ClusterConfig>(json);

        Assert.Equal(cluster1.Destinations, cluster2.Destinations);
        Assert.Equal(cluster1.HealthCheck.Active, cluster2.HealthCheck.Active);
        Assert.Equal(cluster1.HealthCheck.Passive, cluster2.HealthCheck.Passive);
        Assert.Equal(cluster1.HealthCheck, cluster2.HealthCheck);
        Assert.Equal(cluster1.HttpClient, cluster2.HttpClient);
        Assert.Equal(cluster1.HttpRequest, cluster2.HttpRequest);
        Assert.Equal(cluster1.Metadata, cluster2.Metadata);
        Assert.Equal(cluster1.SessionAffinity, cluster2.SessionAffinity);
        Assert.Equal(cluster1, cluster2);
    }

    public class TimeSpanConverter : JsonConverter<TimeSpan>
    {
        public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return TimeSpan.Parse(reader.GetString(), CultureInfo.InvariantCulture);
        }

        public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString(format: null, CultureInfo.InvariantCulture));
        }
    }

    public class VersionConverter : JsonConverter<Version>
    {
        public override Version Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var versionString = reader.GetString();
            return Version.Parse(versionString);
        }

        public override void Write(Utf8JsonWriter writer, Version value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}
