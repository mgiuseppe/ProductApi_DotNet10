# 0009. Migrations editable until merged, forward-only after

- Status: Accepted
- Date: 2026-11-07

## Context
A shared database is never rolled back and a published migration is never changed. During development, though, the model changes often, and a corrective migration for every attempt fills the history with noise. A short downtime during a deployment is acceptable.

EF Core only warns when it scaffolds a migration that may lose data: nothing blocks a `Down` that drops data (unlike DACPAC deployments, which block on possible data loss by default).

## Decision
- Migrations not yet on `main` can be edited, removed and rolled back.
- Migrations on `origin/main` are forward-only: never edited, `Down` never run on a shared database. A mistake is fixed with a new migration.
- No expand/contract: breaking changes (drop, rename, type change) go out in one release, with a short downtime.

## Consequences
- Local comfort, linear shared history.
- Undoing a change is a new migration, written and reviewed on purpose: its author owns what it does to the data, while a `Down` can lose data silently.
- A breaking change costs a short downtime, and a rollback may need a new migration instead of the previous image.
- A required CI check (and `/verify-and-commit` before a push) blocks a change that edits a merged migration or forgets one.

## Alternatives considered
- **Forward-only from the first commit**: a corrective migration for every local experiment.
- **Expand/contract**: needed for zero-downtime deployments; too heavy without high-availability requirements.
