// The API: public ingress on 8080, health probes on 8081 (not exposed by the ingress), secrets from Key Vault.
param name string
param location string
param tags object
param environmentId string
param identityId string
param image string
param sqlSecretUri string
param mongoSecretUri string

resource app 'Microsoft.App/containerApps@2026-07-01' = {
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
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
      }
      secrets: [
        {
          name: 'sql-connection-string'
          keyVaultUrl: sqlSecretUri
          identity: identityId
        }
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
          name: 'api'
          image: image
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: [
            { name: 'ConnectionStrings__DbConnection', secretRef: 'sql-connection-string' }
            { name: 'ConnectionStrings__MongoDb', secretRef: 'mongo-connection-string' }
          ]
          probes: [
            {
              type: 'Liveness'
              httpGet: { path: '/alive', port: 8081 }
              periodSeconds: 30
            }
            {
              // Queries SQL and MongoDB: a long period keeps the serverless database from staying awake for probes only
              type: 'Readiness'
              httpGet: { path: '/health', port: 8081 }
              periodSeconds: 60
              failureThreshold: 3
            }
          ]
        }
      ]
      scale: {
        minReplicas: 0 // scale to zero when idle: no cost, the first request waits for a cold start
        maxReplicas: 1
      }
    }
  }
}

output name string = app.name
output fqdn string = app.properties.configuration.ingress.fqdn
