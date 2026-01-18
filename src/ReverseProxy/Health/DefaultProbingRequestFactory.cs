// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Net.Http.Headers;
using Yarp.ReverseProxy.Model;

namespace Yarp.ReverseProxy.Health;

internal sealed class DefaultProbingRequestFactory : IProbingRequestFactory
{
    private static readonly string? _version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
    private static readonly string _defaultUserAgent = $"YARP{(string.IsNullOrEmpty(_version) ? "" : $"/{_version.Split('+')[0]}")} (Active Health Check Monitor)";

    public HttpRequestMessage CreateRequest(ClusterModel cluster, DestinationModel destination)
    {
        var probeAddress = !string.IsNullOrEmpty(destination.Config.Health) ? destination.Config.Health : destination.Config.Address;
        var probePath = cluster.Config.HealthCheck?.Active?.Path;
        UriHelper.FromAbsolute(probeAddress, out var destinationScheme, out var destinationHost, out var destinationPathBase, out _, out _);
        var query = QueryString.FromUriComponent(cluster.Config.HealthCheck?.Active?.Query ?? "");
        var probeUri = UriHelper.BuildAbsolute(destinationScheme, destinationHost, destinationPathBase, probePath, query);

        var request = new HttpRequestMessage(HttpMethod.Get, probeUri)
        {
            Version = cluster.Config.HttpRequest?.Version ?? HttpVersion.Version20,
            VersionPolicy = cluster.Config.HttpRequest?.VersionPolicy ?? HttpVersionPolicy.RequestVersionOrLower,
        };

        if (!string.IsNullOrEmpty(destination.Config.Host))
        {
            request.Headers.Add(HeaderNames.Host, destination.Config.Host);
        }

        request.Headers.Add(HeaderNames.UserAgent, _defaultUserAgent);

        return request;
    }

    public ValueTask<HttpRequestMessage> CreateRequestAsync(ClusterState cluster, DestinationState destination, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(CreateRequest(cluster.Model, destination.Model));
}
