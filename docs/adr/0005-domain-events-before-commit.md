# 0005. Domain events dispatched before the commit

- Status: Accepted
- Date: 2026-10-14

## Context
Confirming or canceling an order must write an entry to the order history. The aggregate should only state that something happened, not know who reacts.

## Decision
Aggregate roots collect domain events (`BaseAggregateRoot.AddDomainEvent`). `DispatchDomainEventsInterceptor`, an EF `SaveChanges` interceptor, reads and clears them and publishes them through MediatR **before** the commit. Domain events implement `INotification`, so Domain references `MediatR.Contracts`. Events carry a snapshot, not the entity. Metrics are recorded in the command handler after the save succeeds.

## Consequences
- A failing handler makes the save fail: the history is not lost silently.
- The MongoDB write is not in the EF transaction: if the commit fails, the history entry stays as an orphan. Accepted for a learning scope.
- Handlers run inside the request and add latency; handlers must not call `SaveChanges`.
- Events raised by handlers are not published in the same pass.
- The trade-offs per event (orphan acceptable? loss acceptable?) are discussed in [DomainEventsHandling](../DomainEventsHandling.md).

## Alternatives considered
- **Dispatch after the commit**: no orphans, but effects can be lost, and "after" is unreliable with outer transactions.
- **Transactional outbox**: no orphans and no loss (at-least-once with idempotent handlers), but needs a table, a background worker, retries, a dead-letter table and cleanup. Out of scope; the choice for production.
- **History in the relational database in the same transaction**: the simplest correct option in production; MongoDB was kept to practise a second store ([0006](0006-postgresql-and-mongodb.md)).
