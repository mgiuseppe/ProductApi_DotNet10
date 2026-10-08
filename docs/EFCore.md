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
- SQLite stores `decimal` as text: check constraints on amounts are unreliable, and filtering or ordering by amount is not supported. To be revisited with Postgres.
- Ids are generated in the domain (`ValueGeneratedNever`).