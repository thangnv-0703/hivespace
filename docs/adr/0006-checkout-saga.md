# ADR-0006: Orchestrated checkout saga in Order

**Status:** Accepted
**Date:** 2026-11-15
**Requirements addressed:** FR-CHK-001..013, FR-INV-002..008, FR-PAY-001, FR-PAY-003, FR-PAY-005, FR-PAY-010, FR-ORD-001, FR-ORD-002, NFR-REL-002, NFR-PERF-004, NFR-OBS-001, NFR-MNT-007, CON-7, UC-011, UC-012; OQ-13, OQ-16

## Context
One buyer action creates one order per seller across services that each own their data. It must be all-or-nothing (OQ-16, A-4), idempotent, compensating, and answer the buyer within 3 seconds (NFR-PERF-004).

## Decision
The **Order service orchestrates** a persisted saga (state: STARTED, RESERVED, ORDERS_CREATED, PAYMENT_INITIATED, COMPLETED, COMPENSATING, COMPENSATED, FAILED).
1. Insert a checkout row keyed by the `Idempotency-Key` (unique). A repeat returns the stored outcome.
2. Read the selected lines from Cart (ADR-0017), then fetch authoritative prices from Catalog; compute shipping fee and totals through the isolated **Pricing** module.
3. **One batch reserve call (gRPC, ADR-0019) to Inventory** for all lines: a single database transaction, all or nothing. A shortfall returns the insufficient lines and creates no orders.
4. In **one local transaction** create the N orders (one per seller) with price snapshots and an outbox event.
5. COD: orders become placed. Online: call Payment; orders stay awaiting payment with a payment deadline (OQ-13: 15 min proposed).
6. After success publish `CheckoutCompleted`; Cart removes the purchased lines (FR-CHK-010). Payment results arrive as events (`PaymentSucceeded`, `PaymentFailed`); expiry is a sweeper (ADR-0012).
Compensation is a single idempotent release-by-checkout call plus cancelling the orders. Each step has a 10 s timeout (OQ-13). The saga row records progress after every step so a restarted process resumes or compensates. A `COMPENSATING` saga is retried by a sweeper.

## Options considered
- **Orchestration in Order with a synchronous reserve (chosen)** — buyer gets an immediate, explicit answer; compensation is one call because reserve is atomic; easy to trace and to fault-inject.
- **Choreography (each service reacts to events)** — looser coupling, but the buyer answer is asynchronous, failure paths are scattered, and the NFR-REL-002 proof is harder.
- **Per-line reserve calls** — more compensation steps and partial states for no benefit.
- **Two-phase commit** — not available across these stores and against the learning goal.

## Consequences
Easier: one place to read the checkout, a clear trace, a small compensation surface. Harder: Order depends synchronously on Catalog, Inventory and Payment at checkout (failure means a clean retry message, not a partial order). The same saga row is where adding discounts later plugs into Pricing (NFR-MNT-007). Revisit if checkout grows many more steps.
