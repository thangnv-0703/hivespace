# ADR-0015: Aspire orchestration with Azure emulators locally

**Status:** Accepted
**Date:** 2026-11-15
**Requirements addressed:** NFR-PORT-001, NFR-MNT-005, NFR-REL-007, NFR-OBS-002, NFR-SEC-007, NFR-COST-003, EIR-SW-003, EIR-SW-007, EIR-COM-003; dev-standards §3; CR-2

## Context
A fresh clone must run the full stack with one command (NFR-PORT-001). The builder wants local dependencies to be emulators of Azure services, never real cloud resources, so local runs are free, offline-capable and safe.

## Decision
`dotnet run --project apphost` starts, through Aspire:
- all services (.NET as projects; Go and Python as containers or executables), the gateway with both SPAs, and the Function;
- **Azure Service Bus emulator** (RabbitMQ is an optional second broker implementation);
- **Azurite** for blob storage;
- **PostgreSQL with pgvector** as a plain container (Azure has no Postgres emulator), one database per service;
- **MongoDB** as a plain container for Cart (Azure has no MongoDB emulator; a documented exception, ADR-0018);
- **Mailpit** for email; a fake OIDC provider for tests; the mock payment and shipping providers in their services.
The cloud differs only by configuration: connection strings and provider selection come from Aspire parameters locally and from the platform in the demo. An architecture test forbids reading cloud connection strings outside the provider project. Heavy jobs (embeddings, generator, k6) never run in the deployed profile (NFR-COST-003).

## Options considered
- **Aspire with emulators (chosen)** — one command, one dashboard, same code path as the cloud.
- **docker compose** — replaced by CR-2.
- **Real Azure resources for development** — costs money, needs network, risks leaking data.

## Consequences
Easier: free and reproducible local runs; tests with real brokers and storage. Harder: the Service Bus emulator needs a SQL Server container (heavy on RAM) and has feature limits compared with the cloud; the Function's local run in Aspire needs verification (R-9). Revisit if the emulator's limits block a tested feature.
