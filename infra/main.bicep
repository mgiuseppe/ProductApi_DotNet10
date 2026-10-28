// One environment (staging or production), deployed as a deployment stack (ADR-0015):
//   az stack sub create --name productapi-<env> --location northeurope \
//     --template-file infra/main.bicep --parameters infra/<env>.bicepparam \
//     --action-on-unmanage detachAll --deny-settings-mode denyDelete
// Modules are our own. Azure Verified Modules (br/public:avm/...) are the usual choice in enterprise setups:
// worth revisiting if the infrastructure grows.
targetScope = 'subscription'

@description('Environment name, used in resource names and tags.')
@allowed(['staging', 'production'])
param environmentName string

@description('Azure region for every resource (ADR-0017: North Europe, free DocumentDB tier).')
param location string = 'northeurope'

@description('Display name of the pipeline identity of this environment, Entra admin of the SQL server (ADR-0016).')
@minLength(1)
param sqlAdminLogin string

@description('Object id of the pipeline identity of this environment.')
@minLength(36)
@maxLength(36)
param sqlAdminObjectId string

var appName = 'productapi'
var tags = {
  app: appName
  environment: environmentName
  managedBy: 'bicep'
}

resource resourceGroup 'Microsoft.Resources/resourceGroups@2025-04-01' = {
  name: 'rg-${appName}-${environmentName}'
  location: location
  tags: tags
}

module containerAppsEnvironment 'modules/container-apps-environment.bicep' = {
  scope: resourceGroup
  params: {
    name: 'cae-${appName}-${environmentName}'
    location: location
    tags: tags
  }
}

module sql 'modules/sql.bicep' = {
  scope: resourceGroup
  params: {
    serverName: 'sql-${appName}-${environmentName}-${uniqueString(subscription().id, resourceGroup.id)}'
    databaseName: appName
    location: location
    tags: tags
    adminLogin: sqlAdminLogin
    adminObjectId: sqlAdminObjectId
  }
}

output resourceGroupName string = resourceGroup.name
output containerAppsEnvironmentId string = containerAppsEnvironment.outputs.id
output sqlServerFqdn string = sql.outputs.serverFqdn
output sqlServerName string = sql.outputs.serverName
output sqlDatabaseName string = sql.outputs.databaseName
