// Resources shared by every environment (ADR-0017): own deployment stack, deployed by the pipeline before the environments
// on every run (idempotent), deleted only by the "tear down everything" operation:
//   az stack sub create --name productapi-shared --location northeurope \
//     --template-file infra/shared.bicep --parameters infra/shared.bicepparam \
//     --action-on-unmanage detachAll --deny-settings-mode denyDelete
targetScope = 'subscription'

@description('Azure region (ADR-0017: North Europe, free DocumentDB tier).')
param location string = 'northeurope'

@description('Administrator user of the MongoDB cluster: the free tier has no Entra ID authentication.')
@minLength(1)
param mongoAdminUser string

@description('Administrator password of the MongoDB cluster, from a GitHub secret.')
@secure()
param mongoAdminPassword string

var appName = 'productapi'
var tags = {
  app: appName
  environment: 'shared'
  managedBy: 'bicep'
}

resource resourceGroup 'Microsoft.Resources/resourceGroups@2025-04-01' = {
  name: 'rg-${appName}-shared'
  location: location
  tags: tags
}

module mongo 'modules/mongo-cluster.bicep' = {
  scope: resourceGroup
  params: {
    name: 'mongo-${appName}-${uniqueString(subscription().id, resourceGroup.id)}'
    location: location
    tags: tags
    adminUser: mongoAdminUser
    adminPassword: mongoAdminPassword
  }
}

output resourceGroupName string = resourceGroup.name
output mongoClusterName string = mongo.outputs.name
output mongoConnectionStringTemplate string = mongo.outputs.connectionStringTemplate
