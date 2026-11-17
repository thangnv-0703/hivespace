# ADR-0003: Search store is PostgreSQL full text plus pgvector

**Status:** Accepted
**Date:** 2026-11-15
**Requirements addressed:** CON-5, FR-SRCH-001..011, FR-NLS-001..009, FR-CAT-018, FR-NLS-007, NFR-PERF-002, NFR-PERF-003, NFR-SCAL-002, NFR-L10N-003, NFR-COST-001, NFR-MNT-002 (search store), UC-007, UC-008

## Context
Keyword search must handle Vietnamese with and without diacritics (FR-SRCH-002). NL search needs vector similarity under hard filters. CON-5 rules out Azure AI Search and requires pgvector. Scale is small: 10,000 products (NFR-PERF-002, NFR-SCAL-002).

## Decision
Search-AI owns a **denormalized product read model** in its own Postgres database, built from `ProductChanged`, `ShopChanged`, `ReviewChanged` and `CategoryTreeChanged` events, with a monotonic `version` so stale events are ignored.
- Keyword: `unaccent` plus a `simple` text-search configuration (tsvector) and `pg_trgm` for tolerance, ranked with `ts_rank`.
- Semantic: a `vector` column per product with the **embedding model ID stored per row**; queries with a different model are rejected.
- Exact vector scan inside the filtered set for up to about 10,000 products; add an HNSW index only if measurements require it.
- NL interpretation runs in this service (ADR-0014), with fallback to keyword.

## Options considered
- **Postgres full text plus pgvector (chosen)** — one store, one backup, diacritics through `unaccent`, filters and vectors in one query. Cons: relevance tuning is manual; Vietnamese word segmentation is not available (trigrams compensate).
- **Dedicated search engine (OpenSearch, Meilisearch)** — better relevance tooling; another store to run and pay for. Lost on CON-5, cost and operations.
- **Azure AI Search** — excluded by CON-5.

## Consequences
Easier: one technology, SQL filters with vector ranking, cheap demo. Harder: no built-in Vietnamese analyzer, so the evaluation set (FR-NLS-011) decides if relevance is acceptable. Index freshness depends on the broker (target 60 s). Revisit above roughly 100,000 products or if the evaluation shows keyword quality is the blocker.
