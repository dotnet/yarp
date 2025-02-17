using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using HealthChecks.ApplicationStatus.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using static System.Net.WebRequestMethods;

namespace Microsoft.Extensions.Hosting;

// Adds common .NET Aspire services: service discovery, resilience, health checks, and OpenTelemetry.
// This project should be referenced by each service project in your solution.
// To learn more about using this project, see https://aka.ms/dotnet/aspire/service-defaults
public static class Extensions
{
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();

        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Turn on resilience by default
            http.AddStandardResilienceHandler();

            // Turn on service discovery by default
            http.AddServiceDiscovery();
        });

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .SetExemplarFilter(ExemplarFilterType.TraceBased);
            })
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation()
                    .AddGrpcClientInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddOtlpExporter();
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {

            if (string.Equals(Environment.GetEnvironmentVariable("YARP_UNSAFE_SKIP_OLTP_CERT_VALIDATION"), "true", StringComparison.InvariantCultureIgnoreCase))
            {
                // We cannot use UseOtlpExporter() since it doesn't support configuration via OtlpExporterOptions
                // https://github.com/open-telemetry/opentelemetry-dotnet/issues/5802
                builder.Services.Configure<OtlpExporterOptions>(ConfigureOtlpExporterOptions);
                builder.Services.AddOpenTelemetry()
                    .WithLogging(logging => logging.AddOtlpExporter())
                    .WithMetrics(metrics => metrics.AddOtlpExporter())
                    .WithTracing(tracing => tracing.AddOtlpExporter());
            }
            else
            {
                builder.Services.AddOpenTelemetry().UseOtlpExporter();
            }
        }

        static void ConfigureOtlpExporterOptions(OtlpExporterOptions options)
        {
            options.HttpClientFactory = () =>
            {
                var handler = new HttpClientHandler();
                handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
                var httpClient = new HttpClient(handler);
                return httpClient;
            };
        }

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            // Add a default liveness check on application status.
            .AddApplicationStatus(tags: ["live"]);

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // Adding health checks endpoints to applications in non-development environments has security implications.
        // See https://aka.ms/dotnet/aspire/healthchecks for details before enabling these endpoints in non-development environments.
        if (app.Environment.IsDevelopment())
        {
            // All health checks must pass for app to be considered ready to accept traffic after starting
            app.MapHealthChecks("/health");

            // Only health checks tagged with the "live" tag must pass for app to be considered alive
            app.MapHealthChecks("/alive", new HealthCheckOptions
            {
                Predicate = r => r.Tags.Contains("live")
            });
        }

        return app;
    }
}
