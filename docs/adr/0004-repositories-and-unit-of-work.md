# 0004. Repositories and unit of work

- Status: Accepted
- Date: 2026-11-07

## Context
Command handlers should not know how an aggregate is persisted, for example which `Include`s load it whole. EF Core already maps the aggregates and their child entities, so what is left to hide is small.

A use case can change two aggregates (confirming an order redeems a voucher). The commit must be one operation, owned by no single repository.

## Decision
- Repositories work like collections (load, add, remove) for aggregate roots only, and never save: a child entity such as an order line cannot be loaded or saved on its own.
- `AppDbContext` is the `IUnitOfWork`: command handlers call `SaveChangesAsync` once, at the end.
- Unique-constraint violations become `ConflictException` (HTTP 409), so upper layers do not see SQL Server error codes.

## Consequences
- Thin repositories: EF Core does the mapping.
- Command handler tests mock the repositories.
- One transaction per use case, visible in the handler.
- A handler that forgets `SaveChangesAsync` loses its changes: handler tests verify the call.

## Alternatives considered
- **Save inside each repository**: no atomicity across aggregates.
- **Implicit commit in a MediatR behaviour**: less code, but the commit is no longer visible in the handler.
- **`DbContext` directly in handlers**: fewer types, but loading rules (includes) leak into every handler.
