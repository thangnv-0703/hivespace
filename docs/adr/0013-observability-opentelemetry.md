# ADR-0013: Observability with OpenTelemetry

**Status:** Accepted
**Date:** 2026-11-15
**Requirements addressed:** EIR-COM-004, NFR-OBS-001, NFR-OBS-002, NFR-OBS-003, NFR-COST-001, NFR-SEC-009, NFR-MNT-003; dev-standards §11

## Context
One checkout must be one trace across the gateway and every service it touches, including hops through the broker. Three stacks must emit compatible telemetry. Cloud telemetry must stay in free allowances.

## Decision
- OpenTelemetry traces, metrics and logs in every service. .NET gets them from ServiceDefaults; Go and Python use the OTel SDKs with the same resource attributes, verified by a CI smoke check.
- **Trace context propagation:** W3C `traceparent` over HTTP and in the event envelope; consumers continue the trace from the envelope.
- Structured JSON logs with a correlation ID; personal data and secrets redacted (dev-standards §5).
- `/health` (liveness) and `/ready` (dependencies) on every service.
- **Locally:** the Aspire dashboard. **Demo:** OTLP export to Application Insights with sampling and a daily cap.
- k6 reports are committed (NFR-OBS-003).

## Options considered
- **OpenTelemetry to Application Insights (chosen)** — vendor-neutral instrumentation, Azure-native sink inside a free allowance.
- **Self-hosted Grafana stack** — rich, but always-on containers break the cost cap.
- **Application Insights SDK directly** — ties instrumentation to one vendor and is weaker in Go and Python.

## Consequences
Easier: one trace per checkout is a demo asset; the sink can be swapped. Harder: a context-propagation test through the broker per stack. Revisit if telemetry volume threatens the free allowance.
