# ADR-0002: Event bus abstraction, outbox and inbox

**Status:** Accepted
**Date:** 2026-11-15
**Requirements addressed:** EIR-COM-003, NFR-REL-005, NFR-MNT-002 (message broker), NFR-MNT-006, FR-NOTIF-009, FR-NOTIF-010, FR-CAT-018, FR-NLS-007, FR-PAY-007; OQ-5 (resolved subject to R-2)

## Context
Services exchange domain events (EIR-COM-003). Consumers must tolerate at-least-once delivery (NFR-REL-005). The builder intends Azure Service Bus in production and wants the option to switch brokers. Three stacks must interoperate. Local runs must use an Azure emulator (ADR-0015).

## Decision
- One broker-neutral contract: an **event envelope** (`eventId`, `type`, `version`, `occurredAt`, `correlationId`, `traceparent`, `data`) as JSON, with a schema per event type in `contracts/events/`.
- An **`IEventBus`** interface (publish, subscribe by type) with implementations for **Azure Service Bus** (default; emulator locally) and **RabbitMQ** (optional). Selection is configuration. Go (Inventory) and Python (Search-AI and Notification, ADR-0016) get thin equivalent adapters over their SDKs (AMQP 1.0 for Service Bus; both have SDKs).
- Publishing uses a **transactional outbox** (state change and outbox row in one local transaction; a relay publishes and marks sent). Consuming uses an **inbox** table keyed by `(consumer, eventId)` written in the same transaction as the handler's state change.
- Topics per event family with one subscription per consuming service; dead-letter after a bounded number of attempts.
- Where a service has no relational transaction (Cart on MongoDB, ADR-0018), the inbox is a bounded list of processed event IDs inside the document updated by the handler, and the service publishes no events, so it has no outbox.
- Hand-written, small outbox and inbox in a shared .NET library and in the Go and Python services, rather than a framework, so behaviour is identical in all three stacks.

## Options considered
- **Own abstraction over Service Bus and RabbitMQ (chosen)** — meets the switch requirement; a little code per stack. Cons: the abstraction must stay small; broker-specific features (sessions, ordering) are not used.
- **MassTransit or Wolverine in .NET** — less code in .NET, but Go and Python would not share its envelope; adds licensing and upgrade risk.
- **Service Bus directly, no abstraction** — least code; ties tests and local runs to one broker and loses the switch.

## Consequences
Easier: swapping brokers, duplicate-delivery tests, deterministic local runs. Harder: three small implementations plus a conformance check. Ordering is not guaranteed, so snapshots carry a `version` and consumers ignore older ones. Cost risk: Service Bus topics need the Standard tier (R-2); the abstraction is the escape hatch to a free-hosted RabbitMQ. Revisit if event volume or ordering needs grow.
