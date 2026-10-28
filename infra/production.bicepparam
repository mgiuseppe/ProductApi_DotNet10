using 'main.bicep'

param environmentName = 'production'
param sqlAdminLogin = 'productapiadmin'
param mongoAdminUser = 'productapiadmin' // same as shared.bicepparam

// Set by the CD workflow: the API image currently running and the new migrator image
param apiImage = readEnvironmentVariable('API_IMAGE')
param migratorImage = readEnvironmentVariable('MIGRATOR_IMAGE')

// From GitHub secrets (ADR-0015): never written in this file
param sqlAdminPassword = readEnvironmentVariable('SQL_ADMIN_PASSWORD')
param mongoAdminPassword = readEnvironmentVariable('MONGO_ADMIN_PASSWORD')
