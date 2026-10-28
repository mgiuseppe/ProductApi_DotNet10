# 0017. One free MongoDB cluster shared by the Azure environments

- Status: Accepted
- Date: 2026-10-28

## Context
The order history lives in MongoDB ([0013](0013-sql-server-for-aggregates.md), which keeps it from [0006](0006-postgresql-and-mongodb.md)). In Azure it runs on Azure DocumentDB (formerly Azure Cosmos DB for MongoDB vCore). Its free tier gives one 32 GB cluster **per subscription**, in a few regions only (in Europe: France Central, Germany North, North Europe, Norway East, Switzerland North), without Microsoft Entra ID authentication, backups or SLA, and pauses the cluster after 60 days of inactivity. A second cluster for the second environment would cost tens of euros a month for a project that only verifies its pipeline.

## Decision
- **One free DocumentDB cluster shared by `staging` and `production`**, each environment with its own database (`productapi-staging`, `productapi-production`). The cluster is shared only to avoid paying for a second one.
- **Region: North Europe for everything** (resource groups, Container Apps, Azure SQL, DocumentDB), the closest free-tier region with every service we use. The regions closer to Italy (West Europe, Italy North) have no DocumentDB free tier.
- **A third deployment stack, `productapi-shared`**, owns the cluster in its own resource group, with the same stack settings as the environments ([0015](0015-bicep-with-deployment-stacks.md)). Tearing down an environment leaves the cluster alone; the shared stack is deployed before the environments and deleted only on purpose.
- **Native authentication with an administrator password**, the only option on the free tier. The password is a GitHub secret, passed to Bicep as a `@secure()` parameter and stored as a Container App secret inside the connection string. It is never written in the repository or in parameter files, and compiled Bicep output is gitignored.
- **A third GitHub environment, `shared`**, with its own OIDC identity and the same manual approval as `production`, deploys and tears down the shared stack. `MONGO_ADMIN_PASSWORD` is a repository-level secret, because the shared stack and both environments need it.

## Consequences
- No isolation between environments at cluster level: same cluster, same administrator credentials (readable by every workflow), shared capacity. A staging run can read or damage production history. Accepted to avoid an unnecessary cost; with a budget, each environment gets its own cluster.
- MongoDB keeps a password, unlike Azure SQL ([0016](0016-azure-sql-with-managed-identity.md)): one secret to protect and rotate by hand.
- The firewall allows Azure services (`0.0.0.0`): any Azure-hosted client, in any tenant, can reach the cluster, and the password is the only protection.
- No backups: the order history is not recoverable. Acceptable for a log that can be lost in a demo; not for a real production system.
- The cluster pauses after 60 days without use and must be resumed by hand.
- Everything lives in one region: no latency between services, about 30-40 ms more from Italy than West Europe, irrelevant here.

## Alternatives considered
- **One paid cluster per environment**: full isolation and Entra ID authentication, at a monthly cost.
- **App in West Europe, DocumentDB in North Europe**: closer to Italy, but two regions and cross-region latency for no real gain.
- **DocumentDB for production, MongoDB Atlas M0 (free, 512 MB) for staging**: two providers for the same service, so staging would not verify what production does.
- **Order history in Azure SQL**: one store, transactional and protected by managed identity, but it reverses the reason MongoDB is in the project ([0006](0006-postgresql-and-mongodb.md)).
