# Architecture Decision Records

One file per architectural decision: context, decision, consequences, alternatives. The overview is in [ARCHITECTURE.md](../ARCHITECTURE.md).

This set is a baseline written on 2026-11-07: it describes the current choices only. The earlier history (PostgreSQL, North Europe, SQL passwords...) is in the git tag `v0-adr-history`.

## Rules

- One decision per file, `NNNN-short-title.md`, numbers never reused.
- Status: `Proposed`, `Accepted`, `Superseded by NNNN`.
- An accepted ADR is not rewritten: a new ADR supersedes it.
- Written before or with the code, together with `ARCHITECTURE.md`.
- Only for decisions that change the structure or are expensive to reverse. Workflow rules and details go in the topic documents.

## Index

| ADR | Title |
|---|---|
| [0001](0001-clean-architecture.md) | Clean Architecture with four projects |
| [0002](0002-rich-domain-model.md) | Rich domain model with aggregates |
| [0003](0003-cqrs-with-mediatr.md) | CQRS with MediatR |
| [0004](0004-repositories-and-unit-of-work.md) | Repositories and unit of work |
| [0005](0005-domain-events-before-commit.md) | Domain events dispatched before the commit |
| [0006](0006-sql-server-and-mongodb.md) | SQL Server for aggregates, MongoDB for order history |
| [0007](0007-ichatclient-without-own-port.md) | `IChatClient` without a port of our own |
| [0008](0008-migrations-with-migrator.md) | Migrations applied by a migrator |
| [0009](0009-migrations-forward-only-after-merge.md) | Migrations editable until merged, forward-only after |
| [0010](0010-integration-tests-with-testcontainers.md) | Integration tests on SQL Server with Testcontainers |
| [0011](0011-azure-infrastructure.md) | Azure infrastructure with Bicep deployment stacks |
| [0012](0012-identities-and-secrets.md) | Managed identities for Azure SQL, Key Vault for MongoDB |
| [0013](0013-application-insights.md) | Application Insights with the Azure Monitor OpenTelemetry distro |

## Template

```markdown
# NNNN. Title

- Status: Proposed | Accepted | Superseded by NNNN
- Date: YYYY-MM-DD

## Context
## Decision
## Consequences
## Alternatives considered
```
