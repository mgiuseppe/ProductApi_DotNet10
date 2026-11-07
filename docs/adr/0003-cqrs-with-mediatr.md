# 0003. CQRS with MediatR

- Status: Accepted
- Date: 2026-11-07

## Context
Writes need aggregates and their rules. Reads only need projections, often across tables: loading aggregates for them is wasteful.

## Decision
- Each use case is a MediatR request (`XxxCommand` or `XxxQuery`) with its handler in the same file.
- Command handlers use repositories and the unit of work ([0004](0004-repositories-and-unit-of-work.md)).
- Query handlers read through `IQueryContext`, which exposes the `DbSet`s, and project straight to response DTOs.

## Consequences
- Reads are optimised without touching the domain model.
- Pipeline behaviours are available for cross-cutting concerns.
- Application references the EF Core package, never the Infrastructure project.
- Query handler tests need a real database, so they live in `Infrastructure.Tests` ([0010](0010-integration-tests-with-testcontainers.md)).
- At this size MediatR is more code than a service layer: it is here to practise the pattern.

## Alternatives considered
- **Application services**: less code, no pipeline.
- **Separate read model or database**: not justified without scale.
