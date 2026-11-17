# ADR-0004: Payment callback idempotency

**Status:** Accepted
**Date:** 2026-11-15
**Requirements addressed:** FR-PAY-005, FR-PAY-007, FR-PAY-008, FR-PAY-009, FR-PAY-010, FR-PAY-011, FR-PAY-014, NFR-REL-003, NFR-REL-005, NFR-MNT-002 (payment idempotency), UC-012

## Context
Providers retry callbacks and may send them out of order or after a payment window closed. Three deliveries must produce one effect (NFR-REL-003). Callbacks are the only inbound external endpoint besides the gateway (EIR-COM-002).

## Decision
In one database transaction the Payment service:
1. verifies the signature first (failure returns 401 and changes nothing);
2. inserts a row into `provider_events` with a unique key `(provider, providerEventId)`;
3. if the insert conflicts, returns 200 and applies nothing (acknowledged duplicate);
4. otherwise changes payment status with a **compare-and-set** (`UPDATE ... WHERE status = 'PENDING'`) and writes the status-history row and the outbox event.
Rules: unknown payment returns 404 and is logged (FR-PAY-009). A success on a payment already `CANCELLED` (set by `OrderCancelled`) triggers a refund through the provider interface (FR-PAY-011). The mock provider delivers real signed HTTP callbacks to the real endpoint, including duplicates and delays, so the test exercises production code. Order's reaction to `PaymentSucceeded` is also guarded by the order state, so a replayed event cannot move an order twice.

## Options considered
- **Unique provider-event row plus compare-and-set (chosen)** — correct under concurrent duplicates, no distributed lock.
- **Check-then-act in application code** — races under concurrent duplicate deliveries. Rejected.
- **Distributed lock per payment** — adds infrastructure and failure modes for no gain over the database constraint.

## Consequences
Easier: the duplicate test is deterministic, and the same pattern is reused for inbox processing. Harder: each provider must expose a stable event ID (the VNPay sandbox would use its transaction reference). Revisit if a provider has no stable ID.
