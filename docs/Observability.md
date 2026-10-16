# Observability
The API exposes three kinds of signals:
- traces and metrics exported with OpenTelemetry (automatic instrumentation + a few custom metrics)
- logs written with `ILogger`, exported with OpenTelemetry and correlated with the traces
- health check endpoints (`/alive`, `/health`)

## Instrumenting
`Api` registers OpenTelemetry with automatic instrumentation, no custom spans:
- traces: incoming ASP.NET Core requests, outgoing `HttpClient` calls and EF Core queries
- metrics: ASP.NET Core, `HttpClient`, .NET runtime (GC, thread pool, exceptions) the custom `ProductApi.Orders` meter (see [Custom metrics](#custom-metrics)) and the GenAI metrics of the chat client (see [AI](AI.md#observability))

EF Core does not create spans by itself: they come from the `OpenTelemetry.Instrumentation.EntityFrameworkCore` package (still prerelease), registered with `AddEntityFrameworkCoreInstrumentation()`. Each query becomes a child span of the request, with the SQL text.

Data is exported over OTLP only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set; without it nothing is sent, so the API runs without a collector.

### Logs
`UseOtlpExporter()` exports all three signals, logs included: every `ILogger` entry goes to the backend with the `TraceId`/`SpanId` of the current request, so from a log you can jump to its trace (and the `traceId` returned in a 500 ProblemDetails leads to both).

`WithLogging()` in `Api/DependencyInjection.cs` sets two options, both `false` by default:

| Option | Effect | Why |
|---|---|---|
| `IncludeScopes` | exports the `ILogger.BeginScope` values as log attributes. ASP.NET Core opens a scope for every request with `RequestPath`, `RequestId` and `ConnectionId` | `RequestPath` tells which endpoint wrote the log, also for logs that do not carry it (e.g. EF Core `Executed DbCommand`), and lets you filter logs by endpoint without going through the trace. Without the option these values are dropped |
| `IncludeFormattedMessage` | the log message is the rendered text (`Unhandled exception on GET /boom`) instead of the template (`Unhandled exception on {Method} {Path}`) | readable messages in the backend. The parameters are still exported as separate attributes, so filtering works the same |

About the scope fields:
- `RequestPath`: the request path, the useful one.
- `RequestId`: id of the single request (`HttpContext.TraceIdentifier`, `<ConnectionId>:<sequence>`). Mostly redundant with `TraceId`; it helps only when a request is not traced.
- `ConnectionId`: id of the Kestrel TCP connection, shared by requests on the same keep-alive/HTTP/2 connection. Useful only for connection-level issues.

Scopes exist only for logs written during a request: startup logs (`Now listening on...`) never have them.

In the Aspire dashboard the scope attributes are visible in *Structured logs* → log details; the log entries shown inside a trace do not list them. The trace view shows the span name (`GET /orders/{id:guid}`), which is not the log message.

### Run the Aspire dashboard
The standalone Aspire dashboard receives OTLP and shows traces, metrics and logs:
```
docker run --rm -it -p 18888:18888 -p 4317:18889 --name aspire-dashboard mcr.microsoft.com/dotnet/aspire-dashboard:latest
```
- UI: http://localhost:18888. The login token is printed in the container log (`Login to the dashboard at ...`); to skip it locally add `-e DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true`.
- OTLP/gRPC: the container port 18889 is published on the standard port 4317.

### Settings
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

### Privacy
Sensitive data is hidden by default (privacy by default, GDPR):
- EF Core: the SQL text shows only the parameter placeholders (`@p0`, `@__id_0`, ...), never their values, because they may contain personal data. To see the values, enable it explicitly after `UseSqlite` in `src/Infrastructure/DependencyInjection.cs`, only in development:
  ```csharp
  services.AddDbContext<AppDbContext>(options => options
      .UseSqlite(cs)
      .EnableSensitiveDataLogging());
  ```
- HTTP requests: query string values are redacted in the request spans (`url.query`) and request bodies are never recorded, for the same privacy/GDPR reasons.
- Logs: the request scope adds only the path and technical ids (no client IP, headers, cookies, query string or body), the same path already present in the request span. Formatting the message adds no new data either. What matters is what we log: never pass personal data (emails, names, IPs, bodies) as log parameters and never put it in routes.

## Custom metrics
`Application/Orders/OrderMetrics.cs` defines the meter `ProductApi.Orders`, created through `IMeterFactory` and registered as a singleton. `AddMeter(OrderMetrics.MeterName)` in `Api/DependencyInjection.cs` subscribes OpenTelemetry to it.

| Instrument | Type | Tags | Incremented when |
|---|---|---|---|
| `orders.confirmed` | `Counter<long>` (`{order}`) | `with_voucher` (`true`/`false`) | `ConfirmOrderCommand` saves successfully |
| `vouchers.redemption.concurrent_conflicts` | `Counter<long>` (`{conflict}`) | - | `SaveChangesAsync` throws `ConflictException` (two orders redeemed the same voucher at the same time, unique index violation → 409) |

They are meant to spot trends and anomalies (e.g. a spike of conflicts), not to count exactly: counters live in-process, are exported periodically, reset on restart, and the history is kept by the backend. Exact numbers come from the database.

In the Aspire dashboard they are under *Metrics* → `ProductApi.Orders`.

## Health checks
`AddDefaultHealthChecks()` and `MapHealthChecks()` in `Api/DependencyInjection.cs` expose two endpoints:

| Endpoint | Meaning | Checks run |
|---|---|---|
| `/alive` | liveness: the process is up | only checks tagged `live` (`self`, always healthy) |
| `/health` | readiness: the app can serve requests | every check, including `database` |

The `database` check is registered by `Infrastructure` (`AddDbContextCheck<AppDbContext>`, package `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore`), so `Api` does not need to know about EF Core. It runs `Database.CanConnectAsync()`.

Both return `200 Healthy` or `503 Unhealthy` with a plain-text body.

Both endpoints are excluded from tracing (`Filter` on the ASP.NET Core instrumentation): orchestrators and load balancers call them every few seconds, so they would flood the backend with identical traces. The EF Core spans of the `database` check are dropped as well, because the default sampler is parent-based. They still appear in the HTTP server metrics, which are aggregated and cheap. To investigate slow probes, remove the filter temporarily.

Try them with `src/Api/TestsHttp/ApiHealthChecks.http`.
