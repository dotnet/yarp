// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Configuration;

namespace Yarp.Application.Configuration;

/// <summary>
/// Binds IConfiguration into the YarpAppConfig object model.
/// This is the single conversion point — all application code uses the resulting object model.
/// </summary>
public static class YarpAppConfigBinder
{
    public static YarpAppConfig Bind(IConfiguration configuration)
    {
        var config = new YarpAppConfig();

        configuration.GetSection("StaticFiles").Bind(config.StaticFiles);
        configuration.GetSection("NavigationFallback").Bind(config.NavigationFallback);
        configuration.GetSection("Compression").Bind(config.Compression);
        configuration.GetSection("Https").Bind(config.Https);
        configuration.GetSection("Telemetry").Bind(config.Telemetry);
        config.Headers = configuration.GetSection("Headers").Get<List<HeaderRule>>();
        config.Redirects = configuration.GetSection("Redirects").Get<List<RedirectRule>>();

        // Legacy env var support
        MapLegacyKeys(configuration, config);

        return config;
    }

    private static void MapLegacyKeys(IConfiguration configuration, YarpAppConfig config)
    {
        // YARP_ENABLE_STATIC_FILES -> StaticFiles.Enabled
        if (!config.StaticFiles.Enabled
            && string.Equals(configuration["YARP_ENABLE_STATIC_FILES"], "true", StringComparison.OrdinalIgnoreCase))
        {
            config.StaticFiles.Enabled = true;
        }

        // YARP_DISABLE_SPA_FALLBACK -> NavigationFallback.Path = null
        // Legacy: when static files were enabled, SPA fallback defaulted to on.
        // If the new config sets NavigationFallback.Path, that takes precedence.
        if (config.StaticFiles.Enabled
            && config.NavigationFallback.Path is null
            && !string.Equals(configuration["YARP_DISABLE_SPA_FALLBACK"], "true", StringComparison.OrdinalIgnoreCase))
        {
            // Legacy behavior: SPA fallback was on by default when static files were enabled
            // Only apply if using legacy keys (no explicit NavigationFallback section)
            if (!configuration.GetSection("NavigationFallback").Exists()
                && string.Equals(configuration["YARP_ENABLE_STATIC_FILES"], "true", StringComparison.OrdinalIgnoreCase))
            {
                config.NavigationFallback.Path = "/index.html";
            }
        }

        // YARP_UNSAFE_OLTP_CERT_ACCEPT_ANY_SERVER_CERTIFICATE -> Telemetry.UnsafeAcceptAnyCertificate
        if (!config.Telemetry.UnsafeAcceptAnyCertificate
            && string.Equals(configuration["YARP_UNSAFE_OLTP_CERT_ACCEPT_ANY_SERVER_CERTIFICATE"], "true", StringComparison.OrdinalIgnoreCase))
        {
            config.Telemetry.UnsafeAcceptAnyCertificate = true;
        }
    }
}
