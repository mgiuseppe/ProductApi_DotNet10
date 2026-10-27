# Architecture Decision Records

Each ADR records one architectural decision: the context, what was decided, the consequences accepted and the alternatives rejected. The architecture overview is in [ARCHITECTURE.md](../ARCHITECTURE.md).

ADRs 0001-0009 were written retroactively on 2026-10-27 from the code, the commit history and the topic documents; their date is when the decision landed in the code.

## Rules

- One decision per file, named `NNNN-short-title.md`, numbered in sequence, numbers never reused.
- Status: `Proposed`, `Accepted`, `Superseded by NNNN`, `Deprecated`.
- An accepted ADR is not rewritten. If the decision changes, write a new ADR that supersedes it and update the old status line.
- Write the ADR before or together with the code that implements it, and update `ARCHITECTURE.md`.

## Index

| ADR | Title | Status |
|---|---|---|
| [0001](0001-clean-architecture.md) | Clean Architecture with four projects | Accepted |
| [0002](0002-rich-domain-model.md) | Rich domain model with aggregates | Accepted |
| [0003](0003-cqrs-with-mediatr.md) | CQRS with MediatR | Accepted |
| [0004](0004-repositories-and-unit-of-work.md) | Repositories and unit of work | Accepted |
| [0005](0005-domain-events-before-commit.md) | Domain events dispatched before the commit | Accepted |
| [0006](0006-postgresql-and-mongodb.md) | PostgreSQL for aggregates, MongoDB for order history | Accepted |
| [0007](0007-ichatclient-without-own-port.md) | `IChatClient` without a port of our own | Accepted |
| [0008](0008-integration-tests-with-testcontainers.md) | Integration tests with Testcontainers | Accepted |
| [0009](0009-migrations-with-migrator-container.md) | Migrations applied by a migrator container | Accepted |
| [0010](0010-migrations-editable-until-pushed.md) | Migrations editable until pushed, forward-only after | Accepted |
| [0011](0011-github-flow.md) | GitHub Flow with a protected `main` | Accepted |
| [0012](0012-squash-merge-only.md) | Squash merge only | Accepted |

## Template

```markdown
# NNNN. Title

- Status: Proposed | Accepted | Superseded by NNNN
- Date: YYYY-MM-DD

## Context
The forces at play: requirements, constraints, what happens if nothing is decided.

## Decision
What we do, stated in one or two sentences, then the details that matter.

## Consequences
Positive, negative accepted on purpose, and what changes in the code or in the workflow.

## Alternatives considered
Each option with the reason it was not chosen.
```
