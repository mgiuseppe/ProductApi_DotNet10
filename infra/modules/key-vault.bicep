// Key Vault of one environment with its connection strings (ADR-0016).
// The Container App and the migrator job read them through their system-assigned identity (Key Vault Secrets User).
param name string
param location string
param tags object

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

output name string = vault.name
output sqlSecretUri string = sqlSecret.properties.secretUri
output mongoSecretUri string = mongoSecret.properties.secretUri
