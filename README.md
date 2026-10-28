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
    It starts SQL Server (1433), MongoDB (27017) and the Aspire dashboard (UI 18888, OTLP 4317), then the `migrator` service applies the EF migrations and exits. The API is not started: it belongs to the `app` profile.
2. Run `src/Api` with F5 (Visual Studio / VS Code with C# Dev Kit, `http` profile) or `dotnet run --project src/Api`
    - API: http://localhost:5224, OpenAPI document at `/openapi/v1.json`
    - `appsettings.Development.json` already points to the compose services (`localhost:1433`, `localhost:27017`) and sends telemetry to `http://localhost:4317`
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
If you change `SQLSERVER_PORT`, `MONGO_PORT` or `OTLP_PORT`, update `src/Api/appsettings.Development.json` as well, otherwise the F5 API won't find them.

The order summary (`GET /orders/{id}/summary`) calls OpenAI. With F5 the key comes from user-secrets (`dotnet user-secrets set "OpenAI:ApiKey" "<key>" --project src/Api`); the containerized API can't see user-secrets, so set `OPENAI_API_KEY` in `.env`. Without a key everything else still works and only the summary endpoint fails.

### Stop / reset

```bash
docker compose --profile app down     # stop and remove the containers (data is kept in volumes)
docker compose --profile app down -v  # also delete the SQL Server and Mongo data
```
`--profile app` is needed to also remove the `api` container if it was started.

## Contributing: GitHub Flow

`main` is protected: every change goes through a branch and a pull request, merged with squash ([ADR-0011](docs/adr/0011-github-flow.md), [ADR-0012](docs/adr/0012-squash-merge-only.md)).

Create the branch before starting the work. Commits go through Claude Code with `/verify-and-commit`, so that every commit passes the quality gate (format, build, tests, migration checks); when asked, the skill also pushes the branch and opens the pull request.

```bash
git switch main && git pull
git switch -c feature/<short-name>          # or fix/, docs/, chore/
# ...work, then in Claude Code: /verify-and-commit (ask it to push and open the PR)...
gh pr merge --squash --delete-branch        # after reading the diff in the pull request
```

### One-time repository setup

The protection of `main` is versioned in `.github/rulesets/main.json`. GitHub does not read that file by itself: it is applied once with the GitHub CLI by a repository admin (`gh auth login` first, `gh auth status` to check):

```bash
gh api --method POST repos/mgiuseppe/ProductApi_DotNet10/rulesets --input .github/rulesets/main.json
gh repo edit mgiuseppe/ProductApi_DotNet10 --enable-squash-merge --enable-merge-commit=false --enable-rebase-merge=false --delete-branch-on-merge
```

To change the protection, edit the file and update the existing ruleset: find its id with `gh api repos/mgiuseppe/ProductApi_DotNet10/rulesets`, then `gh api --method PUT repos/mgiuseppe/ProductApi_DotNet10/rulesets/<id> --input .github/rulesets/main.json`. Changes made in the GitHub UI are not written back to the file.

## Tools and vscodes plugins

vs codes plugins:
- C# Dev Kit - Adds .net related right clicks options to the folder explorer
- REST Client - lets you use .http files to test api requests

## Working with Claude Code

The repository is set up for agentic development with Claude Code:
- [CLAUDE.md](CLAUDE.md): commands, layer rules, conventions and workflow rules the agent follows
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md): the map of the solution
- [docs/adr/](docs/adr/README.md): Architecture Decision Records, one per decision
- `.claude/skills/verify-and-commit`: format, build, unit and integration tests, then commit (`/verify-and-commit`)
- `.claude/agents/architecture-reviewer`: subagent that reviews a diff against the layer rules and the ADRs

## Clean Architecture

See [Clean Architecture](docs/CleanArchitecture.md)

## CQRS and MediatR
- MediatR was introduced to practice the pattern. With this scope (few aggregates, one consumer) a basic service layer would need less code than commands. Its concrete advantage is pipeline behaviors.
- `Behaviors/` contains a commented-out `UnitOfWorkBehavior` for reference, not registered: it would make the commit implicit and invisible from the handlers.
- Query handlers read through `IQueryContext` (EF Core `DbSet`s) and bypass repositories, because a read does not need to protect invariants. As a consequence, Application references the EF Core package (never the Infrastructure project).
- Query handler tests live in `Infrastructure.Tests`: they need a real `AppDbContext` to verify the SQL translation, and the context is internal to Infrastructure.
- Endpoints contain no try/catch: `ProblemDetailsExceptionHandler` (a global `IExceptionHandler`) maps `NotFoundException`, `DomainException` and `ConflictException` to 404, 400 and 409.

See [DomainEventsHandling](docs/DomainEventsHandling.md)

## Testing Strategy

Infrastructure.Tests start a SQL Server container with Testcontainers (about 1.5 GB image, 2 GB of RAM): Docker must be running.

See [Testing Strategy](docs/TestingStrategy.md)

## EFCore

See [EFCore](docs/EFCore.md)

## Observability

- OpenTelemetry traces, metrics and logs (automatic instrumentation), exported over OTLP when `OTEL_EXPORTER_OTLP_ENDPOINT` is set
- Custom metrics: meter `ProductApi.Orders` with `orders.confirmed` and `vouchers.redemption.concurrent_conflicts`
- Health checks: `/alive` (liveness) and `/health` (readiness, includes the databases), excluded from tracing, served only on the dedicated port `HealthChecks:Port` (5225 with F5, 8081 in Docker)

See [Observability](docs/Observability.md)

## AI

- `GET /orders/{id}/summary`: an LLM summary of an order and its history, for support operators
- `IChatClient` (Microsoft.Extensions.AI) with OpenAI; the API key comes from user-secrets (F5) or `.env` (Docker)
- LLM calls traced with OpenTelemetry (GenAI conventions, no prompt contents)

See [AI](docs/AI.md)

## Domain Simplifications

See [Domain Simplifications](docs/DomainSimplifications.md)

## CI/CD
see [CICD](docs/CICD.md)

## TODO
- add .editorconfig
- optional git hooks (Husky.Net) as a second line behind `/verify-and-commit`