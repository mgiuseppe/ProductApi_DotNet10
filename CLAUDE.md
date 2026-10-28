# CLAUDE.md

Product / order / voucher API built to refresh .NET skills: .NET 10, Clean Architecture, DDD aggregates, CQRS with MediatR, EF Core on SQL Server (Azure SQL), order history on MongoDB, OpenTelemetry, one LLM feature via `IChatClient`.

Read [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) before changing structure, and the ADRs in [docs/adr/](docs/adr/README.md) before revisiting a decision.

## Commands

```bash
docker compose up -d                                   # SQL Server, MongoDB, Aspire dashboard, migrator
dotnet run --project src/Api                           # API on http://localhost:5224
dotnet build ProductApi.slnx
dotnet format ProductApi.slnx --verify-no-changes      # format gate
dotnet test tests/Domain.Tests                         # fast, no dependencies
dotnet test tests/Application.Tests                    # fast, mocks
dotnet test tests/Infrastructure.Tests                 # needs Docker running (Testcontainers)
dotnet ef migrations add <Name> --project src/Infrastructure --startup-project src/Api
```

## Layer rules

Dependency direction: `Api -> Application -> Domain`, `Infrastructure -> Application`. Never break it.

- **Domain**: aggregates with private setters, private parameterless constructor for EF, static `Create` factories, state changes only through methods that enforce invariants and throw `DomainException`. No EF, no infrastructure. The only allowed external reference is `MediatR.Contracts` (`INotification` for domain events, [ADR-0005](docs/adr/0005-domain-events-before-commit.md)).
- **Application**: one file per use case (`XxxCommand`/`XxxQuery` record + its handler, `internal`). Command handlers load via repositories, call domain methods, then `IUnitOfWork.SaveChangesAsync` explicitly. Query handlers read through `IQueryContext` and never through repositories. Responses map with `FromDomain`. References EF Core and `Microsoft.Extensions.AI.Abstractions` packages, never the Infrastructure project.
- **Infrastructure**: `AppDbContext` (internal, is also `IUnitOfWork` and `IQueryContext`), Fluent API configurations, repositories, Mongo, AI client registration. No business rules.
- **Api**: minimal API endpoints that send a MediatR request and map the result to an HTTP status. No try/catch: `ProblemDetailsExceptionHandler` maps `NotFoundException` (404), `DomainException` (400), `ConflictException` (409), `BadHttpRequestException` (its status, usually 400), anything else (500).

## Conventions

- Business rules live in Domain; handlers only coordinate.
- Ids are generated in the domain (`Guid.NewGuid()`, `ValueGeneratedNever`).
- Dates are UTC; time-dependent code takes `TimeProvider`, never `DateTime.Now`.
- Collections returned from repositories and queries are `IReadOnlyList<T>` (materialized), not `IEnumerable<T>`.
- The relational database uses snake_case names: raw SQL must use them (`discount_amount`).
- Event handlers must not call `SaveChanges` (it re-enters the interceptor).
- Prompts and `ChatOptions` stay in the use case handler; never log prompt contents.
- Secrets never go in `appsettings.json`: user-secrets for F5, `.env` for Docker.

## Tests

- One test class per tested class, names `Method_Condition_Result`, AAA comments (`// Arrange`, `// Act`, `// Assert`), xUnit + Moq.
- Domain logic -> `Domain.Tests`. Command handlers -> `Application.Tests` with mocked repositories. Repositories and **query handlers** -> `Infrastructure.Tests` against real SQL Server.
- Never call the real LLM in tests: mock `IChatClient`.

## Workflow rules

- GitHub Flow ([CONTRIBUTING](CONTRIBUTING.md)): never commit or push on `main`. Work on a short-lived branch or worktree, open a pull request, merge with squash. Keep pull requests small; formatting-only changes get their own pull request.
- Before every commit run the format gate and the unit tests; before every push also `Infrastructure.Tests`. Use the `/verify-and-commit` skill. Do not commit if anything fails, and do not skip checks with `--no-verify`.
- A change to layers, persistence, messaging, transaction boundaries or external integrations is an architectural decision: write a new ADR in `docs/adr/` (template in its README) before or with the code, and update `docs/ARCHITECTURE.md`. Workflow rules and implementation details are not ADRs: they go in the topic documents. Never rewrite an accepted ADR: supersede it with a new one.
- After a non-trivial change, run the `architecture-reviewer` subagent on the diff.
- Migrations: edit freely until pushed; once on `origin/main` they are forward-only (never edited, `Down` never run on a shared database). No expand/contract: breaking changes go out in one release; the risky operations are listed in `docs/EFCore.md` as a reference ([ADR-0010](docs/adr/0010-migrations-editable-until-pushed.md)).
- Keep docs in English; commit messages short and imperative.
