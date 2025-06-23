// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Yarp.ReverseProxy.Configuration;

/// <summary>
/// Configuration for <see cref="OutputCacheOptions"/>
/// </summary>
public sealed record OutputCacheConfig
{
    /// <inheritdoc cref="OutputCacheOptions.SizeLimit"/>
    public long SizeLimit { get; set; } = 100 * 1024 * 1024;

    /// <inheritdoc cref="OutputCacheOptions.MaximumBodySize"/>
    public long MaximumBodySize { get; set; } = 64 * 1024 * 1024;

    /// <inheritdoc cref="OutputCacheOptions.DefaultExpirationTimeSpan"/>
    public TimeSpan DefaultExpirationTimeSpan { get; set; } = TimeSpan.FromSeconds(60);

    /// <inheritdoc cref="OutputCacheOptions.UseCaseSensitivePaths"/>
    public bool UseCaseSensitivePaths { get; set; }

    /// <summary>
    /// Policies that will be added with <see cref="OutputCacheOptions.AddBasePolicy(Action{OutputCachePolicyBuilder}, bool)"/>
    /// </summary>
    public IDictionary<string, NamedCacheConfig> NamedPolicies { get; set; } = new Dictionary<string, NamedCacheConfig>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Configuration for <see cref="OutputCachePolicyBuilder"/>
/// </summary>
public sealed record NamedCacheConfig
{
    /// <summary>
    /// Flag to exclude or not the default policy
    /// </summary>
    public bool ExcludeDefaultPolicy { get; set; }

    /// <inheritdoc cref="OutputCachePolicyBuilder.Expire(TimeSpan)"/>
    public TimeSpan? ExpirationTime { get; set; }

    /// <inheritdoc cref="OutputCachePolicyBuilder.NoCache"/>
    public bool NoCache { get; set; }

    /// <inheritdoc cref="OutputCachePolicyBuilder.SetVaryByQuery(string[])"/>
    public string[]? VaryByQueryKeys { get; set; }

    /// <inheritdoc cref="OutputCachePolicyBuilder.SetVaryByHeader(string[])"/>
    public string[]? VaryByHeaders { get; set; }
}

/// <summary>
/// Collections of extensions to configure OutputCache
/// </summary>
public static class OutputCacheConfigExtensions
{
    /// <summary>
    /// Add and configure OuputCache
    /// </summary>
    public static IServiceCollection AddOutputCache(this IServiceCollection services, IConfiguration config)
    {
        if (config == null)
        {
            return services;
        }

        var outputCacheConfig = config.Get<OutputCacheConfig>();

        if (outputCacheConfig != null)
        {
            services.AddOutputCache(outputCacheConfig);
        }

        return services;
    }

    /// <summary>
    /// Add and configure OuputCache
    /// </summary>
    public static IServiceCollection AddOutputCache(this IServiceCollection services, OutputCacheConfig config)
    {
        return services.AddOutputCache(options =>
        {
            options.SizeLimit = config.SizeLimit;
            options.MaximumBodySize = config.MaximumBodySize;
            options.DefaultExpirationTimeSpan = config.DefaultExpirationTimeSpan;
            options.UseCaseSensitivePaths = config.UseCaseSensitivePaths;

            foreach (var policy  in config.NamedPolicies)
            {
                options.AddPolicy(policy.Key,
                    builder => PolicyBuilder(builder, policy.Value),
                    policy.Value.ExcludeDefaultPolicy);
            }
        });
    }

    private static void PolicyBuilder(OutputCachePolicyBuilder builder, NamedCacheConfig policy)
    {
        if (policy.Duration.HasValue)
            builder.Expire(policy.Duration.Value);

        if (policy.NoCache)
            builder.NoCache();

        if (policy.VaryByQueryKeys != null)
            builder.SetVaryByQuery(policy.VaryByQueryKeys);

        if (policy.VaryByHeaders != null)
            builder.SetVaryByHeader(policy.VaryByHeaders);
    }
}
