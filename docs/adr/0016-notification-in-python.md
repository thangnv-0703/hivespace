# ADR-0016: Notification service in Python

**Status:** Accepted
**Date:** 2026-11-15
**Requirements addressed:** FR-NOTIF-001..015, FR-LOC-005, NFR-REL-004, NFR-REL-005, NFR-L10N-003, NFR-COST-004, NFR-MNT-006, CON-3, UC-018; OQ-10 (resolved)

## Context
Notification consumes domain events, renders vi and en HTML emails, sends them through `IEmailSender` and retries failures. It is never on a user-facing request path and exposes only health endpoints, so it needs no JWT validation. CON-3 had it in .NET by default. The builder knows .NET, Go and Python and wants each stack used where it fits.

## Decision
Build Notification in **Python** as its own service and its own image, separate from Search-AI so it carries no ML dependencies. Jinja2 for templates, Babel for message catalogs (shared vi and en catalog from `contracts/`), the Python outbox/inbox and Service Bus adapters shared with Search-AI, pytest and pytest-bdd for tests. Resend and Mailpit stay behind `IEmailSender`.

## Options considered
- **Python (chosen)** — strong templating and i18n, I/O-bound work suits asyncio, adapters already needed for Search-AI, no JWT code needed here.
- **Go** — fastest cold start and a second Go service, but cold start does not matter off the request path and its template and i18n tooling is weaker.
- **.NET** — shared libraries and ServiceDefaults; adds nothing Notification needs and keeps the load on the stack that already has the most services.

## Consequences
Easier: HTML email rendering and translation; Python gets two services, Go keeps Inventory, .NET keeps the transactional core. Harder: Python runtime is heavier and typing is weaker (mypy strict applies); a shared Python package for the adapters must be maintained. If the cut order in the architecture document is triggered, Notification could fold into Order, which would put it back in .NET.
