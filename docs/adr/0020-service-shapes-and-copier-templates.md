# ADR-0020: Service shapes (rich and lean) and Copier templates

**Status:** Accepted
**Date:** 2026-11-16
**Requirements addressed:** NFR-MNT-001, NFR-MNT-003, NFR-MNT-006, NFR-MNT-007, NFR-PORT-001, CON-1, CON-7

## Context
The system has 9 services in three stacks plus a gateway, a Function and two SPAs (architecture §5.2). Dev-standards §8.1 required every .NET service to use four projects (`Api`, `Application`, `Domain`, `Infrastructure`). That fits Order and Payment, whose value is in invariants and state machines, but it is ceremony for Media, which mostly issues SAS URLs and records status. Cart, Notification and Search-AI are similar: projections, I/O and pipelines. One builder (risk R-1) also needs a fast and consistent way to create each new service, in three languages, with the same health, telemetry, error, auth, outbox and contract-test wiring.

## Decision
- **Same outer shell for every service.** Folders, `/health` and `/ready`, OpenTelemetry, error envelope, JWT validation, outbox and inbox tables, contract runner, Dockerfile, CI workflow (repo-structure §3).
- **Two inner shapes:**
  - **rich** (ports and adapters with a pure domain layer): Order, Payment, Catalog, Identity, Inventory.
  - **lean** (vertical feature slices, no separate domain layer): Media, Cart, Search-AI, Notification.
- **Promotion rule:** a lean service that grows invariants across several entities moves to rich. The outer shell does not change.
- **Layout** of each shape per stack is defined in [`repo-structure.md`](../repo-structure.md) §4. Boundaries are enforced by NetArchTest (.NET), depguard (Go) and import-linter (Python).
- **Templates:** one Copier template per stack (`templates/dotnet-service`, `go-service`, `python-service`, `vue-app`, `dotnet-function`), with `shape` as a question. Templates render from the repo root, so one run also creates the service's OpenAPI, proto, Gherkin and CI files. Shared files (AppHost, solution, `go.work`, uv workspace) are not edited by the template; Copier prints the lines to add.
- **Aspire** stays in top-level `apphost/` with ServiceDefaults beside it (repo-structure §5).

## Options considered
- **Rich and lean, same shell (chosen)** — each service carries the structure its problem needs; tooling sees one shape from outside.
- **One four-layer shape for all** — uniform, but adds empty layers and mapping code to I/O services and hides where the real domain work is.
- **Lean everywhere** — least code, but the saga, reservation and payment rules (CON-7) lose the isolation that makes them easy to test without infrastructure.
- **`dotnet new` plus cookiecutter per stack** — native for .NET, but two tools, and neither updates an already generated project. Copier's `update` keeps services in step with template fixes.

## What it teaches
Hexagonal architecture and vertical slice architecture side by side in the same system, and when each is worth its cost. Template-driven scaffolding with Copier, including updating generated projects.

## Consequences
Easier: a new service is one command and passes CI on its first commit; lean services stay small; rich services keep pure, fast domain tests. Harder: two shapes to know per stack; the templates need upkeep and their own CI check (generate a sample per stack and shape, build and test it). Dev-standards §8.1 to §8.3 are amended to describe both shapes. Revisit if a lean service keeps gaining invariants without being promoted.
