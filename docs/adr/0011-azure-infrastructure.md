# 0011. Azure infrastructure with Bicep deployment stacks

- Status: Accepted
- Date: 2026-11-07

## Context
The app runs on Azure in `staging` and `production`. Environments are created to verify the pipeline and deleted afterwards, so they must be re-creatable from code. A deployment must never delete resources on its own.

## Decision
- **Hosting**: Azure Container Apps (consumption, scale to zero), Azure SQL serverless, Azure DocumentDB free tier.
- **Bicep** in `infra/`, our own small modules, one parameter file per environment.
- **One deployment stack per environment**, plus `productapi-shared` for the MongoDB cluster, same settings everywhere:
  - `detachAll`: a resource removed from the template stays in Azure;
  - `denyDelete`: stack resources cannot be deleted by hand.
- **Teardown** is a separate manual workflow with a typed confirmation.
- **One free DocumentDB cluster shared by both environments**, one database each (one free cluster per subscription).
- **France Central** for everything: the only European region where this subscription can create Azure SQL and the free DocumentDB tier exists.
- Images on GitHub Container Registry (public, no registry cost).

## Consequences
- Staging runs the same template as production.
- Nothing is deleted by mistake; deleting on purpose means tearing down the environment.
- No isolation between environments on MongoDB, and no MongoDB backups.
- Public network access with the "Allow Azure services" firewall rule, because consumption Container Apps have no fixed outbound IP: any Azure-hosted client can reach SQL and MongoDB, protected by Entra authentication and the MongoDB password.

## Alternatives considered
- **Classic deployments and `az group delete`**: no protection against manual deletion.
- **One paid MongoDB cluster per environment**: isolation, at a monthly cost.
- **Terraform**: needs a state backend; Bicep is native to Azure.
- **App Service**: simpler for a single API, but it does not scale to zero.
