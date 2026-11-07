// Workload-profiles environment with only the Consumption profile: scales to zero, billed per use.
// (The legacy consumption-only type would have to be recreated to add profiles later.)
// Console and system logs of every app and job go to the Log Analytics workspace of the environment (ADR-0013),
// through a diagnostic setting: no workspace shared key in the template.
param name string
param location string
param tags object
param workspaceId string

resource environment 'Microsoft.App/managedEnvironments@2026-07-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    workloadProfiles: [
      {
        name: 'Consumption'
        workloadProfileType: 'Consumption'
      }
    ]
    appLogsConfiguration: {
      destination: 'azure-monitor'
    }
  }
}

// Latest version of the type, a preview: the linter warns about its age, the only newer-looking choice is not newer
resource logs 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  scope: environment
  name: 'logs-to-workspace'
  properties: {
    workspaceId: workspaceId
    logs: [
      { category: 'ContainerAppConsoleLogs', enabled: true }
      { category: 'ContainerAppSystemLogs', enabled: true }
    ]
  }
}

output id string = environment.id
