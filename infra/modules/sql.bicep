// Azure SQL with Entra authentication only and the free offer (ADR-0013, ADR-0018).
// The Entra administrator is the identity that deploys the environment: the deploy workflow signs in with it
// to create the database users of the API and the migrator (infra/sql/database-users.sql).
param serverName string
param databaseName string
param location string
param tags object

@description('Display name of the Entra administrator.')
param adminLogin string

@description('Client (application) id of the Entra administrator: for an application, Azure SQL identifies it by client id.')
param adminClientId string

resource server 'Microsoft.Sql/servers@2025-01-01' = {
  name: serverName
  location: location
  tags: tags
  properties: {
    administrators: {
      administratorType: 'ActiveDirectory'
      azureADOnlyAuthentication: true // no SQL logins, no password
      principalType: 'Application'
      login: adminLogin
      sid: adminClientId
      tenantId: tenant().tenantId
    }
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

// Consumption Container Apps have no fixed outbound IP: allow Azure services (0.0.0.0 is the special "Azure" rule)
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

output serverFqdn string = server.properties.fullyQualifiedDomainName
output databaseName string = database.name
