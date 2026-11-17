# ADR-0001: Separate services, database per service

**Status:** Accepted
**Date:** 2026-11-15
**Requirements addressed:** CON-3, CON-4, CON-7, NFR-MNT-002, NFR-SCAL-001, NFR-REL-002, FR-ROLE-002, FR-CHK-003..006; OQ-9 (resolved), OQ-10 (resolved)

## Context
The SRS lists about six services (Identity, Catalog, Order, Payment, Inventory, Search+AI) and a separately owned Notification capability. OQ-9 asked modular monolith or services; CR-2 settled on separate services. The project's purpose is to demonstrate a distributed checkout saga, atomic inventory under contention and idempotent callbacks, none of which is credible inside one process. The tension is portfolio value against solo capacity (CON-1) and cost (CON-2).

## Decision
Separate deployable services, each with its own database (PostgreSQL by default, MongoDB for Cart, Catalog's store open; see ADR-0018) and no cross-database access:
Gateway/BFF, Identity, Catalog, Order, Payment, Inventory (Go), Cart (Go, ADR-0017), Search-AI (Python), Notification (Python, ADR-0016), Media, plus one Azure Function (image processor).
Capability allocation the SRS left open:
- Cart in its own **Cart** service (Go, ADR-0017); shop, address book and roles in **Identity**; reviews in **Catalog**; mock shipping in **Order**; mock payment in **Payment**; stock in **Inventory**; media records in **Media**.
- Notification is its own deployable (resolves OQ-10), fed by events and a contact projection.

## Options considered
- **Separate services (chosen)** — real boundaries, per-service stacks (Go, Python), the saga is real. Cons: more to build and run; distributed failure modes.
- **Modular monolith** — far cheaper to build and run; the saga would be in-process and prove little. Lost on the project's purpose and on CON-3 (three stacks).
- **Fewer services (merge Payment into Order, Notification into Order)** — cuts two deployables, but weakens the "payment idempotency" and "separately owned notification" claims (PRD P0-14).

## Consequences
Easier: independent stacks, clear ownership, honest saga and idempotency tests. Harder: nine deployables for one builder (risk R-1), contracts and conformance checks across three languages, eventual consistency in projections (Catalog's purchased items, Notification's contacts, Search-AI's read model). The cut order in the architecture document protects the hero flow. Revisit if schedule slips more than two weeks at the walking-skeleton milestone.
