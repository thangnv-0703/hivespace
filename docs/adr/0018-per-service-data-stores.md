# ADR-0018: Per-service data stores: PostgreSQL by default, MongoDB for Cart, Catalog open

**Status:** Accepted for Cart and for the PostgreSQL services; **Proposed (open)** for Catalog and for the MongoDB demo host
**Date:** 2026-11-16
**Requirements addressed:** FR-CART-001..010, FR-CHK-010, FR-CAT-001..005, FR-CUR-003, CON-4, CON-5, NFR-REL-005, NFR-REL-007, NFR-COST-001, NFR-MNT-006, NFR-MNT-008

## Context
Every service owns its own database (ADR-0001). The first draft used PostgreSQL everywhere. The builder asked whether any service suits NoSQL, and wants Catalog's main store decided after the crawl-data approach (OQ-1) is reviewed.

## Decision
- **PostgreSQL by default** for Identity, Order, Payment, Inventory, Search-AI (pgvector and full text, CON-5) and Media and Notification. Their correctness rests on relational transactions, unique constraints and conditional updates (the saga, reservations, payment idempotency, OpenIddict on EF Core).
- **MongoDB for Cart.** One document per user (`_id` is the user ID) holds the lines (variant ID, quantity, price snapshot as `{amount, currency}`, integer minor units per ADR-0010), a version for optimistic concurrency, and a TTL index on the last-touched date so abandoned carts expire.
  - **Event dedupe without a transaction:** the document keeps a bounded `processedEventIds` list. Consuming `CheckoutCompleted` is one atomic single-document update that removes the purchased lines and records the event ID, which satisfies NFR-REL-005. Cart publishes no events, so it has no outbox.
  - **Schema and indexes:** MongoDB has no migrations. Cart creates its collection, validator and indexes at startup from versioned scripts in the repository; restore from scripts stays within 10 minutes (NFR-REL-007).
  - Cart code depends on a repository interface, not on the driver, so the store stays swappable.
- **Catalog: open.** Decide after the crawl review, using these criteria:
  - how variable and nested the crawled product attributes are (very variable or nested favours documents);
  - the need for relational features: category tree, unique shop names, rating aggregates, reviews;
  - outbox atomicity: a Postgres transaction, or change streams on a MongoDB replica set;
  - total cost across Neon and the MongoDB host.
  PostgreSQL with JSONB is the safe default. Until the decision, Catalog keeps its persistence behind a repository interface and its event contracts stay store-neutral (ADR-0002), so the swap is local.
- **MongoDB demo host: open.** Candidates are MongoDB Atlas free tier and Cosmos DB for the MongoDB API. Decide with the hosting decision (ADR-0005).
- **Local:** a plain MongoDB container started by the AppHost. Azure has no MongoDB emulator, so this is a documented exception to the "Azure emulators locally" rule, as with PostgreSQL (ADR-0015).

## Options considered
- **PostgreSQL for all services** — one technology, one host, no new tooling. Cart's document shape and TTL are less natural, but this is a valid fallback.
- **Cosmos DB (NoSQL API) or Azure Table Storage for Cart** — Azure-native with emulators or a free tier. Not chosen: the builder prefers MongoDB.
- **MongoDB for Cart (chosen)** — fits a per-user aggregate with high write frequency and expiry.
- **MongoDB for Catalog now** — plausible for crawled, variable product documents, but categories, reviews and rating aggregates need design, so the decision waits for the crawl review.

## Consequences
Easier: a natural model for Cart, expiry for free, single-document atomic updates. Harder: a second store type to run, test (Testcontainers) and restore; an undecided host that affects cost (risk R-13); any future multi-document transaction needs a MongoDB replica set, so Cart must stay within single-document updates. Revisit when the crawl review is done.
