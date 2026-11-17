# ADR-0017: Cart as a separate service in Go

**Status:** Accepted
**Date:** 2026-11-16
**Requirements addressed:** FR-CART-001..010, FR-CHK-001, FR-CHK-009, FR-CHK-010, FR-CUR-003, NFR-PERF-004, NFR-REL-005, NFR-MNT-008, CON-3, CON-4, UC-010, UC-011

## Context
The cart was allocated to Order in the first draft of the architecture. It is a separate concern with its own data (per-user lines), its own rules (FR-CART-001..010) and a different load pattern (frequent small writes). The builder accepts the extra service and wants Go used where it fits.

## Decision
A **Cart service in Go** with its own database.
- Owns cart lines per user (variant ID, quantity, price snapshot with currency) and the add-time rules: quantity limit 99, own-shop rejection, sold-out and deactivated rejection, advisory stock check (FR-CART-001..003, FR-CART-009).
- **Composes the cart display** by batch calls to Catalog (variant status, price, shop) and Inventory (availability), in parallel with timeouts; flags unavailable, over-stock and price-changed lines (FR-CART-008).
- **Checkout:** Order reads the selected lines from Cart at the start of the saga and re-fetches authoritative prices from Catalog, so cart prices are never trusted. After success Order publishes `CheckoutCompleted`; Cart removes the lines idempotently (inbox dedupe), and the checkout response lists the purchased lines for the UI.
- **Store: MongoDB** (ADR-0018). One document per user with a TTL index; event dedupe by a bounded `processedEventIds` list updated in the same single-document write that removes the lines. Cart publishes no events, so it has no outbox. Official Go driver behind a repository interface; integration tests with a MongoDB Testcontainer.
- Uses the Go telemetry and Service Bus adapters shared with Inventory, and a Go `Money` type (ADR-0010). Contract tests with godog.

## Options considered
- **Separate Go service (chosen)** — clean ownership, scales independently, good fit for goroutine fan-out, reuses Go libraries.
- **Module inside Order** — one fewer service and no hop in checkout; mixes two concerns and keeps all cart load on Order.
- **Separate .NET service** — shares ServiceDefaults, but gives Go nothing and .NET more.

## Consequences
Easier: clear cart ownership and independent scaling. Harder: a ninth service (R-1), one extra synchronous call in the checkout budget (NFR-PERF-004), and cart line removal is eventually consistent (mitigated by the response listing purchased lines). Cut order: Cart merges back into Order as a module if the schedule demands it.
