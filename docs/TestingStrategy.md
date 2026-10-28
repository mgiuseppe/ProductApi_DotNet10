# Testing strategy

- Domain.Tests: pure unit tests, no dependencies.
- Application.Tests: command handlers, with repositories/UnitOfWork mocked.
- Infrastructure.Tests: repository integration tests (SQL Server via Testcontainers) AND query handler tests 
 - even though query handlers live in Application, their tests depend on the real AppDbContext (internal to Infrastructure) and verify actual SQL translation, so they're grouped by dependency rather than by source location.
 - one `mcr.microsoft.com/mssql/server` container for the whole run (`TestDatabase`), removed by the Testcontainers reaper (Ryuk) when the process exits.
 - databases are pooled: a test takes a free migrated database (or a new one is created and migrated) and on dispose Respawn deletes its rows and returns it to the pool. Every test starts on an empty, migrated database and classes still run in parallel. SQL Server creates and restores databases one at a time, so a fresh database per test was several times slower ([ADR-0014](adr/0014-integration-tests-with-pooled-databases.md)).

## Pro tips
- red green refactor may be used but i'm not using it, many claude code's skills are using it (superpowers, mattpocock with /tdd)
- on real projects reviewing the tests is really important and can make the difference