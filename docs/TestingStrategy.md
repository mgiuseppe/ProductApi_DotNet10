# Testing strategy

- Domain.Tests: pure unit tests, no dependencies.
- Application.Tests: command handlers, with repositories/UnitOfWork mocked.
- Infrastructure.Tests: repository integration tests (SQL Server via Testcontainers) AND query handler tests 
 - even though query handlers live in Application, their tests depend on the real AppDbContext (internal to Infrastructure) and verify actual SQL translation, so they're grouped by dependency rather than by source location.
 - one `mcr.microsoft.com/mssql/server` container for the whole run (`TestDatabase`), removed by the Testcontainers reaper (Ryuk) when the process exits.
 - databases are pooled: a test takes a free migrated database (or a new one is created and migrated) and on dispose Respawn deletes its rows (`__EFMigrationsHistory` excluded) and returns it to the pool, which grows only to the number of tests running at the same time. A test that needs a database without tables gets a fresh one, dropped on dispose. Every test starts on an empty, migrated database and classes still run in parallel.
 - why a pool: SQL Server runs `CREATE DATABASE` and `RESTORE` one at a time, so a fresh database per test (created and migrated, or restored from a backup of a migrated template) took about 65 s for 40 tests; the pool takes about 23 s, of which about 10 s is the container start (Postgres, with `CREATE DATABASE ... TEMPLATE`, took 7-11 s).
 - trade-offs: isolation depends on Respawn deleting every row (a new schema would need its options updated); one shared database with sequential tests would lose parallelism; rolling back a transaction per test does not work with tests that commit and read back from a new context.

## Pro tips
- red green refactor may be used but i'm not using it, many claude code's skills are using it (superpowers, mattpocock with /tdd)
- on real projects reviewing the tests is really important and can make the difference