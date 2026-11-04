# CD setup

For a repository and Azure subscription admin: the setup done once before the first deployment (the CD workflow logs in to Azure with an identity, and that identity cannot create itself), the first deployment, and how to turn CD off and tear it down. Everything else is in the workflows (`.github/workflows/`) and in Bicep (`infra/`); the decisions behind them are in [CICD](../CICD.md) and [ADR-0015](../adr/0015-azure-infrastructure.md).

## 1. Run the setup script

[scripts/cd-setup.sh](../../scripts/cd-setup.sh) configures, in order:

1. the Azure resource providers the templates use;
2. one Entra identity per environment (`github-productapi-staging`, `github-productapi-production`), trusted by GitHub through OIDC with no secret, with its roles;
3. the GitHub environments `staging` and `production` (production waits for your approval) and their `AZURE_*` variables;
4. the secret `MONGO_ADMIN_PASSWORD`, typed at a hidden prompt. Azure SQL has no password: the deployment identity is its Entra administrator ([ADR-0018](../adr/0018-sql-entra-authentication-and-identity-per-workload.md)).

Each step checks first whether its result already exists: re-running the script after a failure is safe.

Requirements: bash 4 or later (on Windows, Git Bash), the GitHub CLI and the Azure CLI, both logged in.

```bash
gh auth login
az login
./scripts/cd-setup.sh
```

The script shows the GitHub repository and the Azure subscription it will configure and asks before changing anything. If the subscription is wrong, select the right one with `az account set --subscription <name or id>` and run it again.

Optional but recommended: a budget on the subscription with alerts (Cost Management, for example 1 EUR with alerts at 50% and 100%). Budgets only warn, they never stop spending.

## 2. First deployment

1. Turn CD on: `gh variable set CD_ENABLED --repo mgiuseppe/ProductApi_DotNet10 --body true`.
2. Build only the images, so the GHCR packages exist: `gh workflow run cd.yml --repo mgiuseppe/ProductApi_DotNet10 -f images-only=true` (or Actions → CD → Run workflow → "Only build and push the images").
3. Make the packages `productapi-api` and `productapi-migrator` **public** (GitHub → your profile → Packages → package → Package settings → Change visibility): Container Apps pull them without credentials. GitHub has no API for this.
4. Run CD again without the option: shared stack, staging, then production after your approval. The smoke test prints the API URL.
5. Check once that each environment uses its own MongoDB database: the connection string in the Key Vault must end with `/productapi-staging?...` (or `-production`). Bicep builds it from Azure's template; if that format changes, the database name falls back to `productapi`, shared by both.

## 3. Turning off and teardown

- Turn CD off: `gh variable set CD_ENABLED --repo mgiuseppe/ProductApi_DotNet10 --body false`.
- Delete everything: Actions → Teardown → `everything` (type it twice).
