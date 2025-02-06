// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Extensions.DependencyInjection;
using Yarp.Kubernetes.Controller.Certificates;

namespace Microsoft.AspNetCore.Hosting;

/// <summary>
/// Extensions for <see cref="IWebHostBuilder"/>
/// used to register the Kubernetes-based ReverseProxy's components.
/// </summary>
public static class KubernetesReverseProxyWebHostBuilderExtensions
{
    /// <summary>
    /// Configures Kestrel for SNI-based certificate selection using Kubernetes Ingress TLS annotations and Kubernetes Secrets.
    /// </summary>
    /// <param name="builder">The web host builder.</param>
    /// <returns>The same <see cref="IWebHostBuilder"/> for chaining.</returns>
    public static IWebHostBuilder UseKubernetesReverseProxyCertificateSelector(this IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));

        builder.ConfigureKestrel(kestrelOptions =>
        {
            kestrelOptions.ConfigureHttpsDefaults(httpsOptions =>
            {
                var selector = kestrelOptions.ApplicationServices.GetService<IServerCertificateSelector>();
                if (selector is null)
                {
                    throw new InvalidOperationException("Missing required services. Did you call '.AddKubernetesReverseProxy()' when configuring services?");
                }

                httpsOptions.ServerCertificateSelector = (connectionContext, domainName) =>
                {
                    return selector.GetCertificate(connectionContext, domainName);
                };
            });
        });

        return builder;
    }
}
