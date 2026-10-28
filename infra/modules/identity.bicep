// Identity of the Container App and the migrator job: it only reads the Key Vault secrets (ADR-0015).
// User-assigned, because a Container App checks its Key Vault secret references when it is created: a system-assigned
// identity would be born with the app, before it could be granted access to the vault.
param name string
param location string
param tags object

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: name
  location: location
  tags: tags
}

output id string = identity.id
output principalId string = identity.properties.principalId
