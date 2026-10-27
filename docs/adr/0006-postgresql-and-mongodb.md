# 0006. PostgreSQL for aggregates, MongoDB for order history

- Status: Accepted
- Date: 2026-10-14

## Context
The project started on SQLite. Concurrency (two orders redeeming the same voucher), realistic SQL translation in tests and a production-like setup needed a server database. The order history is an append-only list of heterogeneous entries read by order id.

## Decision
- **PostgreSQL** via EF Core and Npgsql for products, orders and vouchers, with snake_case naming (`EFCore.NamingConventions`), `numeric(18,2)` for money and `timestamptz` for UTC dates. A unique index on `orders.voucher_id` stops concurrent double redemption (SqlState 23505 -> `ConflictException` -> HTTP 409).
- **MongoDB** for the order history: polymorphic documents (`OrderConfirmedEntry`, `OrderCanceledEntry`) with a type discriminator, the event id as document id (idempotent inserts), indexes created at startup.

## Consequences
- Relational integrity and transactions where invariants live; a schema-free store for the log.
- Two stores means no shared transaction ([0005](0005-domain-events-before-commit.md)) and two health checks.
- Raw SQL must use snake_case column names; Npgsql rejects non-UTC `DateTime`, so the domain normalizes dates.

## Alternatives considered
- **Stay on SQLite**: weak concurrency and different SQL translation from production.
- **SQL Server**: equally valid; Postgres chosen for the light container and a widely used open-source stack.
- **History in Postgres (table or `jsonb`)**: simpler and transactional, the production choice; MongoDB kept to practise a NoSQL store.
