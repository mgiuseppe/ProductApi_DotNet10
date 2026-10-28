// Azure DocumentDB (MongoDB) free-tier cluster shared by the environments (ADR-0017).
// Free tier: 32 GB, no Entra ID authentication, no backups, paused after 60 days without use.
param name string
param location string
param tags object
param adminUser string

@secure()
param adminPassword string

resource cluster 'Microsoft.DocumentDB/mongoClusters@2026-06-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    administrator: {
      userName: adminUser
      password: adminPassword
    }
    serverVersion: '7.0'
    compute: {
      tier: 'Free'
    }
    storage: {
      sizeGb: 32
    }
    sharding: {
      shardCount: 1
    }
    highAvailability: {
      targetMode: 'Disabled'
    }
    publicNetworkAccess: 'Enabled'
  }
}

// Consumption Container Apps have no fixed outbound IP: allow Azure services
resource allowAzureServices 'Microsoft.DocumentDB/mongoClusters/firewallRules@2026-06-01' = {
  parent: cluster
  name: 'AllowAllAzureServicesAndResourcesWithinAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

output name string = cluster.name
// Connection string template from Azure, without the password: it is completed where it is used, as a Container App secret
output connectionStringTemplate string = cluster.properties.connectionString
