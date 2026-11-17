# ADR-0005: Hosting on Azure Container Apps with Neon, cost levers deferred

**Status:** Proposed (deploy mechanism and broker cost to be decided before first deploy)
**Date:** 2026-11-15
**Requirements addressed:** NFR-PORT-002, NFR-COST-001, NFR-COST-003, NFR-COST-004, NFR-MNT-002 (hosting), NFR-MNT-004, NFR-SEC-001, NFR-SEC-007, NFR-REL-006, NFR-PERF-006, NFR-DEM-001, CON-2, CON-5; OQ-3

## Context
The demo must run on Azure container hosting that scales to zero, for about $5 a month including AI. The demo is used about 1 hour a day. There is no AKS (CON-5). Neon was chosen for Postgres. A container registry is needed; the builder has access to a free ACR tier. Azure Service Bus Standard (needed for topics) is billed per hour while the namespace exists.

## Decision
- **Compute:** Azure Container Apps, Consumption plan, all apps scale to zero; only the gateway has external ingress (TLS by the platform).
- **Database:** Neon free tier, one project per service (isolation and independent compute). Same Postgres version and extensions as local (`pgvector`, `unaccent`, `pg_trgm`).
- **Registry:** Azure Container Registry on the free tier the builder has. Confirm its terms before the first deploy.
- **Broker:** Azure Service Bus (ADR-0002).
- **Storage:** Azure Blob Storage (`incoming` private, `public` read, plus a container for data-protection keys).
- **Functions:** one Function app on the Consumption plan for the image processor.
- **IaC and deploy:** `azd` publishing the Aspire AppHost, generated Bicep committed under `infra/` (NFR-MNT-004), a budget alert in IaC (NFR-COST-001).
- **Secrets:** Container Apps secrets or Key Vault.
- **Deferred:** the MongoDB host for Cart (MongoDB Atlas free tier or Cosmos DB for the MongoDB API; limits to verify), decided with the first cost estimate (ADR-0018).
- **Deferred:** whether the Service Bus namespace and other billed-by-the-hour resources are **provisioned and torn down per daily deploy window**, or kept provisioned. The IaC supports both; the decision is made when the first monthly cost is known.

## Options considered
- **Container Apps plus Neon (chosen)** — fits scale to zero and free tiers; portable containers and plain Postgres.
- **Azure Database for PostgreSQL Flexible Server** — stays in Azure; the free offer is time-limited and a small server exceeds the whole budget afterwards.
- **App Service, Functions-only or AKS** — App Service lacks scale-to-zero on cheap tiers; Functions-only does not suit Go and Python services; AKS is excluded.

## Consequences
Cold starts stack (app, database, Function), see R-4. Neon limits (storage, compute hours) cap demo data (R-3). Service Bus cost is the main budget risk (R-2); the `IEventBus` abstraction allows switching. Everything except the external-ingress gateway is unreachable from the internet. Revisit when real monthly cost is measured, or if Neon or ACR terms change.
