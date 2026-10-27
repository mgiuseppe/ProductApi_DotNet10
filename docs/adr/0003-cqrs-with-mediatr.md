# 0003. CQRS with MediatR

- Status: Accepted
- Date: 2026-10-02

## Context
The first version used application services (`IOrderService`). Writes need aggregates and invariants; reads only need projections, often of several tables, and loading aggregates through repositories for them is wasteful.

## Decision
Each use case is a MediatR request (`XxxCommand` or `XxxQuery`) with its handler in the same file. Command handlers use repositories and the unit of work. Query handlers read through `IQueryContext`, which exposes the `DbSet`s of `AppDbContext`, and project directly to response DTOs.

## Consequences
- Reads are optimised independently of the domain model.
- Pipeline behaviors become available for cross-cutting concerns (a `UnitOfWorkBehavior` is kept commented out as a reference, not registered, so the commit stays explicit).
- Application references the EF Core package (never the Infrastructure project).
- Query handler tests need a real `AppDbContext`, so they live in `Infrastructure.Tests` ([0008](0008-integration-tests-with-testcontainers.md)).
- For three aggregates and one consumer, MediatR adds more code than a service layer: it was adopted to practise the pattern.

## Alternatives considered
- **Application services**: less code at this size; no pipeline.
- **Separate read model / read database**: unjustified without scale or different read shapes.
