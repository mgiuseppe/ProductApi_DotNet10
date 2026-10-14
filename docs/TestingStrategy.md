# Testing strategy

- Domain.Tests: pure unit tests, no dependencies.
- Application.Tests: command handlers, with repositories/UnitOfWork mocked.
- Infrastructure.Tests: repository integration tests (Postgres via Testcontainers) AND query handler tests 
 - even though query handlers live in Application, their tests depend on the real AppDbContext (internal to Infrastructure) and verify actual SQL translation, so they're grouped by dependency rather than by source location.
 - one `postgres:18` container for the whole run (`TestDatabase`), removed by the Testcontainers reaper (Ryuk) when the process exits.
 - migrations are applied once to a template database; every test clones it into its own database (`CREATE DATABASE ... TEMPLATE`) and drops it on dispose, so tests are isolated and classes still run in parallel.

## Pro tips
- red green refactor may be used but i'm not using it, many claude code's skills are using it (superpowers, mattpocock with /tdd)
- on real projects reviewing the tests is really important and can make the difference