# 0008. Migrations applied by a migrator

- Status: Accepted
- Date: 2026-11-07

## Context
Migrating at API startup races when several instances start, slows startup and needs schema rights on the API identity. A failed migration should stop a release, not leave a half-started API.

## Decision
- A separate `migrator` image (EF Core migrations bundle) applies the migrations and exits.
- Docker Compose runs it before the API; on Azure it is a Container Apps job that the deploy workflow runs before updating the API.
- The API never migrates.

## Consequences
- Schema changes happen in one place, before the new version.
- A failing migrator stops the deployment: the API keeps the previous image.
- One more image to build.

## Alternatives considered
- **`Database.Migrate()` at startup**: simplest, but races and couples startup to schema rights.
