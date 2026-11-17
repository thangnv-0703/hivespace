# HiveSpace: Brainstorm Notes

Date: 2026-11-14 · Status: draft, not final decisions

## Context

A Shopee-inspired e-commerce side project with four goals:
- Learn the e-commerce domain.
- Practise software architecture and design, Azure and AI.
- Build microservices to strengthen backend skills.
- Serve as a CV piece for middle/senior backend or fullstack roles.

| Constraint | Value |
|---|---|
| MVP target | Feb 2027 (about 13 weeks) |
| Capacity | about 30 h/week, solo (about 390 h) |
| Stack | .NET, Go, Python |
| Budget | about $5/month for Azure and AI combined |

**Frontend:** `hivespace.web` (Vue 3 monorepo: admin, seller, buyer, shared) is **a reference, not a dependency**. Reuse pages and components where convenient. The backend, product model and data pipeline are owned by HiveSpace and must not be shaped around the FE's types or its catalog-import design.

## Key ideas

1. **Depth over breadth.** Cloning Shopee's feature surface produces a wide, shallow project. A thin vertical slice with real engineering problems is a stronger CV story.
2. **Right tool per job.** .NET for the transactional core (identity, catalog, order, payment). Go for the hot path (inventory and flash sale). Python for AI and data (search, summaries, ETL).
3. **Modular boundaries, about six services, not a service explosion.** Identity, Catalog, Order, Payment, Inventory/Flash-sale (Go), Search+AI (Python). Notification lives inside Order until there is a reason to extract it.
4. **Hard problems are the value.** The checkout saga (one checkout splits into an order per seller), stock reservation with zero overselling, and idempotent payment webhooks. Prove the flash-sale claim with a k6 load test.
5. **AI as a feature, not decoration.** Natural-language Vietnamese search is the hero: LLM parses the query into filters, and multilingual embeddings handle semantic match. Include a small eval set (about 50 queries) with a measured hit rate. Listing assist (#2) and review summaries (#4) are secondary. Review summaries are a batch job, not request-time.
6. **The $5 budget is a design constraint and a CV story.** Heavy work runs locally in docker compose (ETL, million-order generator, k6, batch embeddings). Only a small demo slice is deployed to Azure (scale-to-zero Container Apps). Use pgvector instead of Azure AI Search, and avoid AKS. Record each choice as an ADR.
7. **Engineering signals that read as senior:** IaC (Bicep or Terraform), CI/CD, OpenTelemetry tracing across services, ADRs, an architecture diagram and a short demo video.

## Strongest direction

Build the transactional core first and layer AI on top of working data. Suggested order:

| Weeks | Focus |
|---|---|
| 1–2 | Walking skeleton: repo layout, Identity, gateway, CI, one service deployed |
| 3–4 | Seller onboarding, Catalog, listings without AI, seed data |
| 5–7 | Cart, checkout saga, Inventory (Go), mock payment, fulfilment |
| 8–9 | Flash sale plus k6 proof |
| 10–12 | AI search, listing assist, review summaries |
| 13 | Large-scale data generator, load test, docs, demo |

**Cut order if behind:** shrink the generator (1M to 100k orders), replace VNPay with a mock payment, make listing-assist text-only. Never cut the saga or flash-sale correctness.

## Riskiest assumption

That 7 features plus microservices plus AI plus data fit in about 390 hours alone. The mitigation is the cut order above, and starting with the riskiest backend work (saga, inventory) before AI.

## Open questions

1. **Data source.** Crawl a Vietnamese marketplace (legal and terms risk, keep private) vs LLM-generated Vietnamese catalog vs a public dataset. Whatever is chosen, define HiveSpace's own product schema first.
2. **Product model.** Shopee-style category, attributes, variants and SKUs, or something simpler? Decide the schema and ID strategy (UUID vs int) up front.
3. **Azure free account.** Which free offers are available (database hours, Container Apps grant, credits)? Verify current terms before designing infrastructure.
4. **LLM and embeddings.** Which free-tier or local model for query parsing and listing assist, and which multilingual embedding model on CPU?
5. **Auth.** Own Identity service vs an external provider. How much of Identity is in scope?
6. **Payment.** VNPay sandbox or mock first?
7. **FE reuse.** Which FE pages are worth reusing (buyer storefront, seller listing, checkout), and which are ignored? Where do specs live (the FE expects `../hivespace.spec`, which doesn't exist)?
8. **Messaging.** Cheapest broker for the saga and events on a $5 budget (Service Bus Basic, or RabbitMQ locally and something lighter in the cloud)?

## Next steps

1. Decide the data source and write HiveSpace's own product model (entities, ID strategy, attributes and variants).
2. Draft the service map and one ADR on modular monolith vs microservices, with the six-service boundary above.
3. Pick the hero flow and write acceptance criteria for it: browse, search, cart, checkout, pay, order tracking.
4. Confirm Azure free-tier offers and set a cost guardrail (budget alert).
5. Skim the FE for reusable buyer and seller pages and list them as "reuse / adapt / ignore".
6. Set up the repo, CI and walking skeleton (week 1–2).

## Parked ideas

- Vouchers and coupons, live chat, live-streaming, coins and loyalty.
- Admin features such as KYC queue, disputes and scheduled jobs (build only if time allows).
- Image-based product search.
- Recommendation engine beyond "similar products".
- Kubernetes (AKS) deployment, revisit after Feb 2027.
