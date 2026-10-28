using 'main.bicep'

param environmentName = 'production'

// Pipeline identity of the production environment (ADR-0016): not secret, set by the workflow from GitHub environment variables
param sqlAdminLogin = readEnvironmentVariable('SQL_ADMIN_LOGIN')
param sqlAdminObjectId = readEnvironmentVariable('SQL_ADMIN_OBJECT_ID')
