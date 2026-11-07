// The migrator (EF Core migrations bundle, ADR-0008) as a manually triggered job: the pipeline starts it before
// updating the API and stops if it fails.
param name string
param location string
param tags object
param environmentId string
param identityId string
param image string
@description('Azure SQL connection string, without secret: the identity signs in (ADR-0012).')
param sqlConnectionString string
param mongoSecretUri string

resource job 'Microsoft.App/jobs@2026-07-01' = {
  name: name
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identityId}': {}
    }
  }
  properties: {
    environmentId: environmentId
    workloadProfileName: 'Consumption'
    configuration: {
      triggerType: 'Manual'
      replicaTimeout: 600
      replicaRetryLimit: 0 // a failed migration must be looked at, not retried
      manualTriggerConfig: {
        parallelism: 1
        replicaCompletionCount: 1
      }
      secrets: [
        {
          name: 'mongo-connection-string'
          keyVaultUrl: mongoSecretUri
          identity: identityId
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'migrator'
          image: image
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          // The bundle builds the app host, which needs both connection strings (only the SQL one is used)
          env: [
            { name: 'ConnectionStrings__DbConnection', value: sqlConnectionString }
            { name: 'ConnectionStrings__MongoDb', secretRef: 'mongo-connection-string' }
          ]
        }
      ]
    }
  }
}

output name string = job.name
