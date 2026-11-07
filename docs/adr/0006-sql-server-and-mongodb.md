# 0006. SQL Server for aggregates, MongoDB for order history

- Status: Accepted
- Date: 2026-11-07

## Context
Aggregates need transactions and constraints (two orders must not redeem the same voucher). The order history is an append-only list of entries of different types, read by order id. The cloud target is Azure.

## Decision
- **SQL Server** with EF Core: Azure SQL Database (serverless) in the cloud, the `mssql/server` container locally and in tests.
  - snake_case names (`EFCore.NamingConventions`), `decimal(18,2)` for money, `datetime2` read back as UTC.
  - A filtered unique index on `orders.voucher_id` stops concurrent double redemption.
  - `EnableRetryOnFailure`, because the serverless database pauses when idle.
- **MongoDB** for the order history: Azure DocumentDB in the cloud, a container locally. Polymorphic documents with a type discriminator; the event id is the document id, so inserts are idempotent.

## Consequences
- Transactions and constraints where the rules live; a schema-free store for the log.
- No shared transaction between the two stores ([0005](0005-domain-events-before-commit.md)).
- Raw SQL must use snake_case names.
- The first request after a pause of the serverless database can take up to a minute.

## Alternatives considered
- **PostgreSQL**: lighter container, but no permanent free tier on Azure and not the Microsoft standard this project practises.
- **History in SQL Server**: simpler and transactional, the production choice; MongoDB is here to practise a second store.
