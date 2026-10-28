using 'shared.bicep'

param mongoAdminUser = 'productapiadmin'

// From the GitHub secret MONGO_ADMIN_PASSWORD (ADR-0015): never written in this file
param mongoAdminPassword = readEnvironmentVariable('MONGO_ADMIN_PASSWORD')
