# ADR-0007: Inventory atomic reservation and lazy expiry

**Status:** Accepted
**Date:** 2026-11-15
**Requirements addressed:** FR-INV-001..012, FR-CHK-004, FR-CHK-005, FR-PAY-010, NFR-SCAL-001, NFR-REL-001, NFR-PERF-005, NFR-OBS-003, CON-7, UC-011, UC-019; OQ-13

## Context
Available stock must never go below zero under any interleaving (FR-INV-004), proven with 200 concurrent buyers on 10 units. Reservations and releases must be idempotent. The service is Go and its container may be scaled to zero, so a timer-only expiry would not run (ADR-0012).

## Decision
- Tables: `stock(variant_id, on_hand, reserved)`, `reservations(checkout_id, line_id, variant_id, qty, state, expires_at)`, `movements`.
- **Reserve** (a gRPC call, ADR-0019) is one transaction for a whole checkout: rows ordered by `variant_id` (no deadlocks), each via `UPDATE stock SET reserved = reserved + $q WHERE variant_id = $v AND on_hand - reserved >= $q`; zero rows updated means insufficient and the whole transaction rolls back. A unique `(checkout_id, line_id)` makes repeats return the existing reservation (FR-INV-005). No application-level locks.
- **States:** HELD, then COMMITTED on `OrderPlaced`, then CONSUMED on `OrderShipped`; or RELEASED (cancel, compensation, expiry). Release moves the state only once (FR-INV-008).
- **Hold time** = payment window plus a short grace (so a payment accepted at the edge does not lose its stock).
- **Lazy expiry:** a reserve or availability read first releases expired HELD rows for the variants involved, in the same transaction; a sweeper also releases expired rows on startup and periodically.
- **On-hand changes** reject going below reserved (FR-INV-010). Initial stock is created from `VariantCreated`; edits arrive through Catalog.

## Options considered
- **Database conditional update (chosen)** — correctness enforced by the database; simple and fast.
- **Row lock (`SELECT FOR UPDATE`) then update** — also correct, holds locks longer under contention.
- **Redis or in-process counters** — fast but a second source of truth; fails the "no application-level locks" rule.

## Consequences
Easier: the oversell proof is a database property, and the k6 run is straightforward. Harder: hot variants contend on one row; acceptable at this scale and measured by NFR-PERF-005. Revisit if one variant needs more than a few hundred reservations per second.
