# ADR-0014: AI provider interfaces; runtime undecided

**Status:** Proposed (blocked on OQ-4, decide before week 10)
**Date:** 2026-11-15
**Requirements addressed:** EIR-SW-001, EIR-SW-002, FR-NLS-001..012, FR-REV-008, FR-LAS-001, NFR-PERF-003, NFR-SEC-010, NFR-COST-001..003, NFR-MNT-006, NFR-REL-008, UC-008, UC-021; OQ-4, OQ-15

## Context
NL search needs query interpretation (LLM) and embeddings. The model and runtime are undecided: a hosted free-tier API, or a CPU model inside the container. The demo runs on a $5 budget and scale-to-zero. Vectors created at index time and at query time must come from the same model.

## Decision (partial)
- The Search-AI service uses **`ILlmProvider`** and **`IEmbeddingProvider`** interfaces; implementations are selected by configuration. Calling code never references a vendor SDK (NFR-MNT-006).
- **Deterministic first:** a rule-based parser extracts Vietnamese price shorthand and bounds ("300k", "dưới 300k", "1tr") before any LLM call (FR-NLS-002), so price constraints survive an LLM outage.
- **LLM output** is parsed into a fixed Pydantic model; invalid output, a 3 s timeout, or an exhausted usage cap falls back to keyword search (FR-NLS-005, NFR-COST-002, NFR-SEC-010).
- **Model identity** is stored with every embedding; a query is embedded only with the same model, otherwise it falls back to keyword and logs.
- Batch embedding of seed data runs locally (NFR-COST-003).
- **Not decided:** the model, and whether embeddings run in-container on CPU or through a hosted API. Record the choice and its measured hit rate here when OQ-4 closes.

## Options considered (for the open part)
- **Hosted free-tier API** — small image, fast cold start, same model locally and deployed; free-tier limits and vendor dependency.
- **CPU model in the container** — no quota or vendor; image around 1 GB, slow cold start against the 5 s target.

## Consequences
Easier: swapping providers and measuring them against the keyword baseline with the same evaluation set. Harder: the final choice affects image size and cold start (R-4) and must precede the first deploy of Search-AI.
