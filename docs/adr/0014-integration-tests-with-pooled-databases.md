# 0014. Integration tests on SQL Server with pooled databases

- Status: Accepted
- Date: 2026-10-28
- Supersedes: [0008](0008-integration-tests-with-testcontainers.md)

## Context
[0008](0008-integration-tests-with-testcontainers.md) gave each test its own database, cloned from a migrated template with Postgres `CREATE DATABASE ... TEMPLATE`. With SQL Server ([0013](0013-sql-server-for-aggregates.md)) there is no template clone, and SQL Server runs `CREATE DATABASE` and `RESTORE` one at a time: a fresh database per test (created and migrated, or restored from a backup) took about 65 s for 40 tests, against 7-11 s on Postgres.

## Decision
`Infrastructure.Tests` start one SQL Server container per test run with Testcontainers. `TestDatabase` keeps a **pool of migrated databases**: a test takes a free one, or a new one is created and migrated; on dispose its rows are deleted with **Respawn** (`__EFMigrationsHistory` excluded) and it goes back to the pool. The pool grows only to the number of tests running at the same time. A test that needs a database without tables gets a fresh one, dropped on dispose. Tests did not change: they still create a `TestDatabase` per test and dispose it.

## Consequences
- Each test still starts on an empty, migrated database and test classes still run in parallel.
- The run takes about 23 s, of which about 10 s is the container start.
- Isolation depends on Respawn deleting every row: a test that adds data outside the tracked tables (for example a new schema) would need the Respawn options updated.
- A new dependency in the test project: `Respawn`.
- Tests exercise the real SQL, constraints and migrations, as before; Docker must be running, locally and in CI.

## Alternatives considered
- **A new database per test, migrated each time**: simplest code, about 65 s per run.
- **Migrate a template once, back it up, restore it per test**: the closest copy of the Postgres approach, still about 65 s because restores run one at a time.
- **One shared database with cleanup and sequential tests**: no parallelism.
- **Transactions rolled back at the end of each test**: does not work with code that commits and checks the result from a new context, which is what repository tests do.
