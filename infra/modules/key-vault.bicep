// Key Vault of one environment with the MongoDB connection string (ADR-0012; Azure SQL needs no secret).
// The Container App and the migrator job read it through their identities, granted Key Vault Secrets User here.
param name string
param location string
param tags object

@description('Principal ids of the identities that read the secrets.')
param readerPrincipalIds string[]

@secure()
param mongoConnectionString string

resource vault 'Microsoft.KeyVault/vaults@2024-11-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    tenantId: tenant().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true // access through role assignments, no access policies
    enableSoftDelete: true // always on in Azure; written out so the template states it (PSRule Azure.KeyVault.SoftDelete)
    softDeleteRetentionInDays: 7 // the minimum: teardown purges the vault so the name can be reused
  }
}

resource mongoSecret 'Microsoft.KeyVault/vaults/secrets@2024-11-01' = {
  parent: vault
  name: 'mongo-connection-string'
  properties: {
    value: mongoConnectionString
  }
}

// Built-in role "Key Vault Secrets User": read secret values, nothing else
var keyVaultSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'

resource readerRoles 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for principalId in readerPrincipalIds: {
    scope: vault
    name: guid(vault.id, principalId, keyVaultSecretsUserRoleId)
    properties: {
      roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleId)
      principalId: principalId
      principalType: 'ServicePrincipal'
    }
  }
]

output name string = vault.name
// Versionless URI: a new secret version reaches the app on its next revision or restart.
// Module outputs are available only once the whole module, role assignments included, is deployed.
output mongoSecretUri string = '${vault.properties.vaultUri}secrets/${mongoSecret.name}'
