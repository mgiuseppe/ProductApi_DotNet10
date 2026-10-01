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


## Domain
Entities (Aggregates) will follow best practices to enforce business rules (invariants):
- private empty constructor to let EF rehydrate the entity (no validation on rehydration)
- static factory method to create a valid entity 
- private setters to avoid rule breaking
- Method to change state while enforcing rules
- Edits on subaggregates are proxied through the parent aggregate to ensure that the parent can enforce its own invariants (e.g. you cannot modify order lines when the order is shipped). If you really need to edit the subaggregate directly, it likely means the aggregate boundaries are wrong and the subaggregate doesn't belong to the aggregate and should be modeled as its own aggregate (e.g. shipment).

Invariant: a business rule that must be always true! You can ensure invariants by letting the aggregates state be changed only using their own methods and doing all the validation inside those methods.  

## Application
Services will coordinate use cases logic:
- Note: Here you coordinate use cases! Business Rules live in Domain.
    - retrieve tracked objects
    - applying edits through domain's invariants
    - committing uow
- DTO, Commands, Responses live here because they are the contracts Api consumes and keeps the dependency direction Api -> Application -> Domain.

## Api
Api will do basic http tasks:
- map endpoints
- call services
- handle http statuses

## Infrastructure
Here we have repositories used to persist and retrieve aggregates to modify.

### EF Core

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

## Tests

- One test class per tested class 
- Follows Method_Condition_Result naming convention (e.g. Create_WithEmptyName_Throws)
- Follows AAA (Arrange, Act, Assert) pattern to implements tests
- Not using red green refactor
- Used MOQ to mock dependencies

## Tools and vscodes plugins

vs codes plugins:
- C# Dev Kit - Adds .net related right clicks options to the folder explorer
- REST Client - lets you use .http files to test api requests

ClaudeCode:
- Brainstorming
- Writing tests / some of the code


TODO: autoformat before commit

## Domain Simplifications

- Order in status Draft acts as an implicit cart: lines can be freely added/updated/removed and prices will be kept in sync with the product catalog. A real e-commerce would likely model Cart as a separate aggregate from Order (immutable once created via checkout). Simplified here to keep the project simple.
- Order.ConfirmAsync re-reads current product prices and silently updates any stale line before confirming, rather than rejecting the confirmation. In a real  e-commerce, the user would typically see a "price has changed" confirmation step before checkout rather than a silent update. Simplified here.