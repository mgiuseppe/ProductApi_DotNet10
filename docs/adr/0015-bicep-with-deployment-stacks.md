# 0015. Infrastructure as code with Bicep and deployment stacks

- Status: Accepted
- Date: 2026-10-28

## Context
The application is deployed to Azure ([CICD](../CICD.md)) with a `staging` and a `production` environment. Environments are created to verify the pipeline and deleted afterwards, so they must be re-creatable from code, and deleting one must be a single, deliberate step. A deployment must never delete resources on its own: removing a database by mistake is the failure to avoid. The pipeline must be one: environments may differ only in parameters and in the manual approval for production, never in the deployment mechanism.

## Decision
- **Bicep in `infra/`** in this repository: `infra/main.bicep` at **subscription scope** creates the resource group and calls one module per responsibility in `infra/modules/`; one `.bicepparam` file per environment. Modules are our own, small and readable.
- **One deployment stack per environment** (`productapi-staging`, `productapi-production`), created and updated with `az stack sub create`, with the **same settings everywhere**:
  - `--action-on-unmanage detachAll`: a resource removed from the template is detached and stays in Azure. A deployment never deletes anything.
  - `--deny-settings-mode denyDelete`: resources managed by the stack cannot be deleted by hand, from the portal or the CLI.
- **Teardown is a separate, explicit operation**: `az stack sub delete --action-on-unmanage deleteAll`, run by hand or by a manual workflow (`workflow_dispatch`) with a confirmation input. It is the only operation that deletes resources.
- **Conventions**: Cloud Adoption Framework abbreviations for resource names (`rg-`, `cae-`, `ca-`, `sql-`, `log-`) plus `uniqueString()` where a name must be globally unique; tags `app`, `environment`, `managedBy` on every resource; linter rules in `infra/bicepconfig.json`, the important ones as errors.
- **Pipeline**: the pull request builds and lints the Bicep files and shows a preview of the changes; a merge on `main` deploys staging, then production after manual approval, with the same workflow. The pipeline signs in with OIDC, one identity per environment ([CICD](../CICD.md#4-environments-identity-and-secrets)).

## Consequences
- The same template, the same commands and the same stack settings run in every environment: staging verifies exactly what production will do.
- Resources cannot be deleted by mistake, by a deployment or by hand. Removing a resource on purpose means deleting the environment, or updating the stack once with `deleteResources` as a deliberate step.
- The automatic cleanup of resources removed from the template, the main feature of stacks, is given up on purpose.
- The stack lists exactly which resources belong to an environment, and teardown removes exactly those, resource group included.
- The pipeline identity needs subscription-scope rights to create resource groups and stacks, plus the right to manage the stack deny settings (built-in role *Azure Deployment Stack Owner*), and to create the role assignments the template contains. To be narrowed and verified when the identities are set up.
- To verify while writing the pipeline: how far `what-if` covers stacks (otherwise the preview uses `az deployment sub what-if` on the same template), and that teardown works with `denyDelete` in place.
- Some resources are only soft-deleted (Key Vault, Log Analytics): recreating an environment right after a teardown can hit name conflicts. Names and purge settings must account for it.

## Alternatives considered
- **Classic deployment (`az deployment sub create`) and `az group delete`**: the simplest and most familiar; no protection against deleting resources by hand unless a lock is added, which then has to be removed before every teardown.
- **Stack with `deleteAll` on update**: resources removed from the template are deleted automatically; rejected, a wrong change to the template could delete a database.
- **Different settings per environment** (automatic cleanup in staging, protection in production): staging would no longer verify what production does.
- **Azure Verified Modules** (`br/public:avm/...`): Microsoft-maintained modules, the usual choice in enterprise setups; too large for three or four resources, and writing small modules teaches more. Worth revisiting if the infrastructure grows.
- **Terraform**: multi-cloud and widely used, but needs a state backend; Bicep is native to Azure and has no state to manage.
- **Bicep in a separate repository**: useful when several applications share a platform; here infrastructure and application change together.
