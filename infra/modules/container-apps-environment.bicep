// Workload-profiles environment with only the Consumption profile: scales to zero, billed per use.
// (The legacy consumption-only type would have to be recreated to add profiles later.)
// No log destination yet: where logs and traces go (Application Insights / Log Analytics) is still to be decided.
param name string
param location string
param tags object

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
  }
}

output id string = environment.id
