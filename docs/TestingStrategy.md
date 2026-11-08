# Testing strategy

- **Domain.Tests**: pure unit tests, no dependencies.
- **Application.Tests**: command handlers, with repositories and `IUnitOfWork` mocked.
- **Infrastructure.Tests**: repositories and query handlers against a real SQL Server (Testcontainers).

Query handlers live in Application, but their tests are in Infrastructure.Tests: they need the real `AppDbContext` (internal to Infrastructure) and check the actual SQL translation. Tests are grouped by dependency, not by source location.

## SQL Server in tests

- One `mcr.microsoft.com/mssql/server` container for the whole run (`TestDatabase`), removed by the Testcontainers reaper (Ryuk) when the process exits.
- Databases are pooled: a test takes a free migrated database (or a new one is created and migrated); on dispose Respawn deletes its rows (`__EFMigrationsHistory` excluded) and returns it to the pool, which grows only to the number of tests running at the same time. A test that needs a database without tables gets a fresh one, dropped on dispose. Every test starts on an empty, migrated database, and classes still run in parallel.
- Why a pool: SQL Server runs `CREATE DATABASE` and `RESTORE` one at a time, so a fresh database per test (created and migrated, or restored from a backup of a migrated template) took about 65 s for 40 tests; the pool takes about 23 s, of which about 10 s is the container start (Postgres, with `CREATE DATABASE ... TEMPLATE`, took 7-11 s).
- Trade-offs: isolation depends on Respawn deleting every row (a new schema would need its options updated); one shared database with sequential tests would lose parallelism; rolling back a transaction per test does not work with tests that commit and read back from a new context.

## Notes

- Tests are written with the code, not test-first: no red-green-refactor.
- Generated tests are reviewed like production code: a test that checks the wrong thing passes anyway.
