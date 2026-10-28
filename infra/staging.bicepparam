using 'main.bicep'

param environmentName = 'staging'
param sqlAdminLogin = 'productapiadmin'
param mongoAdminUser = 'productapiadmin' // same as shared.bicepparam

// From GitHub secrets (ADR-0016, ADR-0017): never written in this file
param sqlAdminPassword = readEnvironmentVariable('SQL_ADMIN_PASSWORD')
param mongoAdminPassword = readEnvironmentVariable('MONGO_ADMIN_PASSWORD')
