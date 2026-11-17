# HiveSpace MVP: Product Requirements Document

Date: 2026-11-14 · Status: draft v1 · Owner: solo builder · Target: Feb 2027 (~13 weeks, ~30 h/week, ~390 h)
Inputs: [brainstorm-notes.md](brainstorm-notes.md), [competitive-brief.md](competitive-brief.md)

## 1. Problem statement

HiveSpace is a Shopee-inspired marketplace built as a learning and portfolio project. The "problem" is the builder's: cloning a marketplace's feature surface yields a wide, shallow demo that says little about engineering ability, and a solo builder with ~390 hours cannot match years of accumulated marketplace rules anyway.

The product problem it uses as a vehicle is real but narrow. Vietnamese buyers who know what they need but not the product name are poorly served by keyword search on Shopee and TikTok Shop (observed gap, not proven demand). Sellers and platforms also struggle with correct multi-seller checkout and stock under flash-sale load.

**If we get scope wrong:** the project stalls at ~60% of seven half-built features, nothing demos end to end, and it fails its main purpose (a credible CV piece for middle/senior backend or fullstack roles).

## 2. Goals

Goals are outcomes for the builder. There are no market or revenue goals.

| # | Goal | Why |
|---|---|---|
| G1 | **One complete, working hero flow:** register, search, cart, split checkout, pay (COD or mock online), track, review. A seller can list, fulfil and see orders. | A thin vertical slice beats breadth. |
| G2 | **Prove the hard backend problems:** a checkout saga that splits one checkout into one order per seller, zero overselling under concurrent load, idempotent payment webhooks. | These are the senior-level signals. |
| G3 | **Ship one AI feature that is measured, not decorative:** natural-language Vietnamese search with a ~50-query eval set and a recorded hit rate. | The only buyer-visible differentiator. |
| G4 | **Stay inside the budget and cap complexity:** ~$5/month for Azure and AI combined, about six services, heavy work runs locally. | The budget is a design constraint and a CV story. |
| G5 | **Leave a reviewable engineering trail:** ADRs, IaC, CI/CD, OpenTelemetry tracing, an architecture diagram and a short demo video. | Recruiters read the repo before running it. |

## 3. Non-goals

This list is binding until the MVP ships. Revisiting an item means something else leaves the plan.

| Not building | Why |
|---|---|
| **Vouchers, coupons, free-shipping codes** (at most one seller-level code, only if ahead of schedule) | Rules vary by country and change often; large surface, little learning. First to revisit after MVP. |
| **Coins, loyalty, gamification, live chat, livestream or video commerce, ads/promoted listings** | Engagement features need traffic and budget that do not exist. |
| **Real carrier/logistics integration, real payouts, BNPL** | Shipping fee and status updates are mocked; payout is bank fields only; BNPL is a regulated credit product. |
| **Brand-verified Mall tier, authenticity compensation, delivery guarantees, new-seller fee incentives** | Lazada and Tiki show these are table stakes, not moats, and cost real effort. |
| **Full KYC queue, disputes admin, scheduled admin jobs** | Parked; build only if time allows. Seller type (individual vs business) is captured but not verified. |
| **SMS, push, in-app notification centre, marketing email, user notification preferences** | MVP notifications are transactional email only. Other channels add providers, cost and settings UI. |
| **Image search, recommendation engine beyond "similar products", Kubernetes/AKS** | Out of budget and time; revisit after Feb 2027. |
| **Matching Shopee's feature parity or regional variants** | Match the happy path only. Scope creep from "match Shopee" is the biggest threat to the project. |
| **Building around the FE's types or catalog-import design** | `hivespace.web` is a reference. HiveSpace owns its backend, product model and data pipeline. |

## 4. Personas

- **Buyer (Vietnamese-speaking shopper):** knows the need, not always the product name; expects variants, COD and clear tracking.
- **Seller (individual or small business):** wants a low-friction start and simple order handling.
- **Builder/reviewer (also a user):** a recruiter or interviewer who watches the 3-minute demo or reads the repo. Treated as a first-class consumer of the docs and demo.

## 5. User stories

**Buyer**
1. As a buyer, I want to search in natural Vietnamese ("áo khoác mỏng đi mưa dưới 300k") so that I find products without guessing keywords.
2. As a buyer, I want to browse, filter and sort products and pick a variant (size, colour) so that I can compare and choose.
3. As a buyer, I want a cart grouped by seller so that I understand I am buying from several shops.
4. As a buyer, I want one checkout that creates a separate order per seller so that each shop fulfils its own part.
5. As a buyer, I want to pay by COD or one online method so that I can use my usual way to pay.
6. As a buyer, I want an order status timeline so that I know where each order is.
7. As a buyer, I want to confirm receipt (or have it time out) so that payment is released only after delivery.
8. As a buyer, I want to cancel an unshipped order so that I can fix a mistake.
9. As a buyer, I want to leave a rating and text review, and read a summary of other reviews, so that I can decide quickly.
10. As a buyer, I want to see the seller's type and the return window on the product page so that I know who I am buying from.

**Seller**
11. As a buyer, I want to convert my account into a seller account (individual or business) with a phone/email-level start so that selling is low friction.
12. As a seller, I want to set up a shop profile, pickup address and payout bank details so that I can list products.
13. As a seller, I want to create and edit products with variants and stock, optionally with AI-drafted text, so that listing is fast.
14. As a seller, I want to confirm, ship or cancel orders so that I drive the order lifecycle.

**Edge and error states**
15. As a buyer, I want a clear message when an item sells out during checkout so that I am not charged for stock that does not exist.
16. As a buyer, I want a retried payment callback not to charge or create orders twice.
17. As a new user, I want a verification email after I register so that my account is confirmed and protected.
18. As a buyer, I want an email when my order is placed, shipped or cancelled so that I do not have to keep checking the app.
19. As a seller, I want an email when a new order arrives so that I can confirm it quickly.

## 6. Requirements

### P0: the MVP does not ship without these

| ID | Requirement | Acceptance criteria |
|---|---|---|
| P0-1 | **Identity:** register, login, role switch from buyer to seller (individual/business). Own Identity service, minimal scope. | Given a new user, when they register and log in, then they get a token accepted by the gateway. Given a buyer, when they register a shop, then they can access seller pages and keep buyer access. |
| P0-2 | **Seller setup:** shop profile, pickup address, payout bank fields. | Fields saved and shown on the shop page; no real payout occurs. |
| P0-3 | **Catalog:** product create/edit with category, attributes, variants/SKUs and stock, on HiveSpace's own product model. Seed data of at least 200 Vietnamese products. | A seller creates a product with 2+ variants; it appears in browse. Seed data loads from a script with one command. |
| P0-4 | **Basic search, filters, sort** (keyword baseline before AI). | Given seed data, when a buyer searches a keyword, then results are filterable by category and price and sortable. |
| P0-5 | **Cart and checkout saga:** multi-seller cart; checkout creates one order per seller with compensation on failure. | Given a cart with items from 3 sellers, when the buyer checks out, then 3 orders exist and stock is reserved. Given a step fails (payment, stock), then all prior steps are compensated and no orphan orders or held stock remain. |
| P0-6 | **Inventory (Go) with zero oversell:** atomic reservation, release on timeout/cancel. | Given 10 units and 200 concurrent buy attempts, then exactly 10 succeed and stock never goes below 0 (verified by test, see P0-10). |
| P0-7 | **Payment:** COD (an order state, no gateway) plus one online method, a mock provider unless VNPay sandbox fits the schedule. Idempotent webhook handler. | Given the same payment callback delivered 3 times, then the order is paid once and no duplicate side effects occur. |
| P0-8 | **Order lifecycle:** status timeline (placed, confirmed, shipped, delivered, completed, cancelled); seller confirm/ship/cancel; buyer confirm receipt or timeout releases payment; mocked shipping fee and status. | State transitions are validated; illegal transitions are rejected; the buyer sees the timeline. |
| P0-9 | **Ratings and text reviews** (verified-purchase only). | A buyer can review only a completed order's item, once. |
| P0-10 | **Flash-sale load proof:** k6 test run against the inventory path, results recorded in the repo. | Report shows zero oversells at the target concurrency and the p95 latency. |
| P0-11 | **Natural-language Vietnamese search (hero):** LLM parses query into filters; multilingual embeddings (pgvector) handle semantic match; eval set of ~50 queries with a recorded hit rate. | Eval script runs from the repo and prints hit rate against the keyword baseline. Result is documented whatever it is. |
| P0-12 | **Engineering trail:** CI on push, IaC for the deployed slice, OpenTelemetry trace across checkout, ADRs for key decisions, architecture diagram, README with run steps. | A fresh clone runs the stack with `docker compose up`; a checkout trace shows spans across services. |
| P0-14 | **Email notifications (event-driven):** a Notification capability, owned separately from Order, consumes events from Identity and Order and sends templated email through a provider interface. Emails: account verification on register, order placed (buyer and seller), order shipped, order cancelled. Mailpit locally; a free-tier email provider for the demo. Retries on failure and no duplicate sends for a repeated event. | Given a new registration, when the account is created, then a verification email is sent and the account cannot check out until verified. Given a checkout that creates 3 orders, then each buyer and each seller gets the right email. Given the same event delivered twice, then one email is sent. Given the provider is down, then the send is retried and checkout is not blocked. |
| P0-13 | **Demo deployment and video:** a small slice on Azure (scale-to-zero) and a recorded demo of the hero flow. | The deployed URL completes a checkout; the video is under 3 minutes. |

### P1: build if on schedule (cut in this order, last item first)

| ID | Requirement |
|---|---|
| P1-1 | Review summaries (batch job, shown on product page). |
| P1-2 | AI listing assist, text-only (title and description draft from seller input). |
| P1-3 | Simplified cancel/return request after delivery (one happy path). |
| P1-4 | Large-scale data generator and load test (target 1M orders, fall back to 100k). |
| P1-5 | VNPay sandbox instead of the mock provider. |
| P1-6 | Password-reset email (reuses the P0-14 pipeline). |

### P2: design for, do not build

- Vouchers (keep the order total calculation extensible, so adding discounts later does not rewrite checkout).
- Additional notification channels (SMS, push, in-app) and user preferences, behind the same provider interface as P0-14.
- Admin KYC and disputes.
- Real carrier integration behind a shipping interface.

**Cut order if behind (from the brainstorm):** shrink the generator, use mock payment, make listing assist text-only. **Never cut the saga or the oversell guarantee.**

## 7. Success metrics

This is a side project, so metrics measure whether it works, whether it can be shown, and whether it was learned from. There are no revenue or retention targets.

### Primary (the MVP is a success if all three pass by end of Feb 2027)

| Metric | Target | How measured |
|---|---|---|
| **Demo-ability** | Full hero flow (search, cart, split checkout, pay, track, review) demoed in **3 minutes or less**, with no manual database edits, on the deployed slice | Timed run, recorded video; someone else watches it cold |
| **Orders through the real system** | **10 completed orders from 3 seller accounts**, placed through the UI (not scripts), reaching "completed". Sellers and buyers can be friends or test users; the point is that the flow runs end to end | Order table query plus a short log |
| **Correctness under load** | **0 oversells** across a k6 run with a target of 200 concurrent buyers on 10 units, repeated 3 times | k6 report committed to the repo |

### Secondary

| Metric | Target (success / stretch) | How measured |
|---|---|---|
| NL search hit rate on the ~50-query eval set | Beats keyword baseline / at least 70% | Eval script in repo |
| Checkout saga failure injection | All injected failures (stock, payment, timeout) leave no orphan orders or held stock | Automated test |
| Idempotent webhook | 3 duplicate deliveries yield exactly 1 payment effect | Automated test |
| Cost | Under $5/month for Azure and AI combined, with a budget alert configured | Azure cost view |
| Build quality | CI green on main; `docker compose up` works from a fresh clone | CI badge, one fresh-clone test |

### Learning and portfolio indicators (lagging, informal)

- Each hard choice has an ADR (at least 5: microservices boundary, broker, search store, payment idempotency, hosting).
- One technical write-up or README walk-through of the saga and the load-test result.
- Used in at least one real job application or interview by end of Mar 2027.

## 8. Timeline and phasing

Hard date: MVP by Feb 2027 (~13 weeks from mid-Nov 2026; confirm the exact start).

| Weeks | Phase | Exit check |
|---|---|---|
| 1-2 | Walking skeleton: repo layout, Identity, gateway, CI, one service deployed | A request flows through the gateway to a deployed service |
| 3-4 | Seller onboarding, Catalog, seed data, basic search | Seller lists a product; buyer browses it |
| 5-7 | Cart, checkout saga, Inventory (Go), COD/mock payment, order lifecycle | Hero flow works end to end locally |
| 8-9 | Flash-sale reservation hardening and k6 proof | 0 oversells in recorded runs |
| 10-12 | NL search with eval, then listing assist and review summaries if on time | Eval hit rate recorded |
| 13 | Data generator (if on time), docs, deploy, demo video | Success metrics checked |

**Checkpoints:** at the end of week 7, if the hero flow is not working locally, drop P1 items now and start AI search with only the keyword baseline in place. At week 9, if the k6 proof is not done, freeze all other work until it is.

**Dependencies:** Azure free-tier terms (verify before designing infrastructure); LLM and embedding model choice (affects the search design and the budget).

## 9. Open questions

| # | Question | Owner | Blocking? |
|---|---|---|---|
| 1 | **Data source:** LLM-generated Vietnamese catalog, public dataset, or private crawl (legal and terms risk)? | Builder | Yes, before week 3 |
| 2 | **Product model and ID strategy:** Shopee-style category/attributes/variants/SKUs, or simpler? UUID vs int. | Builder | Yes, before week 3 |
| 3 | **Azure free offers:** which database hours, Container Apps grant and credits apply today? | Builder (check Azure terms) | Yes, before infra design |
| 4 | **LLM and embedding models:** which free-tier or local LLM for query parsing; which multilingual embedding model runs on CPU? | Builder | Before week 10; a spike in week 8 is sensible |
| 5 | **Messaging:** Service Bus Basic vs RabbitMQ locally and something lighter in the cloud, within $5/month. | Builder | Before week 5 |
| 6 | **Payment:** is the mock enough, or does VNPay sandbox ship? | Builder | No, default is mock |
| 7 | **FE reuse:** which `hivespace.web` pages (buyer storefront, seller listing, checkout) are reused, adapted or ignored? Where do specs live (FE expects `../hivespace.spec`)? | Builder | Before week 3 |
| 8 | **Search gap validation:** do 10-20 Vietnamese natural-language queries on Shopee and TikTok Shop confirm weak search? | Builder | No, but it takes an hour and could change the hero |
| 10 | **Email provider:** which free tier covers the demo (sending limits, need for a verified domain)? Is Notification its own deployable or a module, given it is no longer inside Order? | Builder | Before week 3; decide with question 9 |
| 9 | **Modular monolith vs six services:** is the six-service split justified by the learning goal, or should some start as modules? (One ADR.) | Builder | Before week 1 scaffolding |

## 10. Key risks

| Risk | Mitigation |
|---|---|
| ~390 hours cannot hold 7 features plus microservices plus AI plus data (**riskiest assumption**) | P1 cut order; riskiest backend work (saga, inventory) first, AI last; week 7 checkpoint |
| Budget overrun on Azure or LLM calls | Heavy work runs locally; scale-to-zero; pgvector; budget alert; ADR for each choice |
| Scope creep from "match Shopee" | Section 3 is binding; additions require a removal |
| Product model rewritten mid-build | Decide Q1/Q2 before week 3; do not shape it around the FE |
| "Real orders from 3 sellers" may not involve real strangers | Metric defined as orders through the real UI and system with real people operating it, not scripts; do not claim real-market traction |

## 11. Next steps

1. Answer open questions 1, 2 and 9 (data source, product model, service-boundary ADR).
2. Confirm Azure free offers and set the budget alert.
3. Write acceptance criteria for the hero flow as the first test script (P0-5, P0-8).
4. List FE pages as reuse / adapt / ignore.
5. Start the walking skeleton (weeks 1-2).
