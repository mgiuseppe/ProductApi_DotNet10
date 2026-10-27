# 0001. Clean Architecture with four projects

- Status: Accepted
- Date: 2026-09-28

## Context
The project exists to practise structuring a .NET service that could grow: business rules must stay testable without a database or HTTP, and infrastructure (EF Core, MongoDB, an LLM provider) must be replaceable without touching use cases.

## Decision
Four projects with dependencies pointing inwards: `Api -> Application -> Domain`, `Infrastructure -> Application`. Domain holds entities and invariants, Application holds use cases and the ports they need, Infrastructure implements the ports, Api maps HTTP to use cases. Contracts (commands, queries, responses) live in Application.

## Consequences
- Domain and Application are unit-testable with no infrastructure.
- More projects, files and mapping than a single-project API needs at this size.
- Two pragmatic exceptions, each with its own ADR: Application references the EF Core package for `IQueryContext` ([0003](0003-cqrs-with-mediatr.md)) and Domain references `MediatR.Contracts` ([0005](0005-domain-events-before-commit.md)).

## Alternatives considered
- **Single project with folders**: less ceremony, but dependency rules are not enforced by the compiler.
- **Vertical slices**: good for many independent features; with three aggregates sharing invariants the layered split teaches more. The Jason Taylor template was analysed in [AnalysisTaylorCleanArchitecture](../AnalysisTaylorCleanArchitecture.md).
