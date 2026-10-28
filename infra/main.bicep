// One environment (staging or production), deployed as a deployment stack (ADR-0015) after shared.bicep:
//   az stack sub create --name productapi-<env> --location northeurope \
//     --template-file infra/main.bicep --parameters infra/<env>.bicepparam \
//     --action-on-unmanage detachAll --deny-settings-mode denyDelete
// Modules are our own. Azure Verified Modules (br/public:avm/...) are the usual choice in enterprise setups:
// worth revisiting if the infrastructure grows.
targetScope = 'subscription'

@description('Environment name, used in resource names and tags.')
@allowed(['staging', 'production'])
param environmentName string

@description('Azure region for every resource (ADR-0015: North Europe, free DocumentDB tier).')
param location string = 'northeurope'

@description('Administrator login of the SQL server.')
@minLength(1)
param sqlAdminLogin string

@description('Administrator password of the SQL server, from a GitHub secret (ADR-0015).')
@secure()
param sqlAdminPassword string

@description('API image (ghcr.io/...:<tag>). The pipeline passes the image currently running, so a deployment never ships new code before the migrator has run.')
param apiImage string

@description('Migrator image (ghcr.io/...:<tag>), the new version.')
param migratorImage string

@description('Administrator user of the shared MongoDB cluster (must match shared.bicepparam).')
@minLength(1)
param mongoAdminUser string

@description('Administrator password of the shared MongoDB cluster, from a GitHub secret (ADR-0015).')
@secure()
param mongoAdminPassword string

var appName = 'productapi'
var tags = {
  app: appName
  environment: environmentName
  managedBy: 'bicep'
}
var suffix = uniqueString(subscription().id, appName, environmentName)

resource resourceGroup 'Microsoft.Resources/resourceGroups@2025-04-01' = {
  name: 'rg-${appName}-${environmentName}'
  location: location
  tags: tags
}

// The shared cluster deployed by shared.bicep: same resource group and same name formula
resource sharedResourceGroup 'Microsoft.Resources/resourceGroups@2025-04-01' existing = {
  name: 'rg-${appName}-shared'
}

resource mongoCluster 'Microsoft.DocumentDB/mongoClusters@2026-06-01' existing = {
  scope: sharedResourceGroup
  name: 'mongo-${appName}-${uniqueString(subscription().id, sharedResourceGroup.id)}'
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
    serverName: 'sql-${appName}-${environmentName}-${suffix}'
    databaseName: appName
    location: location
    tags: tags
    adminLogin: sqlAdminLogin
    adminPassword: sqlAdminPassword
  }
}

// Azure's template is mongodb+srv://<user>:<password>@host/?options: fill the credentials and this environment's database
var mongoConnectionString = replace(
  replace(
    replace(mongoCluster.properties.connectionString, '<user>', mongoAdminUser),
    '<password>',
    uriComponent(mongoAdminPassword)
  ),
  '/?',
  '/${appName}-${environmentName}?'
)

module identity 'modules/identity.bicep' = {
  scope: resourceGroup
  params: {
    name: 'id-${appName}-${environmentName}'
    location: location
    tags: tags
  }
}

module keyVault 'modules/key-vault.bicep' = {
  scope: resourceGroup
  params: {
    name: 'kv-${appName}-${take(suffix, 8)}' // max 24 characters, globally unique
    location: location
    tags: tags
    readerPrincipalId: identity.outputs.principalId
    sqlConnectionString: 'Server=tcp:${sql.outputs.serverFqdn},1433;Database=${sql.outputs.databaseName};User ID=${sqlAdminLogin};Password=${sqlAdminPassword};Encrypt=True;TrustServerCertificate=False'
    mongoConnectionString: mongoConnectionString
  }
}

// App and job depend on the vault module outputs, so they are created after the identity can read the secrets
module api 'modules/container-app.bicep' = {
  scope: resourceGroup
  params: {
    name: 'ca-${appName}-${environmentName}'
    location: location
    tags: tags
    environmentId: containerAppsEnvironment.outputs.id
    identityId: identity.outputs.id
    image: apiImage
    sqlSecretUri: keyVault.outputs.sqlSecretUri
    mongoSecretUri: keyVault.outputs.mongoSecretUri
  }
}

module migrator 'modules/migrator-job.bicep' = {
  scope: resourceGroup
  params: {
    name: 'caj-${appName}-mig-${environmentName}' // max 32 characters
    location: location
    tags: tags
    environmentId: containerAppsEnvironment.outputs.id
    identityId: identity.outputs.id
    image: migratorImage
    sqlSecretUri: keyVault.outputs.sqlSecretUri
    mongoSecretUri: keyVault.outputs.mongoSecretUri
  }
}

output resourceGroupName string = resourceGroup.name
output keyVaultName string = keyVault.outputs.name
output apiName string = api.outputs.name
output apiFqdn string = api.outputs.fqdn
output migratorJobName string = migrator.outputs.name
