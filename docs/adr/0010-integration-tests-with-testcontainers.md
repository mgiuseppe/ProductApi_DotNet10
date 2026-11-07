# 0010. Integration tests on SQL Server with Testcontainers

- Status: Accepted
- Date: 2026-11-07

## Context
Repositories and query handlers depend on how EF Core translates LINQ to SQL. In-memory providers translate differently and hide bugs. Tests must stay isolated and run in parallel.

## Decision
- `Infrastructure.Tests` start one SQL Server container per run with Testcontainers.
- Tests take a migrated database from a pool; Respawn empties it after each test.
- Query handler tests live here too, grouped by dependency rather than by project.

## Consequences
- Tests exercise real SQL, constraints and migrations.
- Docker is needed for this suite, locally and in CI.
- Isolation relies on Respawn deleting every row.

## Alternatives considered
- **EF Core InMemory provider or SQLite**: fast, but different SQL and no real constraints.
- **A new database per test**: SQL Server creates databases one at a time, about three times slower.
