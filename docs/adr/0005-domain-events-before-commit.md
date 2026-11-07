# 0005. Domain events dispatched before the commit

- Status: Accepted
- Date: 2026-11-07

## Context
When an aggregate changes, other parts of the system may need to react. The aggregate should say what happened, not know who reacts. Example: confirming or canceling an order writes an entry in the order history, on MongoDB.

## Decision
- Aggregate roots collect domain events; an EF `SaveChanges` interceptor publishes them through MediatR **before** the commit.
- Events implement `INotification`, so Domain references `MediatR.Contracts`.
- Events carry a snapshot, not the entity. Event handlers never call `SaveChanges`.

## Consequences
- If an event handler throws, `SaveChanges` stops before the commit: nothing is written, so a confirmed order never misses its history entry.
- The MongoDB write is outside the SQL transaction: if the commit fails, the history entry stays as an orphan. Accepted.
- Handlers run inside the request and add latency.

## Alternatives considered
Which option fits depends on whether an orphan or a lost effect is acceptable: see the table in [DomainEventsHandling](../DomainEventsHandling.md#criticality-four-levels).
- **Dispatch after the commit**: no orphans, but effects can be lost.
- **Transactional outbox**: no orphans and no loss, but needs a table, a worker and retries. The choice for production.
