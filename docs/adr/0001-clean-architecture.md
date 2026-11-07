# 0001. Clean Architecture with four projects

- Status: Accepted
- Date: 2026-11-07

## Context
Business rules must be testable without a database or HTTP, and infrastructure (EF Core, MongoDB, the LLM provider) must be replaceable without touching use cases.

## Decision
Four projects, dependencies pointing inwards: `Api -> Application -> Domain`, `Infrastructure -> Application`.
- Domain: entities and invariants.
- Application: use cases, their contracts and the ports they need.
- Infrastructure: implements the ports.
- Api: maps HTTP to use cases.

## Consequences
- Domain and Application are unit-testable without infrastructure.
- More projects and mapping than an API of this size needs.
- Two exceptions: Application references the EF Core package for `IQueryContext` ([0003](0003-cqrs-with-mediatr.md)), Domain references `MediatR.Contracts` ([0005](0005-domain-events-before-commit.md)).

## Alternatives considered
- **One project with folders**: less ceremony, but the compiler does not enforce the dependency rules.
- **Vertical slices**: good for many independent features; here three aggregates share rules.
