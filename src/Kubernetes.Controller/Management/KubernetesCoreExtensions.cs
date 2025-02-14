// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using k8s;
using Microsoft.Extensions.Options;
using System.Linq;
using Yarp.Kubernetes.Controller.Client;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Class KubernetesCoreExtensions.
/// </summary>
public static class KubernetesCoreExtensions
{
    /// <summary>
    /// Adds the kubernetes.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>IServiceCollection.</returns>
    public static IServiceCollection AddKubernetesCore(this IServiceCollection services)
    {
        if (!services.Any(serviceDescriptor => serviceDescriptor.ServiceType == typeof(IKubernetes)))
        {
            services = services.Configure<KubernetesClientOptions>(options =>
            {
                options.Configuration ??= KubernetesClientConfiguration.BuildDefaultConfig();
            });

            services = services.AddSingleton<IKubernetes>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<KubernetesClientOptions>>().Value;

                return new Kubernetes(options.Configuration);
            });
        }

        return services;
    }
}
