# One-time CI/CD setup

Steps done once by hand, by a repository and Azure subscription admin. Everything else is in the workflows (`.github/workflows/`) and in Bicep (`infra/`); the decisions behind them are in [CICD](docs/CICD.md) and [ADR-0015](docs/adr/0015-azure-infrastructure.md).

Tools: GitHub CLI (`gh auth login`, check with `gh auth status`) and Azure CLI (`az login`).

## 1. GitHub repository

The protection of `main` is versioned in `.github/rulesets/main.json`. GitHub does not read that file by itself: apply it once.

```bash
gh api --method POST repos/mgiuseppe/ProductApi_DotNet10/rulesets --input .github/rulesets/main.json
gh repo edit mgiuseppe/ProductApi_DotNet10 --enable-squash-merge --enable-merge-commit=false --enable-rebase-merge=false --delete-branch-on-merge
```

To change the protection, edit the file and update the existing ruleset: find its id with `gh api repos/mgiuseppe/ProductApi_DotNet10/rulesets`, then `gh api --method PUT repos/mgiuseppe/ProductApi_DotNet10/rulesets/<id> --input .github/rulesets/main.json`. Changes made in the GitHub UI are not written back to the file.

## 2. Azure subscription

Register the resource providers the templates use (once per subscription):

```bash
for ns in Microsoft.App Microsoft.DocumentDB Microsoft.KeyVault Microsoft.Sql Microsoft.ManagedIdentity; do
  az provider register --namespace "$ns"
done
```

Create one identity per environment for GitHub Actions, trusted only for that GitHub environment (OIDC, no secret):

```bash
REPO=mgiuseppe/ProductApi_DotNet10
SUBSCRIPTION=$(az account show --query id -o tsv)
SCOPE=/subscriptions/$SUBSCRIPTION
# Lets the identity create and delete only "Key Vault Secrets User" assignments (the one the template creates).
# The role id is written out: a "!" inside double quotes would trigger history expansion in interactive bash.
CONDITION='((!(ActionMatches{'"'"'Microsoft.Authorization/roleAssignments/write'"'"'})) OR (@Request[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals {4633458b-17de-408a-b874-0445c86b69e6})) AND ((!(ActionMatches{'"'"'Microsoft.Authorization/roleAssignments/delete'"'"'})) OR (@Resource[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals {4633458b-17de-408a-b874-0445c86b69e6}))'

for env in staging production; do
  appId=$(az ad app create --display-name "github-productapi-$env" --query appId -o tsv)
  # Object id of the service principal: assigning roles by appId right after creation often fails (directory replication)
  spId=$(az ad sp create --id "$appId" --query id -o tsv)
  az ad app federated-credential create --id "$appId" --parameters "{
    \"name\": \"github-$env\",
    \"issuer\": \"https://token.actions.githubusercontent.com\",
    \"subject\": \"repo:$REPO:environment:$env\",
    \"audiences\": [\"api://AzureADTokenExchange\"]
  }"
  sp=(--assignee-object-id "$spId" --assignee-principal-type ServicePrincipal --scope "$SCOPE")
  az role assignment create "${sp[@]}" --role Contributor
  az role assignment create "${sp[@]}" --role "Azure Deployment Stack Owner"
  az role assignment create "${sp[@]}" --role "Role Based Access Control Administrator" \
    --condition "$CONDITION" --condition-version 2.0
  echo "$env: AZURE_CLIENT_ID=$appId"
done
```

The roles are at subscription scope because the stacks create their resource groups: the separation between the staging and production identities is a convention of the workflows, not enforced by Azure.

Optional but recommended: a budget on the subscription with alerts (Cost Management, for example 1 EUR with alerts at 50% and 100%). Budgets only warn, they never stop spending.

## 3. GitHub environments, variables and secrets

```bash
REPO=mgiuseppe/ProductApi_DotNet10

# Environments; production waits for your approval
gh api --method PUT "repos/$REPO/environments/staging"
echo '{"reviewers":[{"type":"User","id":'"$(gh api user --jq .id)"'}]}' \
  | gh api --method PUT "repos/$REPO/environments/production" --input -

# Per environment: the identity created above (not secret)
for env in staging production; do
  gh variable set AZURE_CLIENT_ID --env "$env" --repo "$REPO" --body "<appId of $env>"
  gh variable set AZURE_TENANT_ID --env "$env" --repo "$REPO" --body "$(az account show --query tenantId -o tsv)"
  gh variable set AZURE_SUBSCRIPTION_ID --env "$env" --repo "$REPO" --body "$(az account show --query id -o tsv)"
done

# Passwords (prompted, never typed on the command line): SQL Server complexity rules, and no ; ' " characters,
# because they end up inside connection strings
gh secret set SQL_ADMIN_PASSWORD --repo "$REPO"
gh secret set MONGO_ADMIN_PASSWORD --repo "$REPO"
```

## 4. First deployment

1. Turn CD on: `gh variable set CD_ENABLED --repo mgiuseppe/ProductApi_DotNet10 --body true`.
2. Run CD with **images only** (Actions → CD → Run workflow → "Only build and push the images"), so the GHCR packages exist.
3. Make the packages `productapi-api` and `productapi-migrator` **public** (GitHub → your profile → Packages → package → Package settings → Change visibility): Container Apps pull them without credentials. GitHub has no API for this.
4. Run CD again without the option: shared stack, staging, then production after your approval. The smoke test prints the API URL.
5. Check once that each environment uses its own MongoDB database: the connection string in the Key Vault must end with `/productapi-staging?...` (or `-production`). Bicep builds it from Azure's template; if that format changes, the database name falls back to `productapi`, shared by both.

To delete everything: Actions → Teardown → `everything` (type it twice).

## 5. After the first green CI run

Add the CI jobs (`build-and-test`, `images`, `bicep`) as required status checks in `.github/rulesets/main.json`, update the ruleset (step 1) and enable auto-merge on the repository (`gh repo edit --enable-auto-merge`).
