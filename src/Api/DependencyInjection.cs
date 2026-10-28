using System.Text.Json.Serialization.Metadata;
using Application.Orders;
using Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Driver;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Api;

public static class DependencyInjection
{
    private const string LivenessPath = "/alive";
    private const string ReadinessPath = "/health";

    // Extension method for WebApplicationBuilder
    public static WebApplicationBuilder AddCustomOpenTelemetry(this WebApplicationBuilder builder)
    {
        // OpenTelemetry: automatic instrumentation, no custom spans needed for requests
        var otel = builder.Services.AddOpenTelemetry()
            .ConfigureResource(
                resource => resource.AddService(builder.Environment.ApplicationName)
            )
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options =>
                    // Health check probes are frequent and identical: keep them out of traces
                    options.Filter = context =>
                        !context.Request.Path.StartsWithSegments(LivenessPath) &&
                        !context.Request.Path.StartsWithSegments(ReadinessPath))
                .AddHttpClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation()
                // The MongoDB driver (3.x) emits its own spans natively: subscribing to its source is enough
                .AddSource(MongoTelemetry.ActivitySourceName)
                .AddSource(AITelemetry.SourceName))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddMeter(OrderMetrics.MeterName)
                .AddMeter(AITelemetry.SourceName) // GenAI token usage and call duration
            .AddRuntimeInstrumentation())
            .WithLogging(
                logging => { },
                options =>
                {
                    options.IncludeScopes = true;
                    options.IncludeFormattedMessage = true;
                });

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

    /// <summary>
    /// Serves the health checks only on the dedicated port <c>HealthChecks:Port</c>, in every environment.
    /// The platform probes call that port directly; the public ingress exposes only the API port, so callers on
    /// the Internet cannot reach them (each /health call queries the databases). The check is on the local port
    /// the request arrived on, not on the Host header, which a client could set to anything.
    /// </summary>
    public static WebApplication UseHealthChecksOnDedicatedPort(this WebApplication app)
    {
        var port = app.Configuration.GetValue<int?>("HealthChecks:Port")
                   ?? throw new InvalidOperationException("Configuration 'HealthChecks:Port' is required");

        // Liveness: the process is up. Runs only checks tagged as "live", so a database outage never restarts the app.
        app.UseHealthChecks(LivenessPath, port, new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("live")
        });

        // Readiness: the app can serve requests (databases reachable). Runs every check.
        app.UseHealthChecks(ReadinessPath, port);

        return app;
    }

    public static WebApplicationBuilder AddOrderHistoryJson(this WebApplicationBuilder builder)
    {
        builder.Services.ConfigureHttpJsonOptions(o =>
        {
            var resolver = o.SerializerOptions.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver();
            o.SerializerOptions.TypeInfoResolver = resolver.WithAddedModifier(ti =>
            {
                if (ti.Type != typeof(OrderHistoryEntry)) return;

                ti.PolymorphismOptions = new JsonPolymorphismOptions
                {
                    TypeDiscriminatorPropertyName = "type",
                    DerivedTypes =
                    {
                        new JsonDerivedType(typeof(OrderConfirmedEntry), OrderHistoryEntriesTypes.OrderConfirmedType),
                        new JsonDerivedType(typeof(OrderCanceledEntry), OrderHistoryEntriesTypes.OrderCanceledType)
                    }
                };
            });
        });
        return builder;
    }
}