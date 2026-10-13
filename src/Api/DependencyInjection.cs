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
                .AddRuntimeInstrumentation());

        // export only when a collector is configured (OTEL_EXPORTER_OTLP_ENDPOINT)
        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            otel.UseOtlpExporter();
        }

        return builder;
    }
}