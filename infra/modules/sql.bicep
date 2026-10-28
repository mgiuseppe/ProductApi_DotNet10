// Azure SQL with Entra-only authentication and the free offer (ADR-0013, ADR-0016).
param serverName string
param databaseName string
param location string
param tags object

@description('Display name of the Entra admin (the pipeline identity of the environment).')
param adminLogin string

@description('Object id of the Entra admin.')
param adminObjectId string

resource server 'Microsoft.Sql/servers@2025-01-01' = {
  name: serverName
  location: location
  tags: tags
  // Lets the server look up Entra identities when the pipeline creates database users (ADR-0016)
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    administrators: {
      administratorType: 'ActiveDirectory'
      azureADOnlyAuthentication: true // no SQL logins, so no password can exist
      principalType: 'Application'
      login: adminLogin
      sid: adminObjectId
      tenantId: tenant().tenantId
    }
  }
}

// Consumption Container Apps have no fixed outbound IP: allow Azure services (0.0.0.0 is the special "Azure" rule).
// The pipeline adds and removes a rule for the runner IP while it creates the database users (ADR-0016): this works
// because denyDelete does not apply to child resources; do not enable --deny-settings-apply-to-child-scopes.
resource allowAzureServices 'Microsoft.Sql/servers/firewallRules@2025-01-01' = {
  parent: server
  name: 'AllowAllWindowsAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource database 'Microsoft.Sql/servers/databases@2025-01-01' = {
  parent: server
  name: databaseName
  location: location
  tags: tags
  sku: {
    name: 'GP_S_Gen5_2' // General Purpose serverless: the tier of the free offer
    tier: 'GeneralPurpose'
    family: 'Gen5'
    capacity: 2
  }
  properties: {
    useFreeLimit: true
    freeLimitExhaustionBehavior: 'AutoPause' // never bill: pause until next month when the free amount runs out
    autoPauseDelay: 60
    minCapacity: json('0.5')
    maxSizeBytes: 34359738368 // 32 GB, the free offer limit
    requestedBackupStorageRedundancy: 'Local'
  }
}

output serverName string = server.name
output serverFqdn string = server.properties.fullyQualifiedDomainName
output databaseName string = database.name
