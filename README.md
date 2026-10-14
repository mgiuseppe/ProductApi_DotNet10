# Product API

This solution contains a product / catalog manager sample api implemented to refresh .NET knowledge.

## How To Run

1. Start Postgres and MongoDB
    ```bash
    docker run -d --name productapi-postgres -p 5432:5432 -e POSTGRES_USER=productapi -e POSTGRES_PASSWORD=productapi -e POSTGRES_DB=productapi -v productapi-pgdata:/var/lib/postgresql postgres:18
    docker run -d --name mongo -p 27017:27017 mongo
    ```
2. Apply migrations (connection string `DbConnection` in `src/Api/appsettings.Development.json`)
    ```bash
    dotnet ef database update --project src/Infrastructure --startup-project src/Api
    ```
3. Run src/Api

4. (optional) Observability: 
    1. Start the Aspire dashboard with `docker run --rm -it -p 18888:18888 -p 4317:18889 mcr.microsoft.com/dotnet/aspire-dashboard:latest`,
    2. Set the environment variable `OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317` in src/Api/Properties/launchSettings.json
    3. Open http://localhost:18888

## Tools and vscodes plugins

vs codes plugins:
- C# Dev Kit - Adds .net related right clicks options to the folder explorer
- REST Client - lets you use .http files to test api requests

ClaudeCode:
- Brainstorming
- Writing tests / some of the code

## Clean Architecture

See [Clean Architecture](docs/CleanArchitecture.md)

## CQRS and MediatR
- MediatR was introduced to practice the pattern. With this scope (few aggregates, one consumer) a basic service layer would need less code than commands. Its concrete advantage is pipeline behaviors.
- `Behaviors/` contains a commented-out `UnitOfWorkBehavior` for reference, not registered: it would make the commit implicit and invisible from the handlers.
- Query handlers read through `IQueryContext` (EF Core `DbSet`s) and bypass repositories, because a read does not need to protect invariants. As a consequence, Application references the EF Core package (never the Infrastructure project).
- Query handler tests live in `Infrastructure.Tests`: they need a real `AppDbContext` to verify the SQL translation, and the context is internal to Infrastructure.
- Each endpoint translates `NotFoundException`, `DomainException` and `ConflictException` with its own try/catch. A global `IExceptionHandler` would centralize it.

See [DomainEventsHandling](docs/DomainEventsHAndling.md)

## Testing Strategy

Infrastructure.Tests start a Postgres container with Testcontainers: Docker must be running.

See [Testing Strategy](docs/TestingStrategy.md)

## EFCore

See [EFCore](docs/EFCore.md)

## Observability

- OpenTelemetry traces, metrics and logs (automatic instrumentation), exported over OTLP when `OTEL_EXPORTER_OTLP_ENDPOINT` is set
- Custom metrics: meter `ProductApi.Orders` with `orders.confirmed` and `vouchers.redemption.concurrent_conflicts`
- Health checks: `/alive` (liveness) and `/health` (readiness, includes the database), excluded from tracing

See [Observability](docs/Observability.md)

## Domain Simplifications

See [Domain Simplifications](docs/DomainSimplifications.md)

## TODO 
- autoformat before commit
- test gateway before commit (prove it works!)