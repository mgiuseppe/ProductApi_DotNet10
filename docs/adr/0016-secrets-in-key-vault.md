# 0016. Secrets in Key Vault, read through system-assigned identities

- Status: Accepted
- Date: 2026-10-28

## Context
In Azure the API and the migrator need the connection strings of Azure SQL ([0013](0013-sql-server-for-aggregates.md)) and MongoDB ([0017](0017-shared-free-mongodb-cluster.md)), both with a password. The free MongoDB tier has no Entra ID authentication, so a password exists anyway. Azure SQL could use Entra authentication with managed identities, but then the pipeline must create database users for each identity, which needs an Entra admin, a firewall rule for the runner and possibly a tenant-level role for the server: too much machinery for this project.

## Decision
- **SQL authentication with an administrator login and password.** The password is a GitHub secret (`SQL_ADMIN_PASSWORD`), passed to Bicep as a `@secure()` parameter.
- **One Key Vault per environment**, created by Bicep, holding the two connection strings (`sql-connection-string`, `mongo-connection-string`). Bicep writes them from the secure parameters.
- **The Container App and the migrator job have a system-assigned managed identity** with the *Key Vault Secrets User* role on that vault, and read the connection strings as Key Vault secret references. The identity is used only to read secrets, never to authenticate to the databases.
- **Local development and tests do not change**: SQL Server container with a SQL login (Docker Compose, Testcontainers).

## Consequences
- Same pattern in Azure and locally (login and password), so connection problems reproduce the same way.
- No secret in the repository, in parameter files or in the app configuration: they live in GitHub secrets and in Key Vault; compiled Bicep output is gitignored.
- The API and the migrator use the same administrator login: the API could change the schema. Accepted for simplicity; a separate least-privilege user would need a script run against the database.
- Passwords are rotated by hand: change the GitHub secret and redeploy.
- Key Vault is soft-deleted on teardown (7 days, the minimum, without purge protection): recreating an environment within that window reuses the name and would fail, so the vault name includes a unique suffix and teardown purges it.
- The SQL firewall allows Azure services (`0.0.0.0`): any Azure-hosted client, in any tenant, can reach the server, and the password is the protection.

## Alternatives considered
- **Entra-only Azure SQL with managed identities** (the Microsoft recommendation, no SQL password): needs an Entra admin, a pipeline step that creates database users, a temporary firewall rule for the runner and possibly the *Directory Readers* role for the server. MongoDB would keep a password anyway.
- **Connection strings as plain Container App secrets, no Key Vault**: one resource less, but secrets would live in each app definition instead of one place readable only by identities with a role.
