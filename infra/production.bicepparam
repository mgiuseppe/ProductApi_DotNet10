using 'main.bicep'

param environmentName = 'production'
param mongoAdminUser = 'productapiadmin' // same as shared.bicepparam

// Set by the CD workflow: the deployment identity (from the environment's AZURE_CLIENT_ID variable), the API image
// currently running and the new migrator image
param sqlAdminClientId = readEnvironmentVariable('AZURE_CLIENT_ID')
param apiImage = readEnvironmentVariable('API_IMAGE')
param migratorImage = readEnvironmentVariable('MIGRATOR_IMAGE')

// From GitHub secrets (ADR-0012): never written in this file
param mongoAdminPassword = readEnvironmentVariable('MONGO_ADMIN_PASSWORD')
param alertEmail = readEnvironmentVariable('ALERT_EMAIL')
