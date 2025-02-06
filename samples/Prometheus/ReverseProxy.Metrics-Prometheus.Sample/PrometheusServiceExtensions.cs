// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.DependencyInjection;
using Yarp.Telemetry.Consumption;

namespace Yarp.Sample
{
    public static class PrometheusServiceExtensions
    {
        public static IServiceCollection AddPrometheusForwarderMetrics(this IServiceCollection services)
        {
            services.AddTelemetryListeners();
            services.AddSingleton<IMetricsConsumer<ForwarderMetrics>, PrometheusForwarderMetrics>();
            return services;
        }

        public static IServiceCollection AddPrometheusDnsMetrics(this IServiceCollection services)
        {
            services.AddTelemetryListeners();
            services.AddSingleton<IMetricsConsumer<NameResolutionMetrics>, PrometheusDnsMetrics>();
            return services;
        }

        public static IServiceCollection AddPrometheusKestrelMetrics(this IServiceCollection services)
        {
            services.AddTelemetryListeners();
            services.AddSingleton<IMetricsConsumer<KestrelMetrics>, PrometheusKestrelMetrics>();
            return services;
        }

        public static IServiceCollection AddPrometheusOutboundHttpMetrics(this IServiceCollection services)
        {
            services.AddTelemetryListeners();
            services.AddSingleton<IMetricsConsumer<HttpMetrics>, PrometheusOutboundHttpMetrics>();
            return services;
        }

        public static IServiceCollection AddPrometheusSocketsMetrics(this IServiceCollection services)
        {
            services.AddTelemetryListeners();
            services.AddSingleton<IMetricsConsumer<SocketsMetrics>, PrometheusSocketMetrics>();
            return services;
        }

        public static IServiceCollection AddAllPrometheusMetrics(this IServiceCollection services)
        {
            services.AddPrometheusForwarderMetrics();
            services.AddPrometheusDnsMetrics();
            services.AddPrometheusKestrelMetrics();
            services.AddPrometheusOutboundHttpMetrics();
            services.AddPrometheusSocketsMetrics();
            return services;
        }
    }
}
