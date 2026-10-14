# Product API

This solution contains a product / order manager sample api implemented to refresh .NET knowledge.

## How To Run

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Docker Desktop (running)
- (optional) `dotnet-ef` to create/apply migrations from your machine: `dotnet tool install --global dotnet-ef`

### Develop: infrastructure in Docker, API with F5

1. Start the infrastructure
    ```bash
    docker compose up -d
    ```
    It starts Postgres (5432), MongoDB (27017) and the Aspire dashboard (UI 18888, OTLP 4317), then the `migrator` service applies the EF migrations and exits. The API is not started: it belongs to the `app` profile.
2. Run `src/Api` with F5 (Visual Studio / VS Code with C# Dev Kit, `http` profile) or `dotnet run --project src/Api`
    - API: http://localhost:5224, OpenAPI document at `/openapi/v1.json`
    - `appsettings.Development.json` already points to the compose services (`localhost:5432`, `localhost:27017`) and sends telemetry to `http://localhost:4317`
3. Open the Aspire dashboard at http://localhost:18888 to see traces, metrics and logs

When you add a migration, apply it from your machine (faster than rebuilding the migrator image):
```bash
dotnet ef migrations add <Name> --project src/Infrastructure --startup-project src/Api
dotnet ef database update --project src/Infrastructure --startup-project src/Api
```

### Check Before Pushing: Run everything in Docker (profile `app`)

The `api` service is in the `app` profile, so it starts only when the profile is enabled:
```bash
docker compose --profile app up --build -d
```
- API: http://localhost:8080, Aspire dashboard: http://localhost:18888
- `--build` rebuilds the API and migrator images with your latest code
- Useful to check the containerized app before pushing, or to try the app without the SDK.

### Ports and `.env`

Host ports default to the ones above. To change them, copy `.env.example` to `.env` (gitignored) and edit it: Compose reads it automatically.
If you change `POSTGRES_PORT`, `MONGO_PORT` or `OTLP_PORT`, update `src/Api/appsettings.Development.json` as well, otherwise the F5 API won't find them.

### Stop / reset

```bash
docker compose --profile app down     # stop and remove the containers (data is kept in volumes)
docker compose --profile app down -v  # also delete the Postgres and Mongo data
```
`--profile app` is needed to also remove the `api` container if it was started.

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