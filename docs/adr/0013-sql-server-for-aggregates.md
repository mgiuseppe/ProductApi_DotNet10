# 0013. SQL Server (Azure SQL) for aggregates, MongoDB for order history

- Status: Accepted
- Date: 2026-10-28
- Supersedes: [0006](0006-postgresql-and-mongodb.md)

## Context
The first cloud target is Azure. SQL Server is the Microsoft standard relational database for .NET applications: it is what this app would use in production and what a Microsoft-stack reader expects. Azure SQL Database has a free offer, so the database stays inside Azure as a managed service: backups, updates and infrastructure maintenance are not on us. PostgreSQL on Azure has no permanent free tier. The Postgres-specific code is small and confined to Infrastructure.

## Decision
- **SQL Server** via EF Core (`Microsoft.EntityFrameworkCore.SqlServer`) for products, orders and vouchers: Azure SQL Database in the cloud, the `mcr.microsoft.com/mssql/server` container locally and in tests. The relational part of [0006](0006-postgresql-and-mongodb.md) is replaced; **MongoDB for the order history is unchanged**.
- snake_case names are kept (`EFCore.NamingConventions`), so raw SQL and check constraints did not change.
- `decimal(18,2)` for money, `uniqueidentifier` for ids, `datetime2` for dates. `datetime2` has no time zone, so a value converter marks every `DateTime` read back as UTC.
- The unique index on `orders.voucher_id` still stops concurrent double redemption: EF filters it with `IS NOT NULL` (SQL Server would otherwise allow only one order without a voucher), and errors 2601/2627 become `ConflictException` -> HTTP 409.
- `EnableRetryOnFailure`: Azure SQL serverless pauses when idle and returns transient errors while it resumes. No code opens its own transaction, so the retrying strategy needs no changes.
- Enum columns stored as strings get a max length (`orders.status`), otherwise SQL Server uses `nvarchar(max)`.
- The migration history restarts: `InitialCreate` is regenerated for SQL Server. A provider change is not backward compatible by nature: staying forward-only would mean keeping both databases running, which is not worth it. So it is treated as a one-time exception to [0010](0010-migrations-editable-until-pushed.md). The "shared migrations untouched" check of `/verify-and-commit` is expected to fail on this change only: the maintainer approves that push by hand, and the check is not changed or bypassed.

## Consequences
- Same domain, application and API code; the changes are in Infrastructure (provider, conflict detection, UTC converter), the migration, the test database helper ([TestingStrategy](../TestingStrategy.md)) and Docker Compose.
- The SQL Server image is about 1.5 GB, needs about 2 GB of RAM and takes about 10 s to start: local setup and CI are heavier than with Postgres.
- On Azure SQL serverless the first request after a pause can take up to about a minute.
- Default collation is case-insensitive: string equality in queries ignores case (no current query depends on it).
- `Microsoft.Data.SqlClient` 6.1 pulls a vulnerable `System.Security.Cryptography.Pkcs`; it is pinned to a patched version in `Infrastructure.csproj` until SqlClient moves past it.
- [0004](0004-repositories-and-unit-of-work.md) and [0009](0009-migrations-with-migrator-container.md) still name Postgres/Npgsql: their decisions hold unchanged, read those mentions as SQL Server/SqlClient.
- Developer databases created by the old migration (the `pgdata` Docker volume) are left behind, not migrated.
- The local development password is in `docker-compose.yml` and `appsettings.Development.json`, as before; the container uses `sa` because SQL Server has no "create user on startup" option. Development only.

## Alternatives considered
- **Stay on PostgreSQL** (Azure Database for PostgreSQL Flexible Server): no code change and a lighter test container, but no permanent free tier and not the Microsoft standard this project wants to practise.
- **PostgreSQL on Neon's free tier**: no code change and free, but data outside Azure and a third-party provider.
- **PostgreSQL as a container on Container Apps with Azure Files**: free, but backups, persistence and upgrades are on us.
- **Support both providers**: two migration sets and two test matrices, too much for a single-maintainer project.
