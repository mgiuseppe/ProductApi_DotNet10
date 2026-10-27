# 0010. Migrations editable until pushed, forward-only after

- Status: Accepted
- Date: 2026-10-27

## Context
Forward-only migrations are the safe practice in production: a shared database is never rolled back and a published migration is never changed. During development, though, the model changes often, and being forced to add a corrective migration for every local attempt fills the history with noise. The repository has a single developer working on `main`, without pull requests yet, and the app has no high-availability requirement: a short downtime during a deployment is acceptable.

## Decision
- **Local** migrations (not pushed) can be edited, removed, regenerated and rolled back with `Down`.
- **Pushed** migrations (on `origin/main`) are forward-only: never edited or deleted, `Down` never run against a shared database. A mistake is fixed with a new migration.
- **No expand/contract**: a migration does not have to keep the schema usable by the previous version of the app. Breaking changes (drop, rename, type change) go out in a single release with a short downtime. The risky operations are listed in [EFCore](../EFCore.md#changes-that-need-attention) as a reference.
- `/verify-and-commit` enforces it before a push: it blocks if a pushed migration was changed or if the model has pending changes, and lists the risky operations of the new migrations as information.

## Consequences
- Development keeps the comfort of local rollback; the shared history stays linear and trustworthy.
- Breaking schema changes cost one release, at the price of downtime and of a rollback that may need a new migration instead of just the previous image.
- The check is a convenience: a manual `git push` bypasses it. It becomes a real gate only as a required CI check.
- With GitHub Flow (feature branches and pull requests to `main`, planned) the boundary moves from the push to the merge on `main`, and this ADR will be superseded.

## Alternatives considered
- **Forward-only from the first commit**: safest, but a corrective migration for every local experiment, for no benefit on a database nobody else uses.
- **Expand/contract** (every migration compatible with the previous version, breaking changes split over several releases): needed for zero-downtime deployments with overlapping versions or instant image rollback. Too heavy for an app without high-availability requirements; the choice for one that has them.
- **`Down` that throws**: makes rollback impossible everywhere, including the local database where it is useful, and requires editing every generated migration.
