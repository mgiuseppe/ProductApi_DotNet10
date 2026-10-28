using 'main.bicep'

param environmentName = 'staging'

// Pipeline identity of the staging environment (ADR-0016): not secret, set by the workflow from GitHub environment variables
param sqlAdminLogin = readEnvironmentVariable('SQL_ADMIN_LOGIN')
param sqlAdminObjectId = readEnvironmentVariable('SQL_ADMIN_OBJECT_ID')
