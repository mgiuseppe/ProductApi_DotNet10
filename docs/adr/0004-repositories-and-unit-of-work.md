# 0004. Repositories and unit of work

- Status: Accepted
- Date: 2026-11-07

## Context
A use case can change two aggregates (confirming an order redeems a voucher). The commit must be one operation, owned by no single repository.

## Decision
- Repositories work like collections (load, add, remove) for aggregate roots only, and never save.
- `AppDbContext` is the `IUnitOfWork`: command handlers call `SaveChangesAsync` once, at the end.
- Unique-constraint violations become `ConflictException` (HTTP 409), so upper layers do not see SQL Server error codes.

## Consequences
- One transaction per use case, visible in the handler.
- A handler that forgets `SaveChangesAsync` loses its changes: handler tests verify the call.

## Alternatives considered
- **Save inside each repository**: no atomicity across aggregates.
- **Implicit commit in a MediatR behaviour**: less code, but the commit is no longer visible in the handler.
- **`DbContext` directly in handlers**: fewer types, but loading rules (includes) leak into every handler.
