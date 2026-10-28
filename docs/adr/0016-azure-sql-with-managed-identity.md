# 0016. Azure SQL authentication with managed identities

- Status: Accepted
- Date: 2026-10-28

## Context
In Azure the API and the migrator connect to Azure SQL ([0013](0013-sql-server-for-aggregates.md)). A SQL login means a password to create, store, rotate and keep out of logs. Microsoft recommends Microsoft Entra authentication with managed identities: the identity is managed by Azure and no credential exists in the code, the pipeline or the configuration. Infrastructure is deployed with Bicep ([0015](0015-bicep-with-deployment-stacks.md)), and the pipeline signs in to Azure with OIDC.

## Decision
- **Entra-only authentication** on the Azure SQL server: SQL logins are disabled, so no password can exist.
- **System-assigned managed identities**: Azure creates them with the Container App and the migrator job and deletes them with them; no identity resource to declare.
  - The API: `db_datareader` and `db_datawriter`.
  - The migrator job: also `db_ddladmin`, to apply migrations.
- **The pipeline identity of the environment is the Entra admin of the server.** After the infrastructure deployment, a pipeline step signs in with that identity (`go-sqlcmd` with Azure CLI authentication) and runs an idempotent script that creates the two database users (`CREATE USER [<resource name>] FROM EXTERNAL PROVIDER`) and grants the roles above. If a user exists but belongs to an identity that no longer exists (the app or the job was recreated, same name, new identity), the script drops and recreates it. Database users depend on the environment, so they are not created by EF migrations.
- **Order on the first deployment**: Bicep creates the database, the app and the job (their identities are born with them); the pipeline creates the database users; the migrator runs; the app becomes ready. Until then the app runs but `/health` reports it not ready, so it gets no traffic, and it recovers by itself without a restart: EF opens connections only on the first query.
- **Connection string** in Azure: `Authentication=Active Directory Managed Identity` (no `User Id` with a system-assigned identity), set as an environment variable by Bicep. `Microsoft.Data.SqlClient` supports it natively: no code change.
- **Local development and tests do not change**: SQL Server container with a SQL login (Docker Compose, Testcontainers).

## Consequences
- No database password in Azure, in GitHub or in the repository; nothing to rotate.
- Least privilege: the API cannot change the schema, only the migrator can, and only the pipeline is admin.
- Two authentication paths: managed identity in Azure, SQL login locally. A connection problem in Azure cannot be reproduced locally with the same credentials.
- The pipeline must reach the server to create the users: GitHub-hosted runners have changing IP addresses, so the step adds a firewall rule for the runner IP and removes it at the end (also on failure).
- The pipeline identity is database admin: who can run the production workflow can do anything on the production database. Mitigated by the manual approval of the `production` environment.
- `Microsoft.Data.SqlClient` brings `Azure.Identity` (already a dependency today).
- To create users for other identities, the SQL server resolves them in Entra ID: the server gets a system-assigned identity, which may need the *Directory Readers* role (granted once per tenant, by a tenant admin). To verify on the first deployment whether `CREATE USER ... WITH OBJECT_ID` makes it unnecessary.
- The firewall allows Azure services (`0.0.0.0`): any Azure-hosted client, in any tenant, can reach the server. Entra-only authentication is what protects it.

## Alternatives considered
- **SQL login with the password in a secret store** (Key Vault or Container Apps secrets): familiar, but a credential to protect and rotate, and SQL authentication stays enabled.
- **One identity for the API and the migrator**: simpler, but the API could change the schema.
- **User-assigned identities**: separate resources, so permissions can be granted before the app and the job exist and survive if they are recreated; two more resources and the client id in the connection string, for an ordering problem that here solves itself.
- **The migrator identity as Entra admin, creating the API user itself**: no firewall rule for the runner, but the migrator (an EF bundle) would need extra code to run environment-specific SQL, and it would be admin.
