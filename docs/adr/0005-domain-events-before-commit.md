# 0005. Domain events dispatched before the commit

- Status: Accepted
- Date: 2026-11-07

## Context
Confirming or canceling an order must write an entry in the order history. The aggregate should say what happened, not know who reacts.

## Decision
- Aggregate roots collect domain events; an EF `SaveChanges` interceptor publishes them through MediatR **before** the commit.
- Events implement `INotification`, so Domain references `MediatR.Contracts`.
- Events carry a snapshot, not the entity. Event handlers never call `SaveChanges`.

## Consequences
- A failing handler fails the save: history is not lost silently.
- The MongoDB write is outside the SQL transaction: if the commit fails, the history entry stays as an orphan. Accepted.
- Handlers run inside the request and add latency.

## Alternatives considered
- **Dispatch after the commit**: no orphans, but effects can be lost.
- **Transactional outbox**: no orphans and no loss, but needs a table, a worker and retries. The choice for production.
