# ADR-0012: Timers and expiry under scale-to-zero

**Status:** Accepted
**Date:** 2026-11-15
**Requirements addressed:** FR-INV-007, FR-PAY-010, FR-ORD-009, FR-CHK-007, FR-CHK-006, FR-AUTH-005, NFR-COST-001, NFR-PORT-002, NFR-REL-006, UC-012, UC-016, UC-019; OQ-13, OQ-14

## Context
Several rules are time-based: reservation hold, payment window, order auto-completion, saga step timeout, verification-link lifetime. Container Apps that scale to zero run no background loop while idle, and an always-on replica would use most of the free grant.

## Decision
- **Every time-based rule is stored as a deadline in the database** (`expires_at`, `payment_deadline`, `auto_complete_at`), never as an in-memory timer.
- **Correctness does not depend on a timer firing.** Readers and writers treat an expired record as expired: Inventory releases expired holds before reserving (ADR-0007); Order treats an overdue awaiting-payment order as cancelled when it is read or acted on.
- **Sweepers** (background services) run a catch-up pass on startup and on a short interval while the app is awake. Work is claimed with `FOR UPDATE SKIP LOCKED` so replicas do not clash.
- Apps wake on incoming messages (KEDA scaler on the Service Bus subscription) so queued work is processed.
- No always-on replicas. During the 1-hour daily window the stack is warm and sweepers run normally. Values (hold 15 min plus grace, payment window 15 min, step timeout 10 s) are configuration (FR-INV-012).

## Options considered
- **Deadlines in the database, lazy expiry, catch-up sweeps (chosen)** — correct across restarts and scale-to-zero, no extra cost.
- **Always-on replica for Order and Inventory** — simple, but exceeds the cost cap.
- **A timer-triggered Function that calls sweep endpoints** — works, adds a component; little value with a 1-hour window. May be added later.

## Consequences
Easier: restart safety and deterministic tests (move the clock). Harder: every expiry path needs its lazy-read twin and a test. A record can stay "expired but not yet marked" while the system is idle; this is invisible because reads apply the rule. Revisit if the demo window becomes long or traffic continuous.
