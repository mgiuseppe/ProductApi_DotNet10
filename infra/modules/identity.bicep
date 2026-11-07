// Identity of one workload (the API or the migrator job, ADR-0012): it signs in to Azure SQL as the database user
// created for it by the deploy workflow; the API identity also reads the MongoDB secret from Key Vault.
// User-assigned, because a Container App checks its Key Vault secret references when it is created (a system-assigned
// identity would be born with the app, before it could be granted access to the vault), and because the deploy
// workflow needs both client ids to create the database users before the workloads run.
param name string
param location string
param tags object

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: name
  location: location
  tags: tags
}

output id string = identity.id
output name string = identity.name
output principalId string = identity.properties.principalId
output clientId string = identity.properties.clientId
