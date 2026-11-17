# ADR-0010: Money model and isolated pricing pipeline

**Status:** Accepted
**Date:** 2026-11-15
**Requirements addressed:** FR-CUR-001..005, FR-CAT-002, FR-CAT-005, FR-CHK-004, FR-CHK-009, FR-LOC-006, NFR-MNT-007, NFR-MNT-008, NFR-MNT-009, NFR-L10N-001, A-1

## Context
VND is the only enabled currency, but money must be modelled as amount plus currency so another currency can be added by configuration (NFR-MNT-009). Discounts are out of scope but must not require a checkout rewrite (NFR-MNT-007).

## Decision
- Money is `{ amount, currency }` everywhere: code, database, APIs, events, logs. `amount` is an **integer in minor units** (VND exponent 0 means whole dong); never a float or bare number.
- Database: `amount bigint` plus `currency char(3)`. OpenAPI: a shared `Money` schema `$ref`.
- A `Money` type per language; arithmetic across currencies throws; one currency per checkout and order (FR-CUR-004).
- A **currency table** (code, symbol, exponent) is configuration. Formatting per language lives in the UI and in email templates (FR-CUR-002).
- The Order service has an isolated **Pricing** module that computes line totals, shipping and order total through an ordered list of price components; adding discounts later means adding a component.
- Payment calls send the currency (FR-CUR-005).

## Options considered
- **Integer minor units plus currency code (chosen)** — exact, simple, standard.
- **Decimal amounts** — fine for display but invites rounding differences across three languages.
- **Bare integers with an implicit VND** — violates NFR-MNT-008 and blocks a second currency.

## Consequences
Easier: no rounding bugs, currency additions are configuration. Harder: a custom lint or analyzer per language to ban bare money (dev-standards §5). Cart, checkout, order and inventory code must not name a specific currency (architecture test). Revisit when a second currency is added (OQ-34).
