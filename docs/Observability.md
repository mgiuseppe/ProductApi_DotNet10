# Instrumenting
`Api` registers OpenTelemetry with automatic instrumentation only, no custom spans:
- traces: incoming ASP.NET Core requests, outgoing `HttpClient` calls and EF Core queries
- metrics: ASP.NET Core, `HttpClient` and .NET runtime (GC, thread pool, exceptions)

EF Core does not create spans by itself: they come from the `OpenTelemetry.Instrumentation.EntityFrameworkCore` package (still prerelease), registered with `AddEntityFrameworkCoreInstrumentation()`. Each query becomes a child span of the request, with the SQL text.

Data is exported over OTLP only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set; without it nothing is sent, so the API runs without a collector.

## Run the Aspire dashboard
The standalone Aspire dashboard receives OTLP and shows traces, metrics and logs:
```
docker run --rm -it -p 18888:18888 -p 4317:18889 --name aspire-dashboard mcr.microsoft.com/dotnet/aspire-dashboard:latest
```
- UI: http://localhost:18888. The login token is printed in the container log (`Login to the dashboard at ...`); to skip it locally add `-e DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true`.
- OTLP/gRPC: the container port 18889 is published on the standard port 4317.

## Settings
The SDK reads the standard `OTEL_*` keys from configuration, so they can be set in any configuration source:

| Key | Value | Notes |
|---|---|---|
| `OTEL_EXPORTER_OTLP_ENDPOINT` | `http://localhost:4317` | required to enable the export |
| `OTEL_EXPORTER_OTLP_PROTOCOL` | `grpc` (default) or `http/protobuf` | with `http/protobuf` use port 4318 (dashboard port 18890) |
| `OTEL_SERVICE_NAME` | e.g. `product-api` | defaults to the application name (`Api`) |

Where to set them:
- local development: `environmentVariables` of the profile in `src/Api/Properties/launchSettings.json` (used by `dotnet run` and the IDE)
  ```json
  "environmentVariables": {
    "ASPNETCORE_ENVIRONMENT": "Development",
    "OTEL_EXPORTER_OTLP_ENDPOINT": "http://localhost:4317"
  }
  ```
- one-off run from the shell: `$env:OTEL_EXPORTER_OTLP_ENDPOINT="http://localhost:4317"; dotnet run --project src/Api` (PowerShell)
- deployed environments: environment variables of the host/container. Avoid `appsettings.json`, otherwise every environment exports to the same endpoint.

## Privacy
Sensitive data is hidden by default (privacy by default, GDPR):
- EF Core: the SQL text shows only the parameter placeholders (`@p0`, `@__id_0`, ...), never their values, because they may contain personal data. To see the values, enable it explicitly after `UseSqlite` in `src/Infrastructure/DependencyInjection.cs`, only in development:
  ```csharp
  services.AddDbContext<AppDbContext>(options => options
      .UseSqlite(cs)
      .EnableSensitiveDataLogging());
  ```
- HTTP requests: query string values are redacted in the request spans (`url.query`) and request bodies are never recorded, for the same privacy/GDPR reasons.
