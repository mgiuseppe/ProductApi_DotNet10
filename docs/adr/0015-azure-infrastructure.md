# 0015. Azure infrastructure: Bicep stacks, secrets in Key Vault, shared MongoDB cluster

- Status: Accepted
- Date: 2026-10-28

## Context
The application is deployed to Azure ([CICD](../CICD.md)) with a `staging` and a `production` environment. Environments are created to verify the pipeline and deleted afterwards, so they must be re-creatable from code, and deleting one must be a single, deliberate step. A deployment must never delete resources on its own: removing a database by mistake is the failure to avoid. The pipeline must be one: environments may differ only in parameters and in the manual approval for production.

The app needs Azure SQL ([0013](0013-sql-server-for-aggregates.md)) and MongoDB for the order history. MongoDB runs on Azure DocumentDB, whose free tier gives **one** 32 GB cluster per subscription, only in a few regions (in Europe: France Central, Germany North, North Europe, Norway East, Switzerland North), without Entra ID authentication, backups or SLA, paused after 60 days without use.

## Decision

### Bicep and deployment stacks
- **Bicep in `infra/`**: `main.bicep` (one environment) and `shared.bicep` (resources shared by the environments) at **subscription scope**, each creating its resource group; one module per responsibility in `infra/modules/`; one `.bicepparam` file per environment. Modules are our own, small and readable.
- **One deployment stack per environment** (`productapi-staging`, `productapi-production`) plus `productapi-shared`, created and updated with `az stack sub create` and the **same settings everywhere**:
  - `--action-on-unmanage detachAll`: a resource removed from the template is detached and stays in Azure. A deployment never deletes anything.
  - `--deny-settings-mode denyDelete`: resources managed by a stack cannot be deleted by hand.
- **Teardown is a separate, explicit operation**: `az stack sub delete --action-on-unmanage deleteAll`, by hand or with a manual workflow with a confirmation input. Tearing down an environment leaves the shared stack alone; "tear down everything" deletes it last.
- **Conventions**: Cloud Adoption Framework abbreviations for names (`rg-`, `cae-`, `ca-`, `sql-`, `kv-`) plus `uniqueString()` where a name must be globally unique; tags `app`, `environment`, `managedBy`; linter rules in `infra/bicepconfig.json`, the important ones as errors.
- **Pipeline**: the pull request lints the Bicep files and shows a preview; a merge on `main` deploys the shared stack (idempotent), then staging, then production after manual approval. It signs in with OIDC, one identity per environment.

### Secrets
- **SQL authentication with an administrator login and password**, and the MongoDB administrator password: GitHub secrets (`SQL_ADMIN_PASSWORD`, `MONGO_ADMIN_PASSWORD`), passed to Bicep as `@secure()` parameters.
- **One Key Vault per environment** holding the two connection strings, written by Bicep. The Container App and the migrator job have a **system-assigned identity** with *Key Vault Secrets User* on that vault and read them as Key Vault secret references. The identity only reads secrets; it never authenticates to the databases.

### Shared MongoDB cluster
- **One free DocumentDB cluster shared by staging and production**, each with its own database (`productapi-staging`, `productapi-production`), in the shared stack, only to avoid paying for a second cluster.
- **North Europe for everything**: the closest free-tier region with every service we use (West Europe and Italy North have no DocumentDB free tier).

## Consequences
- The same template, commands and stack settings run in every environment: staging verifies what production will do.
- Resources cannot be deleted by mistake, by a deployment or by hand. Removing one on purpose means tearing down the environment, or updating the stack once with `deleteResources`. The automatic cleanup of resources removed from the template is given up on purpose.
- No secret in the repository, in parameter files or in the app configuration; compiled Bicep output is gitignored. Passwords are rotated by hand: change the GitHub secret and redeploy.
- Login and password in Azure as locally, so connection problems reproduce the same way. The API and the migrator share the SQL administrator login, so the API could change the schema.
- No isolation between environments on MongoDB: same cluster and credentials, so a staging run can read or damage production history. No MongoDB backups, and the cluster must be resumed by hand after 60 days without use.
- Firewalls allow Azure services (`0.0.0.0`), because consumption Container Apps have no fixed outbound IP: any Azure-hosted client, in any tenant, can reach SQL and MongoDB, and the passwords are the protection.
- Key Vault is soft-deleted on teardown (7 days, without purge protection): teardown purges it, otherwise recreating the environment within a week fails on the name.
- The pipeline identities need subscription-scope rights to create resource groups and stacks, plus *Azure Deployment Stack Owner* for the deny settings and the right to create the Key Vault role assignments.
- To verify while writing the pipeline: how far `what-if` covers stacks (otherwise `az deployment sub what-if` on the same template).

## Alternatives considered
- **Classic deployments and `az group delete`**: simplest and familiar; no protection against deleting resources by hand unless a lock is added and removed before every teardown.
- **Stacks with `deleteAll` on update**, or different settings per environment: a wrong change to the template could delete a database, or staging would not verify what production does.
- **Entra-only Azure SQL with managed identities** (Microsoft's recommendation, no SQL password): needs an Entra admin, a pipeline step that creates database users, a temporary firewall rule for the runner and possibly the *Directory Readers* role; MongoDB would keep a password anyway.
- **Connection strings as plain Container App secrets, no Key Vault**: one resource less, secrets spread over each app definition.
- **One paid MongoDB cluster per environment**: isolation and Entra ID, at a monthly cost. **Atlas M0 for staging**: two providers for the same service.
- **Azure Verified Modules** (`br/public:avm/...`): the usual choice in enterprise setups; too large for this handful of resources. **Terraform**: needs a state backend; Bicep is native to Azure. **A separate infrastructure repository**: useful when several applications share a platform.
