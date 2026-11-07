# 0002. Rich domain model with aggregates

- Status: Accepted
- Date: 2026-11-07

## Context
Orders have rules that must always hold: no edits after confirmation, no shipping without lines, one voucher per order. If any code can set properties, every caller must remember them.

## Decision
- `Product`, `Order` and `Voucher` are aggregate roots: private setters, a static `Create` factory, methods (`AddLine`, `Confirm`, `Ship`, `Cancel`, `Redeem`) that enforce the rules and throw `DomainException`.
- `OrderLine` changes only through `Order`.
- A rule spanning two aggregates lives in a domain service (`VoucherRedemptionService`).
- Ids are generated in the domain.

## Consequences
- Rules live in one place, covered by fast unit tests; handlers only coordinate.
- EF needs Fluent API mapping for private fields and collections.
- Order lines copy product name and price and reference the product by id, without a foreign key.

## Alternatives considered
- **Anemic entities, rules in services**: simpler mapping, but rules spread across handlers and are easy to bypass.
