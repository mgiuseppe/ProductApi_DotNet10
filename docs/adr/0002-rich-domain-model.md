# 0002. Rich domain model with aggregates

- Status: Accepted
- Date: 2026-09-29

## Context
Orders have rules that must always hold: no edits once shipped or canceled, no shipping without lines, quantities above zero, one voucher per order. If any code can set properties, every caller has to remember the rules.

## Decision
`Product`, `Order` and `Voucher` are aggregate roots. They have private setters, a private parameterless constructor for EF rehydration, a static `Create` factory that returns a valid instance and methods (`AddLine`, `Confirm`, `Ship`, `Cancel`, `Redeem`) that validate and throw `DomainException`. `OrderLine` is changed only through `Order`. Rules spanning two aggregates live in a domain service (`VoucherRedemptionService`). Ids are generated in the domain.

## Consequences
- Invariants are enforced in one place and covered by fast unit tests.
- Handlers become thin coordinators.
- EF mapping needs Fluent API configuration for private fields and backing collections.
- Order lines snapshot product name and price and reference products by id without a foreign key (see [DomainSimplifications](../DomainSimplifications.md)).

## Alternatives considered
- **Anemic entities with validation in services**: simpler mapping, but rules spread across handlers and are easy to bypass.
