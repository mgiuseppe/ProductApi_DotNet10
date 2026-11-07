// One environment (staging or production), deployed as a deployment stack (ADR-0011) after shared.bicep:
//   az stack sub create --name productapi-<env> --location francecentral \
//     --template-file infra/main.bicep --parameters infra/<env>.bicepparam \
//     --action-on-unmanage detachAll --deny-settings-mode denyDelete
// Modules are our own. Azure Verified Modules (br/public:avm/...) are the usual choice in enterprise setups:
// worth revisiting if the infrastructure grows.
targetScope = 'subscription'

@description('Environment name, used in resource names and tags.')
@allowed(['staging', 'production'])
param environmentName string

@description('Azure region for every resource (ADR-0011: France Central, Azure SQL and free DocumentDB tier).')
param location string = 'francecentral'

@description('Client id of the deployment identity of the environment, the Entra administrator of the SQL server (ADR-0012).')
param sqlAdminClientId string

@description('API image (ghcr.io/...:<tag>). The pipeline passes the image currently running, so a deployment never ships new code before the migrator has run.')
param apiImage string

@description('Migrator image (ghcr.io/...:<tag>), the new version.')
param migratorImage string

@description('Administrator user of the shared MongoDB cluster (must match shared.bicepparam).')
@minLength(1)
param mongoAdminUser string

@description('Administrator password of the shared MongoDB cluster, from a GitHub secret (ADR-0012).')
@secure()
param mongoAdminPassword string

@description('Recipient of the alert emails, from a GitHub secret: not in the public repository.')
@minLength(1)
param alertEmail string

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
    workspaceId: monitoring.outputs.workspaceId
  }
}

// Telemetry of the API and logs of the environment (ADR-0013)
module monitoring 'modules/monitoring.bicep' = {
  scope: resourceGroup
  params: {
    workspaceName: 'log-${appName}-${environmentName}'
    appInsightsName: 'appi-${appName}-${environmentName}'
    location: location
    tags: tags
    publisherPrincipalIds: [apiIdentity.outputs.principalId]
  }
}

// Entra administrator: the deployment identity of the environment (ADR-0012)
module sql 'modules/sql.bicep' = {
  scope: resourceGroup
  params: {
    serverName: 'sql-${appName}-${environmentName}-${suffix}'
    databaseName: appName
    location: location
    tags: tags
    adminLogin: 'github-${appName}-${environmentName}'
    adminClientId: sqlAdminClientId
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

// One identity per workload, so each gets only its own database rights (ADR-0012)
module apiIdentity 'modules/identity.bicep' = {
  scope: resourceGroup
  params: {
    name: 'id-${appName}-api-${environmentName}'
    location: location
    tags: tags
  }
}

module migratorIdentity 'modules/identity.bicep' = {
  scope: resourceGroup
  params: {
    name: 'id-${appName}-migrator-${environmentName}'
    location: location
    tags: tags
  }
}

// No secret: the identity named by User Id signs in (ADR-0012)
var sqlConnectionStringBase = 'Server=tcp:${sql.outputs.serverFqdn},1433;Database=${sql.outputs.databaseName};Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False'

module keyVault 'modules/key-vault.bicep' = {
  scope: resourceGroup
  params: {
    name: 'kv-${appName}-${take(suffix, 8)}' // max 24 characters, globally unique
    location: location
    tags: tags
    readerPrincipalIds: [apiIdentity.outputs.principalId] // the migrator never uses MongoDB
    mongoConnectionString: mongoConnectionString
  }
}

// The app depends on the vault module outputs, so it is created after its identity can read the secret
module api 'modules/container-app.bicep' = {
  scope: resourceGroup
  params: {
    name: 'ca-${appName}-${environmentName}'
    location: location
    tags: tags
    environmentId: containerAppsEnvironment.outputs.id
    identityId: apiIdentity.outputs.id
    identityClientId: apiIdentity.outputs.clientId
    image: apiImage
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
    sqlConnectionString: '${sqlConnectionStringBase};User Id=${apiIdentity.outputs.clientId}'
    mongoSecretUri: keyVault.outputs.mongoSecretUri
  }
}

// Email when the API keeps restarting or returns 5xx: what the probes cannot fix by themselves
module alerts 'modules/alerts.bicep' = {
  scope: resourceGroup
  params: {
    appName: appName
    environmentName: environmentName
    tags: tags
    containerAppId: api.outputs.id
    alertEmail: alertEmail
  }
}

module migrator 'modules/migrator-job.bicep' = {
  scope: resourceGroup
  params: {
    name: 'caj-${appName}-mig-${environmentName}' // max 32 characters
    location: location
    tags: tags
    environmentId: containerAppsEnvironment.outputs.id
    identityId: migratorIdentity.outputs.id
    image: migratorImage
    sqlConnectionString: '${sqlConnectionStringBase};User Id=${migratorIdentity.outputs.clientId}'
  }
}

output resourceGroupName string = resourceGroup.name
output keyVaultName string = keyVault.outputs.name
output apiName string = api.outputs.name
output apiFqdn string = api.outputs.fqdn
output migratorJobName string = migrator.outputs.name
// Read by the deploy workflow to create the database users (infra/sql/database-users.sql)
output sqlServerFqdn string = sql.outputs.serverFqdn
output sqlDatabaseName string = sql.outputs.databaseName
output apiIdentityName string = apiIdentity.outputs.name
output apiIdentityClientId string = apiIdentity.outputs.clientId
output migratorIdentityName string = migratorIdentity.outputs.name
output migratorIdentityClientId string = migratorIdentity.outputs.clientId
