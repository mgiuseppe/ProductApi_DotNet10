using Application.Orders;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Api;

public static class DependencyInjection
{
    // Extension method for WebApplicationBuilder
    public static WebApplicationBuilder AddCustomOpenTelemetry(this WebApplicationBuilder builder)
    {
        // OpenTelemetry: automatic instrumentation, no custom spans needed for requests
        var otel = builder.Services.AddOpenTelemetry()
            .ConfigureResource(
                resource => resource.AddService(builder.Environment.ApplicationName)
            )
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddMeter(OrderMetrics.MeterName)
                .AddRuntimeInstrumentation());

        // export only when a collector is configured (OTEL_EXPORTER_OTLP_ENDPOINT)
        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            otel.UseOtlpExporter();
        }

        return builder;
    }

    public static WebApplicationBuilder AddDefaultHealthChecks(this WebApplicationBuilder builder)
    {
        builder.Services.AddHealthChecks()
                        .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);
        return builder;
    }

    public static WebApplication MapHealthChecks(this WebApplication app)
    {
        // Liveness: the process is up. 
        // Runs only checks tagged as "live".
        app.MapHealthChecks("/alive", new HealthCheckOptions 
        { 
            Predicate = check =>  check.Tags.Contains("live") 
        });

        // Readiness: the app can serve requests (database reachable).
        // Runs every check.
        app.MapHealthChecks("/health");

        return app;
    }
}