# 0009. Migrations applied by a migrator container

- Status: Accepted
- Date: 2026-10-14

## Context
Applying migrations at API startup races when several instances start together, slows startup and needs DDL permissions on the application identity. A failed migration should stop a release, not leave a half-started API.

## Decision
The Dockerfile has a `migrator` target that builds an EF Core migrations bundle. In Docker Compose the `migrator` service runs after Postgres is healthy and exits; the `api` service starts only if it completed successfully. The API never migrates on startup. During development, migrations are applied from the host with `dotnet ef database update`.

## Consequences
- One place applies schema changes, in a predictable order before the new version.
- Migrations must be backward compatible with the running version (expand/contract), as described in [CICD](../CICD.md).
- One more image to build and keep in sync with the API.

## Alternatives considered
- **`Database.Migrate()` at startup**: simplest, but races across instances and couples startup to DDL rights.
- **SQL scripts applied by a DBA or a pipeline step**: more control, more manual steps than this project needs.
