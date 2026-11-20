# ADR-0025: One JavaScript runner (cucumber-js) for all Gherkin contract tests

**Status:** Accepted
**Date:** 2026-11-20
**Requirements addressed:** NFR-MNT-005, EIR-COM-001 (contract-first), FR-LOC-004
**Supersedes:** the godog and pytest-bdd contract-test mentions in ADR-0016 and ADR-0017, and the per-stack runner list in dev-standards §9.1

## Context
The shared `.feature` files in `contracts/features/` were meant to be run by one runner per stack: Reqnroll (.NET), godog (Go) and pytest-bdd (Python) (dev-standards §9.1). Only Reqnroll was built, for Identity and Catalog. A contract scenario only calls the public HTTP API and checks the status, the stable error `code` and the body, so the language of the service says nothing about the language of the test. Three runners would mean three sets of step definitions for the same behaviour.

## Decision
- **All Gherkin contract tests run on cucumber-js** in `tests/contract/` (TypeScript, Node). One set of shared steps, one set of per-service steps. godog and pytest-bdd are no longer planned.
- **Black box.** The runner starts the real service process and a Postgres container (Testcontainers for Node) per service, then calls it over HTTP. Services are started lazily, by the `@service:<name>` tag on the feature.
- **State outside the API** (reading the emailed verification token from the outbox, expiring a token, counting queued events, resetting between scenarios) goes through direct SQL on the service's own database. No test-only endpoints in product code.
- **Broker:** the service gets a dead Service Bus endpoint, so the outbox relay logs a warning and leaves rows unsent. The steps read them.
- **Reqnroll is removed.** The in-process tests that were never Gherkin (`OidcFlowTests` and the shell checks) moved into `Identity.IntegrationTests`.
- **Boundary rules still hold.** The runner knows each service only by its HTTP address and its database, never by its code.

## Options considered
- **cucumber-js for every service (chosen)** — one runner, one step language, services tested as clients see them, the same pnpm and Node tooling as `tests/e2e`.
- **One native runner per stack** — each service's tests run in its own toolchain with in-process hosting and no process management, but the same steps are written three times and two runners never got built.
- **Test-only endpoints for seeding** — simpler steps, but test code ships inside every service.

## Consequences
Easier: one place to read and write contract steps; a new service (any language) only needs a tag and a small spec entry in `tests/contract/support/service.ts`; behaviour is checked from the outside.
Harder: steps that touch the database depend on table and column names, so a schema rename breaks them (they sit beside the migration and fail loudly). Starting a process and a container per service is slower than hosting in-process (about 25 s for Identity and Catalog together). `@rpc:` scenarios for gRPC (ADR-0019) will need a gRPC client in the runner (`@grpc/grpc-js` with the generated stubs).

**What it teaches:** treating an API contract as language-neutral in practice, process and container orchestration from a test harness, and the cost of black-box testing against the speed of in-process hosting.
