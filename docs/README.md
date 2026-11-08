# Documentation

Start from [ARCHITECTURE](ARCHITECTURE.md): the map of the solution, with links to the rest. The reasons behind each choice are in the [decision records](adr/README.md).

## Topics

| Document | What it covers |
|---|---|
| [ARCHITECTURE](ARCHITECTURE.md) | Layers, request flow, errors, where to validate, known limits |
| [Domain simplifications](DomainSimplifications.md) | What the domain leaves out on purpose |
| [Domain events](DomainEventsHandling.md) | Publishing before the commit, orphans and losses, outbox |
| [EF Core](EFCore.md) | Mapping, SQL Server conventions, migrations and the risky changes |
| [AI](AI.md) | The order summary through `IChatClient`, prompts, configuration, alternatives |
| [Observability](Observability.md) | OpenTelemetry, logs, custom metrics, health checks, alerts |
| [Testing strategy](TestingStrategy.md) | Test projects and SQL Server in tests |
| [CI/CD](CICD.md) | Pipelines, gates, deployment to Azure, migrations in CD |

## Administration

- [CI setup and maintenance](administrators/CISetupAndMaintenance.md): rulesets, repository settings, new repository.
- [CD setup](administrators/CDSetup.md): Azure identities, GitHub environments and secrets, first deployment, teardown.

## Personal notes

- [Clean Architecture by Jason Taylor](personal-notes/AnalysisCleanArchitectureByJasonTaylor.md): my analysis of the reference template and how this project differs.
