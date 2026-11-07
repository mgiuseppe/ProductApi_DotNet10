// Log Analytics workspace and workspace-based Application Insights of one environment (ADR-0013).
// The API sends its OpenTelemetry data with Entra authentication: local authentication is off, so the connection
// string is not a secret, and the publishers get Monitoring Metrics Publisher here.
param workspaceName string
param appInsightsName string
param location string
param tags object

@description('Principal ids of the identities that send telemetry.')
param publisherPrincipalIds string[]

resource workspace 'Microsoft.OperationalInsights/workspaces@2025-02-01' = {
  name: workspaceName
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
    workspaceCapping: {
      dailyQuotaGb: json('0.1') // cost cap: over it ingestion stops until the next day
    }
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: workspace.id
    DisableLocalAuth: true
  }
}

// Built-in role "Monitoring Metrics Publisher": send telemetry, nothing else
var monitoringMetricsPublisherRoleId = '3913510d-42f4-4e42-8a64-420c390055eb'

resource publisherRoles 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for principalId in publisherPrincipalIds: {
    scope: appInsights
    name: guid(appInsights.id, principalId, monitoringMetricsPublisherRoleId)
    properties: {
      roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', monitoringMetricsPublisherRoleId)
      principalId: principalId
      principalType: 'ServicePrincipal'
    }
  }
]

output workspaceId string = workspace.id
output appInsightsConnectionString string = appInsights.properties.ConnectionString
