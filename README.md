# Product API

This solution contains a product / catalog manager sample api implemented to refresh .NET knowledge.

## How To Run

1. Apply migrations
    ```bash
    dotnet ef database update --project src/Infrastructure --startup-project src/Api
    ```
2. Run src/Api

## Clean Architecture

This solution will follow clean architectures rules:

- Api (Presenter)
    - Contains minimal API to handle auth, command ingestion, host, http endpoints 
- Infrastructure
    - Contains EF with repositories, Adapters to the external world (DbContext, Repositories) 
- Application
    - Contains Services needed to coordinate use cases (application business rules)(load from repos, call domain methods, save)
- Domain
    - Contains Entities with invariants and factory methods, needed to enforce enterprise business rules, these won't be anemic models
- Tests
    - Different folder containing unit tests and integration tests

## EF Core

- ORM used to map db tables to C# Entities.
- We will use migrations with a forward-only pattern.
- We will use IReadOnlyList and not IEnumerable to avoid a lazy query execution that would throw an ObjectDisposedException 
- We will use classes implementing IEntityTypeConfiguration inside the repository to map EF against the entities withouth writing EF-related code inside Domain.
- a DBContext is not ThreadSafe and is designed to be used a single Unit Of Work (Scoped lifetime is the way, Also Repositories should be Scoped)
- Repositories will be implemented as collections and UoW will be in DbContext  

```bash
dotnet ef migrations add InitialCreate --project src/Infrastructure --startup-project src/Api
dotnet ef database update --project src/Infrastructure --startup-project src/Api
```

# Domain
Entities will follow best practices to ensure rules:
- private empty constructor to let EF rehydrate the entity (no validation on rehydration)
- static factory method to create a valid entity 
- private setters to avoid rule breaking
- invariants to change state and enforce rules

# Tests

- One test class per tested class 
- Follows Method_Condition_Result naming convention (e.g. Create_WithEmptyName_Throws)
- Follows AAA (Arrange, Act, Assert) pattern to implements tests
- Not using red green refactor

# Tools and vscodes plugins

vs codes plugins:
- C# Dev Kit - Adds .net related right clicks options to the folder explorer
- REST Client - lets you use .http files to test api requests

ClaudeCode:
- Brainstorming
- Writing tests / some of the code