// Key Vault of one environment with its connection strings (ADR-0015).
// The Container App and the migrator job read them through their identity, granted Key Vault Secrets User here.
param name string
param location string
param tags object

@description('Principal id of the identity that reads the secrets.')
param readerPrincipalId string

@secure()
param sqlConnectionString string

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
    softDeleteRetentionInDays: 7 // the minimum: teardown purges the vault so the name can be reused
  }
}

resource sqlSecret 'Microsoft.KeyVault/vaults/secrets@2024-11-01' = {
  parent: vault
  name: 'sql-connection-string'
  properties: {
    value: sqlConnectionString
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

resource readerRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: vault
  name: guid(vault.id, readerPrincipalId, keyVaultSecretsUserRoleId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleId)
    principalId: readerPrincipalId
    principalType: 'ServicePrincipal'
  }
}

output name string = vault.name
// Versionless URIs: a new secret version reaches the app on its next revision or restart. Read after the role assignment exists.
output sqlSecretUri string = '${vault.properties.vaultUri}secrets/${sqlSecret.name}'
output mongoSecretUri string = '${vault.properties.vaultUri}secrets/${mongoSecret.name}'
output readerRoleAssignmentId string = readerRole.id
