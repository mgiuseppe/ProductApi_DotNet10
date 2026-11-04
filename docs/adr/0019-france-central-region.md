# 0019. France Central as the Azure region

- Status: Accepted
- Date: 2026-11-04
- Supersedes: the region choice of [0015](0015-azure-infrastructure.md) ("North Europe for everything")

## Context
[0015](0015-azure-infrastructure.md) puts everything in North Europe, the closest region with the free DocumentDB tier. On the first deployment Azure refused to create the SQL server there (`RegionDoesNotAllowProvisioning`): the subscription is restricted from creating Azure SQL servers in North Europe, West Europe, Switzerland North, Norway East and UK South. A support request can lift the restriction, with no guaranteed answer or delay.

## Decision
**France Central for everything**: the only European region where the subscription can create Azure SQL servers and the free DocumentDB tier exists.

## Consequences
- One region as before, both services on their free offer.
- The shared stack, already created in North Europe, is deleted and recreated (one free cluster per subscription).
- Region restrictions depend on the subscription: on another subscription check first with the SQL capabilities API (`Microsoft.Sql/locations/<region>/capabilities`).

## Alternatives considered
- **SQL in France Central, MongoDB in North Europe**: two regions, latency and cross-region traffic between the API and MongoDB.
- **Support request to lift the North Europe restriction**: no code change, but days of waiting and no guaranteed answer.
