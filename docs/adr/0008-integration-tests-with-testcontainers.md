# 0008. Integration tests with Testcontainers

- Status: Superseded by [0013](0013-sql-server-for-aggregates.md) (how the SQL Server tests work: [TestingStrategy](../TestingStrategy.md))
- Date: 2026-10-14

## Context
Repositories and query handlers depend on how EF Core translates LINQ to SQL. In-memory providers or SQLite translate differently from Postgres and hide bugs. Tests must stay isolated and run in parallel.

## Decision
`Infrastructure.Tests` start one `postgres:18` container per test run with Testcontainers. Migrations are applied once to a template database; each test clones it (`CREATE DATABASE ... TEMPLATE`) and drops it on dispose. Query handler tests live here, grouped by dependency rather than by source project. Domain and command handlers are unit-tested without containers.

## Consequences
- Tests exercise the real SQL, constraints and migrations.
- Docker must be running for `Infrastructure.Tests`, locally and in CI.
- The suite is slower than unit tests, so it runs before push and in CI, not on every commit.

## Alternatives considered
- **EF Core InMemory provider**: fast, but no SQL translation and no constraints.
- **SQLite in memory**: closer, but a different SQL dialect from production.
- **Shared database with cleanup between tests**: forces sequential runs and leaks state.
