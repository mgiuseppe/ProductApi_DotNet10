#!/usr/bin/env bash
#
# One-time CD setup (docs/administrators/CDSetup.md)
#
# 
#
# Usage (Git Bash on Windows, or any bash 4+):
#   gh auth login      # once, as a repository admin
#   az login           # once, as an owner of the subscription
#   ./scripts/cd-setup.sh
#
# This script must be executed manually to:
#   1. Azure subscription: adds the resource providers needed by the Bicep templates
#   2. Azure identities: setup one Entra app registration and service principal per environment
#                        that will be used by GitHub to deploy the Bicep templates through OIDC (no secret).
#   3. GitHub environments: staging and production (production waits for your approval), with their variables
#   4. GitHub secret: the MongoDB admin password, typed at a prompt (Azure SQL has no password, ADR-0018)
#
# Idempotent: every step first checks whether its result already exists, so running the script again
# creates what is missing and changes nothing else. Safe to re-run after a failure halfway.

# ---------------------------------------------------------------------------------------------------------
# Settings
# ---------------------------------------------------------------------------------------------------------

# Stop at the first failing command (-e), treat unset variables as errors (-u), and make a pipeline fail
# when any command in it fails, not only the last one (-o pipefail). Without these, bash happily keeps
# going after an error and the later steps work on empty values.
set -euo pipefail

REPO=mgiuseppe/ProductApi_DotNet10
ENVIRONMENTS=(staging production)

# The GitHub environments and the federated credentials must use the same names: the OIDC token GitHub
# sends to Azure carries "<prefix>:environment:<env>" as subject, and Azure compares it character by character.
# The prefix is "repo:<owner>/<name>", or with immutable subjects (the default for repositories created after
# 15 July 2026) "repo:<owner>@<owner id>/<name>@<repository id>": the script reads it from GitHub.

# ---------------------------------------------------------------------------------------------------------
# Git Bash quirks
# ---------------------------------------------------------------------------------------------------------

# Git Bash (MSYS) rewrites arguments that look like Unix paths into Windows paths before calling a .exe:
# "/subscriptions/123" would reach az as "C:/Program Files/Git/subscriptions/123". This turns it off.
# It has no effect on Linux or macOS.
export MSYS_NO_PATHCONV=1

# Used to remove the "\r" from az output
remove_carriage_return() {
  tr -d '\r'
}
# Small output helpers to make log clear
step() { printf '\n==> %s\n' "$*"; }
info() { printf '    %s\n' "$*"; }

# ---------------------------------------------------------------------------------------------------------
# 0. Preconditions
# ---------------------------------------------------------------------------------------------------------

step "Checking the logins"

# Both CLIs keep their login between sessions; here we only check it, logging in stays your job.
gh auth status >/dev/null 2>&1 || { echo "Not logged in to GitHub: run 'gh auth login' first." >&2; exit 1; }
az account show >/dev/null 2>&1 || { echo "Not logged in to Azure: run 'az login' first." >&2; exit 1; }

SUBSCRIPTION_ID=$(az account show --query id -o tsv | remove_carriage_return)
SUBSCRIPTION_NAME=$(az account show --query name -o tsv | remove_carriage_return)
TENANT_ID=$(az account show --query tenantId -o tsv | remove_carriage_return)
SCOPE=/subscriptions/$SUBSCRIPTION_ID

# Subject prefix of this repository's OIDC tokens, as GitHub issues them (see the settings at the top)
SUBJECT_PREFIX=$(gh api "repos/$REPO/actions/oidc/customization/sub" --jq .sub_claim_prefix)

info "GitHub repository:  $REPO"
info "Azure subscription: $SUBSCRIPTION_NAME ($SUBSCRIPTION_ID)"

# az uses whichever subscription was selected last (az account set). Creating roles in the wrong one is
# easy to do and annoying to undo, so ask before touching anything.
read -r -p "    Configure these? [y/N] " answer
[[ "$answer" == [yY] ]] || { echo "Stopped, nothing changed."; exit 0; }

# ---------------------------------------------------------------------------------------------------------
# 1. Azure resource providers
# ---------------------------------------------------------------------------------------------------------

step "1. Registering the Azure resource providers"

# Each Azure service is a "resource provider" that must be registered on the subscription before its resources can be created.
# Registering is idempotent; it runs in the background and takes a few minutes, we wait for it.
for ns in Microsoft.App Microsoft.DocumentDB Microsoft.KeyVault Microsoft.Sql Microsoft.ManagedIdentity; do
  info registering - "$ns"...
  az provider register --namespace "$ns" --wait >/dev/null
done

# ---------------------------------------------------------------------------------------------------------
# 2. Azure identities for GitHub Actions
# ---------------------------------------------------------------------------------------------------------

# The Bicep template grants the Container App's managed identity the "Key Vault Secrets User" role, so the
# deployment identity must be allowed to create role assignments. "Role Based Access Control Administrator"
# allows that, but on its own it could also grant itself Owner. This ABAC condition narrows it down:
# it may write or delete role assignments only when the role is Key Vault Secrets User (role definition id
# 4633458b-..., the same in every tenant). Read it as:
#   (the action is not "write an assignment" OR the requested role is Key Vault Secrets User)
#   AND (the action is not "delete an assignment" OR the existing role is Key Vault Secrets User)
# The "!" is safe inside double quotes here because a script runs without history expansion; pasted into an
# interactive bash it would be expanded, which is one of the reasons this is a script and not a snippet.
KV_SECRETS_USER=4633458b-17de-408a-b874-0445c86b69e6
CONDITION="((!(ActionMatches{'Microsoft.Authorization/roleAssignments/write'})) OR (@Request[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals {$KV_SECRETS_USER})) AND ((!(ActionMatches{'Microsoft.Authorization/roleAssignments/delete'})) OR (@Resource[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals {$KV_SECRETS_USER}))"

# Grants a role at subscription scope unless the identity already has it. "az role assignment create"
# fails with "RoleAssignmentExists" on a second run, so check with "list" first.
# Extra arguments (the condition) are passed through to "create".
grant_role() {
  local sp_id=$1 role=$2
  shift 2
  local existing
  existing=$(az role assignment list --assignee "$sp_id" --role "$role" --scope "$SCOPE" --query "[0].id" -o tsv | remove_carriage_return)
  if [[ -n "$existing" ]]; then
    info "Role '$role': already assigned"
    return
  fi
  # --assignee-object-id + --assignee-principal-type instead of --assignee: with --assignee, az looks the
  # principal up in the directory, and a service principal created seconds ago is often not visible yet
  # (directory replication), so the call fails. With the object id and the type, no lookup is needed.
  az role assignment create --assignee-object-id "$sp_id" --assignee-principal-type ServicePrincipal \
    --scope "$SCOPE" --role "$role" "$@" >/dev/null
  info "Role '$role': assigned"
}

# The appId of each environment, filled in by the loop below and used again in step 3: this is what
# replaces copying it from the terminal by hand. An associative array (key = environment name) needs
# bash 4 or later: Git Bash and Linux have it, the bash 3.2 that ships with macOS does not.
declare -A CLIENT_IDS

for env in "${ENVIRONMENTS[@]}"; do
  step "2. Azure identity for '$env'"
  display_name="github-productapi-$env"

  # An "app registration" is the definition of the application in Entra ID (the directory); its appId is
  # the AZURE_CLIENT_ID the workflow logs in with. Look it up by name, create it only if missing.
  app_id=$(az ad app list --display-name "$display_name" --query "[0].appId" -o tsv | remove_carriage_return)
  if [[ -z "$app_id" ]]; then
    app_id=$(az ad app create --display-name "$display_name" --query appId -o tsv | remove_carriage_return)
    info "App registration '$display_name': created"
  else
    info "App registration '$display_name': exists"
  fi
  CLIENT_IDS[$env]=$app_id

  # The "service principal" is the local representation of the app in this tenant: roles are assigned
  # to it, by its object id.
  sp_id=$(az ad sp list --filter "appId eq '$app_id'" --query "[0].id" -o tsv | remove_carriage_return)
  if [[ -z "$sp_id" ]]; then
    sp_id=$(az ad sp create --id "$app_id" --query id -o tsv | remove_carriage_return)
    info "Service principal: created"
  else
    info "Service principal: exists"
  fi

  # The federated credential is what makes a secret unnecessary (OIDC): Azure accepts a token signed by
  # GitHub (issuer) for this app only when its subject is exactly this repository and this GitHub environment.
  # A job without "environment: staging" gets a different subject and is refused, which is what keeps the
  # production identity out of reach of any job that has not passed the production approval.
  credential_name="github-$env"
  subject="$SUBJECT_PREFIX:environment:$env"
  credential="{
    \"name\": \"$credential_name\",
    \"issuer\": \"https://token.actions.githubusercontent.com\",
    \"subject\": \"$subject\",
    \"audiences\": [\"api://AzureADTokenExchange\"]
  }"
  existing_subject=$(az ad app federated-credential list --id "$app_id" \
    --query "[?name=='$credential_name'] | [0].subject" -o tsv | remove_carriage_return)
  if [[ -z "$existing_subject" ]]; then
    az ad app federated-credential create --id "$app_id" --parameters "$credential" >/dev/null
    info "Federated credential for $subject: created"
  elif [[ "$existing_subject" != "$subject" ]]; then
    # Created with another subject format (for example before the repository switched to immutable subjects)
    az ad app federated-credential update --id "$app_id" --federated-credential-id "$credential_name" \
      --parameters "$credential" >/dev/null
    info "Federated credential for $subject: updated (was $existing_subject)"
  else
    info "Federated credential for $subject: exists"
  fi

  # Roles at subscription scope, because the deployment stacks create their own resource groups:
  # - Contributor: create and delete resources
  # - Azure Deployment Stack Owner: manage deployment stacks, including their deny settings
  # - Role Based Access Control Administrator, with the condition above: only Key Vault Secrets User
  # Staging and production get the same roles on the same subscription: keeping them apart is a convention
  # of the workflows, not something Azure enforces.
  grant_role "$sp_id" Contributor
  grant_role "$sp_id" "Azure Deployment Stack Owner"
  grant_role "$sp_id" "Role Based Access Control Administrator" --condition "$CONDITION" --condition-version 2.0
done

# ---------------------------------------------------------------------------------------------------------
# 3. GitHub environments and variables
# ---------------------------------------------------------------------------------------------------------

step "3. GitHub environments and variables"

# PUT creates the environment or updates it, so it is idempotent by nature.
gh api --method PUT "repos/$REPO/environments/staging" >/dev/null
info "Environment 'staging'"

# Production requires a reviewer: a job with "environment: production" waits until you approve it in the
# Actions UI. The reviewer is you, identified by your numeric GitHub user id.
my_user_id=$(gh api user --jq .id)
echo "{\"reviewers\":[{\"type\":\"User\",\"id\":$my_user_id}]}" \
  | gh api --method PUT "repos/$REPO/environments/production" --input - >/dev/null
info "Environment 'production' (waits for your approval)"

# Variables, not secrets: these ids say who logs in, but they are useless without a token GitHub signs
# for this repository. Keeping them as variables makes them readable in the logs, which helps debugging.
# They are per environment, so each job gets the identity of the environment it runs in.
for env in "${ENVIRONMENTS[@]}"; do
  gh variable set AZURE_CLIENT_ID --env "$env" --repo "$REPO" --body "${CLIENT_IDS[$env]}"
  gh variable set AZURE_TENANT_ID --env "$env" --repo "$REPO" --body "$TENANT_ID"
  gh variable set AZURE_SUBSCRIPTION_ID --env "$env" --repo "$REPO" --body "$SUBSCRIPTION_ID"
  info "Variables of '$env': AZURE_CLIENT_ID=${CLIENT_IDS[$env]}"
done

# ---------------------------------------------------------------------------------------------------------
# 4. GitHub secret
# ---------------------------------------------------------------------------------------------------------

step "4. MongoDB admin password"

# Repository secret, shared by both environments (one MongoDB cluster, ADR-0015). Never passed on the command
# line (it would end up in the shell history and in the process list): without --body, gh asks for the value
# with a hidden prompt. An existing secret is left alone, because GitHub never shows a secret's value back
# and a re-run would otherwise force you to type it again.
# No ; ' " characters: the password ends up inside the connection string.
secret=MONGO_ADMIN_PASSWORD
if gh secret list --repo "$REPO" --json name --jq '.[].name' | grep -qx "$secret"; then
  info "$secret: already set (to change it: gh secret set $secret --repo $REPO)"
else
  info "$secret:"
  gh secret set "$secret" --repo "$REPO"
fi

# ---------------------------------------------------------------------------------------------------------
# Done
# ---------------------------------------------------------------------------------------------------------

step "Done. Next: the first deployment, section 2 of docs/administrators/CDSetup.md"
