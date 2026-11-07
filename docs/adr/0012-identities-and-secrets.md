# 0012. Managed identities for Azure SQL, Key Vault for MongoDB

- Status: Accepted
- Date: 2026-11-07

## Context
The API must not be able to change the schema, and passwords should not exist where Azure can avoid them. The free DocumentDB tier has no Entra authentication, so MongoDB needs a password.

## Decision
- **Pipeline sign-in with OIDC**: one Entra identity per GitHub environment, no stored Azure secret.
- **Entra-only Azure SQL**: no SQL password; the deployment identity is the server's Entra administrator.
- **One user-assigned identity per workload**: the migrator gets schema rights, the API only read and write.
- **Database users created by the deploy workflow** (`azure/sql-action`, idempotent T-SQL), after Bicep and before the migrator.
- **MongoDB password** in a GitHub secret; Bicep builds the connection string and writes it into one Key Vault per environment, read only by the API identity: the migrator gets a placeholder, because it never uses MongoDB.

## Consequences
- The only secret is the MongoDB password.
- Locally SQL uses a login and password, on Azure a managed identity: different authentication.
- The deployment identity can grant only the roles its ABAC condition allows.

## Alternatives considered
- **SQL login and password**: a password to keep and rotate, and the API could change the schema.
- **One shared identity**: the API keeps the schema rights.
- **Users created by a Bicep deployment script**: extra container and storage on every run.
