# ADR-0019: gRPC for internal service-to-service calls

**Status:** Accepted
**Date:** 2026-11-16
**Requirements addressed:** EIR-COM-001, FR-CHK-004..008, FR-INV-002, FR-INV-005, FR-LOC-004, NFR-REL-002, NFR-OBS-001, NFR-PERF-004, NFR-PERF-005, NFR-MNT-006, NFR-SEC-006

## Context
Services call each other synchronously in a few places: Order to Inventory (reserve and release), Order to Cart, Catalog and Payment, Cart to Catalog and Inventory, Catalog to Inventory and Media, Identity to Media (architecture §5.3). The first design used REST for these. The builder wants gRPC for internal calls, partly to learn it. The public API must stay REST/OpenAPI through the gateway because browsers cannot call gRPC directly and the OpenAPI-first workflow, generated TypeScript client and Gherkin contract tests depend on it.

## Decision
- **Internal synchronous calls use gRPC. Public operations stay REST/OpenAPI through the gateway/BFF. Events stay on the broker (ADR-0002). Payment callbacks stay REST.**
- **Contracts:** `.proto` files in `contracts/proto/`, one package per service, versioned in the package name. `buf` runs lint and breaking-change checks in CI. Stubs are generated per language (.NET, Go, Python).
- **Deadlines** replace HTTP timeouts: the caller sets a deadline per call (checkout step 10 s, OQ-13) and it propagates. Retries only on idempotent calls (reserve and release are idempotent by design, ADR-0007).
- **Errors:** the gRPC status code carries the class, and the stable error code and field details travel in status details (`google.rpc.ErrorInfo`), so `{ code, message, details }` is rebuilt at the gateway and FR-LOC-004 still holds.
- **Auth:** the user's bearer token travels as call metadata and every service validates it through JWKS as before (NFR-SEC-006). Calls with no user (sweepers, event handlers) use internal ingress only and no token.
- **Hosting:** Container Apps internal ingress with HTTP/2; Aspire references use HTTP/2 locally. Only the gateway has external ingress.
- **Observability:** gRPC OpenTelemetry instrumentation in all three stacks; the trace context travels in metadata (NFR-OBS-001).
- **Testing:** `buf breaking` in CI; Gherkin scenarios for gRPC methods tagged `@rpc:<Service.Method>` with the requirement ID, for the saga-critical calls (Reserve, Release, selected cart lines, Initiate payment). The failure-injection and idempotency tests (NFR-REL-002) call the real gRPC endpoints.
- **Services that need both:** a service exposes its public REST API and its internal gRPC API side by side, sharing the same application layer.

## Options considered
- **gRPC for internal calls (chosen)** — typed contracts, generated stubs in all three stacks, deadlines, small payloads, and a good fit for the batch reserve call (NFR-PERF-005).
- **REST for everything** — one contract style and one set of tests; weaker typing across three languages and nothing new to learn.
- **gRPC for everything with JSON transcoding at the gateway** — one contract language, but reworks OpenAPI-first, the generated client and the contract tests. Rejected for now.

## Consequences
Easier: shared typed contracts, consistent deadlines, efficient batch calls. Harder: a second contract language and tooling (`buf`, stub generation) in three stacks (risk R-14); gRPC is harder to inspect than REST with curl (use `grpcurl`); internal ingress and Aspire must be configured for HTTP/2, to verify at scaffolding. Revisit if transcoding becomes attractive to reduce duplicate contracts.
