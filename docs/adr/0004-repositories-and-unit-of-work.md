# 0004. Repositories and unit of work

- Status: Accepted
- Date: 2026-09-30

## Context
A use case can change more than one aggregate (confirming an order with a voucher changes both). The commit must be a single operation, and no single repository should own it.

## Decision
Repositories behave like collections (load, add, remove) for aggregate roots only and never save. `AppDbContext` implements `IUnitOfWork`; command handlers call `SaveChangesAsync` explicitly at the end. `DbContext`, repositories and the unit of work are scoped per request. Lists are returned materialized as `IReadOnlyList<T>`.

## Consequences
- One transaction per use case, visible in the handler.
- `AppDbContext.SaveChangesAsync` translates Postgres unique violations into `ConflictException`, so upper layers do not depend on Npgsql exception types.
- A handler that forgets `SaveChangesAsync` silently loses its changes: handler tests must verify the call.

## Alternatives considered
- **Save inside each repository**: breaks atomicity across aggregates.
- **Implicit commit in a MediatR behavior**: less boilerplate, but the commit becomes invisible from the handler.
- **No repositories, `DbContext` directly in handlers**: fewer types, but aggregate loading rules (includes) leak into every handler.
