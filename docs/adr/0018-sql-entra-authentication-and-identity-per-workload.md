# 0018. Azure SQL with Entra authentication and one identity per workload

- Status: Accepted
- Date: 2026-11-04
- Supersedes: the "Secrets" section of [0015](0015-azure-infrastructure.md) for Azure SQL

## Context
In [0015](0015-azure-infrastructure.md) the API and the migrator share the SQL administrator password: the API can change the schema, and the password must be kept and rotated. Database users are created only with T-SQL, not by Bicep or any other infrastructure tool. MongoDB keeps its password: the free DocumentDB tier has no Entra authentication.

## Decision
- **Entra-only Azure SQL**: the deployment identity of the environment is the server's Entra administrator; no SQL password.
- **Two user-assigned identities per environment**, `id-productapi-api-<env>` and `id-productapi-migrator-<env>`, both reading the MongoDB secret from Key Vault.
- **Database users created by the deploy workflow** with [`azure/sql-action`](https://github.com/Azure/sql-action), after Bicep and before the migrator, from an idempotent T-SQL script: migrator `db_ddladmin`, `db_datareader`, `db_datawriter`; API `db_datareader`, `db_datawriter`.
- **SQL connection string without secret** (`Authentication=Active Directory Managed Identity`), as a plain setting, no longer in Key Vault.

## Consequences
- No SQL password: `SQL_ADMIN_PASSWORD` and its step in `scripts/cd-setup.sh` go away. Each identity has only the rights it needs.
- The deployment identity is database administrator: no new privilege, it is already Contributor on the subscription.
- Local (SQL login) and Azure (managed identity) authenticate differently.
- PSRule: `Azure.SQL.AAD` and `Azure.SQL.AADOnly` no longer excluded; `Azure.Cosmos.MongoEntraID` moves to the cost exclusions.
- Users are created `WITH SID = <client id>, TYPE = E` instead of `FROM EXTERNAL PROVIDER`: no lookup in Entra ID, so the server needs no *Directory Readers* role.
- The runner reaches SQL through the existing "Allow Azure services" rule; if it cannot, `azure/sql-action` adds a rule for its IP and removes it afterwards.

## Alternatives considered
- **Keep the SQL password** (0015), or one login per workload: a password to keep, and the second one still needs T-SQL.
- **One shared identity**: the API keeps the schema rights.
- **Bicep deployment script** (.NET Aspire, Reliable Web App): temporary container and storage on each run, an extra role, and the migrator would be administrator.
- **Users created by an EF migration**: identity names per environment would end up in the migrations.
