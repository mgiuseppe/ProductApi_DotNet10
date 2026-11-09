# CLAUDE.md

Product / order / voucher API built to refresh .NET skills: .NET 10, Clean Architecture, DDD aggregates, CQRS with MediatR, EF Core on SQL Server (Azure SQL), order history on MongoDB, OpenTelemetry, one LLM feature via `IChatClient`.

Read [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) before changing structure, and the ADRs in [docs/adr/](docs/adr/README.md) before revisiting a decision.

Commands, tests, migrations, API documentation and the rules on branches and pull requests are in CONTRIBUTING, imported below: this file adds only what the agent needs on top.

@CONTRIBUTING.md

## Run locally

```bash
docker compose up -d                                   # SQL Server, MongoDB, Aspire dashboard, migrator
dotnet run --project src/Api                           # API on http://localhost:5224
```

## Layer rules

Dependency direction: `Api -> Application -> Domain`, `Infrastructure -> Application`. Never break it.

- **Domain**: aggregates with private setters, private parameterless constructor for EF, static `Create` factories, state changes only through methods that enforce invariants and throw `DomainException`. No EF, no infrastructure. The only allowed external reference is `MediatR.Contracts` (`INotification` for domain events, [ADR-0005](docs/adr/0005-domain-events-before-commit.md)).
- **Application**: one file per use case (`XxxCommand`/`XxxQuery` record + its handler, `internal`). Command handlers load via repositories, call domain methods, then `IUnitOfWork.SaveChangesAsync` explicitly. Query handlers read through `IQueryContext` and never through repositories. Responses map with `FromDomain`. References EF Core and `Microsoft.Extensions.AI.Abstractions` packages, never the Infrastructure project.
- **Infrastructure**: `AppDbContext` (internal, is also `IUnitOfWork` and `IQueryContext`), Fluent API configurations, repositories, Mongo, AI client registration. No business rules.
- **Api**: minimal API endpoints that send a MediatR request and map the result to an HTTP status. No try/catch: `ProblemDetailsExceptionHandler` maps the exceptions to HTTP statuses ([table](docs/ARCHITECTURE.md#errors)).

## Conventions

- Business rules live in Domain; handlers only coordinate.
- Ids are generated in the domain (`Guid.NewGuid()`, `ValueGeneratedNever`).
- Dates are UTC; time-dependent code takes `TimeProvider`, never `DateTime.Now`.
- Collections returned from repositories and queries are `IReadOnlyList<T>` (materialized), not `IEnumerable<T>`.
- The relational database uses snake_case names: raw SQL must use them (`discount_amount`).
- Event handlers must not call `SaveChanges` (it re-enters the interceptor).
- Prompts and `ChatOptions` stay in the use case handler; never log prompt contents.
- Secrets never go in `appsettings.json`: user-secrets for F5, `.env` for Docker.

## Agent rules

- Never commit or push on `main`. When you have to create a branch, propose two or three branch names and create the one the user chooses from an up-to-date `main`; never pick the name yourself.
- Do not commit if a check of `/verify-and-commit` fails, and never skip checks with `--no-verify`.
- Write the ADR before or with the code (template in `docs/adr/README.md`) and update `docs/ARCHITECTURE.md`. Never rewrite an accepted ADR: supersede it.
- After a non-trivial change, run the `architecture-reviewer` and `code-reviewer` subagents on the diff. Fix the bugs and clear problems they find; ask the user about judgment calls.
- Keep docs in English; commit messages short and imperative.
