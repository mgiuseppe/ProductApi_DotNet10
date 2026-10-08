# Clean Architecture

This solution will follow clean architectures rules:

- Api (Presenter)
    - Contains minimal API to handle auth, command ingestion, host, http endpoints 
- Infrastructure
    - Contains EF with repositories, Adapters to the external world (DbContext, Repositories) 
- Application (Use cases)
    - Contains Services needed to coordinate use cases (application business rules)(load from repos, call domain methods, save)
- Domain (Entities)
    - Contains Entities with invariants and factory methods, needed to enforce enterprise business rules, these won't be anemic models
- Tests
    - Different folder containing unit tests and integration tests

## Domain
Entities (Aggregates) are needed to ensure invariants on creation / editing.
They will follow best practices to enforce business rules (invariants):
- private empty constructor to let EF rehydrate the entity (no validation on rehydration)
- static factory method to create a valid entity 
- private setters to avoid rule breaking
- Method to change state while enforcing rules
- Edits on subaggregates are proxied through the parent aggregate to ensure that the parent can enforce its own invariants (e.g. you cannot modify order lines when the order is shipped). If you really need to edit the subaggregate directly, it likely means the aggregate boundaries are wrong and the subaggregate doesn't belong to the aggregate and should be modeled as its own aggregate (e.g. shipment).

Invariant: a business rule that must be always true! You can ensure invariants by letting the aggregates state be changed only using their own methods and doing all the validation inside those methods.  

## Application
Services (Or command handlers) will coordinate use cases logic:
- Note: Here you coordinate use cases! Business Rules live in Domain.
    - retrieve tracked objects
    - applying edits through domain's invariants
    - committing uow
- DTO, Commands, Responses live here because they are the contracts Api consumes and keeps the dependency direction Api -> Application -> Domain.

## Api
Api will do basic http tasks:
- map endpoints
- call services (or command handlers)
- handle http statuses

## Infrastructure
Here we have repositories used to persist and retrieve aggregates to modify.
Here we have also have adapters to communicate with external resources (e.g. an LLM adapter, just the adapter part like retries, not the business logic).

## Tests

- One test class per tested class 
- Follows Method_Condition_Result naming convention (e.g. Create_WithEmptyName_Throws)
- Follows AAA (Arrange, Act, Assert) pattern to implements tests
- Not using red green refactor
- Used MOQ to mock dependencies