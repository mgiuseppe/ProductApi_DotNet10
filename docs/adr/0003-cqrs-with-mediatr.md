# 0003. CQRS with MediatR

- Status: Accepted
- Date: 2026-11-07

## Context
Writes must go through aggregates, which enforce the invariants. Reads change nothing, so they have no invariants to protect: they need projections, often across tables, and loading aggregates for them is wasteful.

Each use case also needs a home. Large services grow with every use case; one small handler per use case does not. Domain events need an in-process publish/subscribe ([0005](0005-domain-events-before-commit.md)), and cross-cutting concerns (logging, validation) need a single place.

## Decision
- Each use case is a MediatR request (`XxxCommand` or `XxxQuery`) with its handler in the same file.
- Command handlers use repositories and the unit of work ([0004](0004-repositories-and-unit-of-work.md)).
- Query handlers read through `IQueryContext`, which exposes the `DbSet`s, and project straight to response DTOs.
- Endpoints depend only on `ISender`; domain events are MediatR notifications.

## Consequences
- Reads are optimised without touching the domain model.
- Pipeline behaviours are available for cross-cutting concerns.
- For reads, Application depends on EF Core directly instead of only on abstractions implemented by Infrastructure: simpler queries, less persistence ignorance.
- Query handler tests need a real database, so they live in `Infrastructure.Tests` ([0010](0010-integration-tests-with-testcontainers.md)).
- Indirection: "go to definition" on a request does not reach its handler.
- MediatR is commercial from version 13; its free community license covers this use.

## Alternatives considered
- **Application services**: less code, no pipeline, no notifications.
- **Mediator** (source-generated, MIT): faster and free for everyone, not explored because MediatR is free for this use.
- **Separate read model or database**: not justified without scale.
