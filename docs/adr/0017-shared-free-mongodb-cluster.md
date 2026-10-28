# 0017. One free MongoDB cluster shared by the Azure environments

- Status: Accepted
- Date: 2026-10-28

## Context
The order history lives in MongoDB ([0013](0013-sql-server-for-aggregates.md), which keeps it from [0006](0006-postgresql-and-mongodb.md)). In Azure it runs on Azure DocumentDB (formerly Azure Cosmos DB for MongoDB vCore). Its free tier gives one 32 GB cluster **per subscription**, in a few regions only (in Europe: France Central, Germany North, North Europe, Norway East, Switzerland North), without Microsoft Entra ID authentication, backups or SLA, and pauses the cluster after 60 days of inactivity. A second cluster for the second environment would cost tens of euros a month for a project that only verifies its pipeline.

## Decision
- **One free DocumentDB cluster shared by `staging` and `production`**, each environment with its own database (`productapi-staging`, `productapi-production`). The cluster is shared only to avoid paying for a second one.
- **Region: North Europe for everything**, the closest free-tier region with every service we use. The regions closer to Italy (West Europe, Italy North) have no DocumentDB free tier.
- **Its own resource group and deployment stack, `productapi-shared`** ([0015](0015-bicep-with-deployment-stacks.md)), because its lifecycle is not the one of an environment: tearing down staging or production leaves it alone.
- **The pipeline deploys it on every run**, before the environments and with the staging identity. The deployment is idempotent: nothing changes if it already exists, and it is recreated after a full teardown. Only the "tear down everything" operation deletes it, as its last step.
- **Administrator password** (the only option on the free tier) in the repository-level GitHub secret `MONGO_ADMIN_PASSWORD`; each environment stores its connection string in its Key Vault ([0016](0016-secrets-in-key-vault.md)).

## Consequences
- No isolation between environments at cluster level: same cluster, same administrator credentials, shared capacity. A staging run can read or damage production history. Accepted to avoid an unnecessary cost; with a budget, each environment gets its own cluster.
- The firewall allows Azure services (`0.0.0.0`): any Azure-hosted client, in any tenant, can reach the cluster, and the password is the only protection.
- No backups: the order history is not recoverable. Acceptable for a log that can be lost in a demo; not for a real production system.
- The cluster pauses after 60 days without use and must be resumed by hand.

## Alternatives considered
- **One paid cluster per environment**: full isolation and Entra ID authentication, at a monthly cost.
- **App in West Europe, DocumentDB in North Europe**: closer to Italy, but two regions and cross-region latency for no real gain.
- **DocumentDB for production, MongoDB Atlas M0 (free, 512 MB) for staging**: two providers for the same service, so staging would not verify what production does.
