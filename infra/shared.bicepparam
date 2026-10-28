using 'shared.bicep'

param mongoAdminUser = 'productapiadmin'

// From the GitHub secret MONGO_ADMIN_PASSWORD (ADR-0017): never written in this file
param mongoAdminPassword = readEnvironmentVariable('MONGO_ADMIN_PASSWORD')
