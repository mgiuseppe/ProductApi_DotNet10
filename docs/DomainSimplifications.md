# Domain Simplifications
This project is a learning exercise, so several areas are deliberately simplified.

## Order and cart
- `Order.Draft` acts as an implicit cart: lines can be edited freely and their prices follow the current product price. `ConfirmOrderCommandHandler` silently re-syncs stale prices before confirming, rather than rejecting the confirmation. A real e-commerce would model `Cart` as a separate aggregate, create an immutable `Order` at checkout, and show a "price has changed" step to the user before charging.
- One status per order, no per-line status. Partial shipments, returns and partial cancellations are out of scope.
- `Subtotal` and `Total` are computed in memory, not persisted. Lists of orders map entities in memory after `ToListAsync`, which is fine for the few columns involved.

## Products
- Order lines reference products by id, without a foreign key, so products can be deleted without affecting order history (name and price are snapshotted in the line). Existence is enforced at application level by `ProductLookup`.

## Vouchers
- One single-use voucher per order, applied only at confirmation, so a draft never holds a discount to keep in sync.
- `ValidatedVoucher` (internal constructor) is kept to demonstrate the "ticket" technique. An `internal` method on `Order` would give the same guarantee, since the ticket is issued and consumed inside the same domain service call. It does not prevent application code from calling `Voucher.Redeem` without confirming an order.
- Concurrent double redemption is stopped by the unique index and translated to a `ConflictException` (HTTP 409) in `AppDbContext.SaveChangesAsync`. The check is on the Postgres unique violation (SqlState 23505). It treats any unique violation as a voucher conflict, which is only correct while this is the only unique index.
- Time-dependent rules receive `now` from a `TimeProvider`, so expiry can be tested without the system clock.
