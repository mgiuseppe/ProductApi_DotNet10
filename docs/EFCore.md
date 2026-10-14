# EF Core

- ORM used to map db tables to C# Entities.
- We will use migrations with a forward-only pattern.
- We will use IReadOnlyList and not IEnumerable to avoid a lazy query execution that would throw an ObjectDisposedException 
- We will use Fluent API (classes implementing IEntityTypeConfiguration inside infrastructure) to map EF against the entities withouth writing EF-related code inside Domain.
- a DBContext is not ThreadSafe and is designed to be used a single Unit Of Work (Scoped lifetime is the way, Also Repositories should be Scoped)
- Repositories will be implemented as collections and UoW will be in DbContext, because when a use case touches multiple repositories, the commit shouuld still be a single operation since no single repository "owns" SaveChanges  

- Generate migration and apply
```bash
dotnet ef migrations add InitialCreate --project src/Infrastructure --startup-project src/Api
dotnet ef database update --project src/Infrastructure --startup-project src/Api
```
- Note: InitialCreate is just the name of the migration, so you can add more migrations by specifying a different name
- Generate next migrations

## Persistence
- Migrations are not forward-only, in production it would be a safe practice.
- Postgres (Npgsql) with snake_case names (`EFCore.NamingConventions`): raw SQL such as check constraints must use the snake_case column names (`discount_amount >= 0`).
- `decimal` is `numeric(18,2)`, `Guid` is `uuid`, `DateTime` is `timestamptz`: Npgsql rejects a non-UTC `DateTime`, so the domain normalizes incoming dates to UTC (`Voucher.Create`). Postgres keeps microseconds, not 100ns ticks.
- Unique violations (SqlState 23505, e.g. the same voucher redeemed by two orders concurrently) become a `ConflictException` (409) in `AppDbContext.SaveChangesAsync`.
- Ids are generated in the domain (`ValueGeneratedNever`).