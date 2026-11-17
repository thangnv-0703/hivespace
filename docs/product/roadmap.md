# HiveSpace Roadmap (Now / Next / Later)

Date: 2026-11-14 · Owner: solo builder · Source: [prd.md](prd.md), [competitive-brief.md](competitive-brief.md)

**How to use this document.** *Now* is committed and detailed in the PRD. For the MVP, the kit's implementation plan will replace this roadmap as the build order. *Next* and *Later* stay here as the view of phases beyond the MVP. They are directional: no dates, only an order and the reason for it.

**Prioritization: MoSCoW.** It is applied to every item and is relative to the horizon it sits in. A "Must" in Next means the Next phase fails without it, not that the MVP does.

**Capacity assumption.** About 30 h/week, solo. Post-MVP capacity is unknown (job search, other commitments). Nothing in Next or Later is a commitment.

## Status overview

| Horizon | Window | Items | Status |
|---|---|---|---|
| Now | Nov 2026 to Feb 2027 (~13 weeks) | 14 Must, 6 Should | Not Started |
| Next | After MVP ships, roughly Mar to mid 2027 | 3 Must, 4 Should, 2 Could | Not Started |
| Later | H2 2027 and beyond | 2 Should, 4 Could | Not Started |

## Now: MVP (committed)

Goal: one working hero flow, proof of the hard backend problems, one measured AI feature. Full acceptance criteria are in the PRD (section 6). Order of build is in the PRD timeline (section 8).

| MoSCoW | Capability | PRD ref | Weeks | Notes |
|---|---|---|---|---|
| **Must** | Walking skeleton: repo, gateway, CI, one service deployed | P0-12, P0-13 | 1-2 | Data tool and product model decided in this window |
| **Must** | Identity: register, login, buyer to seller role switch | P0-1 | 1-3 | Own service, minimal scope |
| **Must** | Seller setup: shop profile, pickup address, payout fields | P0-2 | 3-4 | Fields only, no real payouts |
| **Must** | Catalog: products, variants/SKUs, stock, 200+ seed products | P0-3 | 3-4 | Depends on the product model |
| **Must** | Basic search, filters, sort (keyword baseline) | P0-4 | 4 | Baseline for the AI comparison |
| **Must** | Cart and checkout saga, one order per seller | P0-5 | 5-7 | Never cut |
| **Must** | Inventory (Go) with zero oversell | P0-6 | 5-7 | Never cut |
| **Must** | Payment: COD plus mock online, idempotent webhooks | P0-7 | 6-7 | Mock unless VNPay fits |
| **Must** | Order lifecycle, seller fulfilment, mocked shipping | P0-8 | 6-7 | |
| **Must** | Ratings and text reviews (verified purchase) | P0-9 | 7 | Input for review summaries |
| **Must** | Email notifications: verify on register, order placed/shipped/cancelled, seller new order | P0-14 | 3-7 | Starts with Identity (verification email), extended as order events appear |
| **Must** | Flash-sale k6 proof | P0-10 | 8-9 | Report committed to the repo |
| **Must** | Natural-language Vietnamese search with eval hit rate | P0-11 | 10-12 | The hero feature |
| **Must** | Demo deployment, video, docs, ADRs, IaC | P0-12, P0-13 | 13 | |
| **Should** | Review summaries (batch) | P1-1 | 11-12 | Cut first if behind (see order below) |
| **Should** | AI listing assist, text-only | P1-2 | 11-12 | |
| **Should** | Simplified cancel/return request after delivery | P1-3 | 8 | One happy path only |
| **Should** | Large-scale data generator and load test | P1-4 | 13 | Fall back from 1M to 100k orders |
| **Should** | VNPay sandbox | P1-5 | 7 | Mock is the default |
| **Should** | Password-reset email | P1-6 | 4 | Reuses the notification pipeline |

**Cut order if behind (from the PRD):** VNPay sandbox, then data generator size, then listing assist, then review summaries, then simplified returns. The saga and the oversell guarantee are never cut.

**Won't (this horizon):** vouchers, loyalty, chat, livestream, ads, real logistics, real payouts, BNPL, Mall tier, KYC and disputes admin. These are the PRD non-goals and stay binding until the MVP ships.

## Next: after MVP ships (planned, not scheduled)

Entry condition: the MVP success metrics pass (3-minute demo, 10 completed orders from 3 sellers, 0 oversells). If they do not, Next waits and the MVP gets fixed first.

| MoSCoW | Item | Why this horizon | Depends on |
|---|---|---|---|
| **Must** | **Returns and refunds** (full flow): request, seller response, refund state machine, stock restock | Trust is the reason buyers use marketplaces, and the competitive brief rates this Strong on Shopee. Builds on the MVP's simplified return (P1-3) and order state machine. | Order lifecycle (P0-8); payment hold (P0-7) |
| **Must** | **Reviews, phase 2:** photo reviews, seller replies, review summaries if not shipped in Now | Ratings and text are in the MVP; this deepens them. Photo and seller replies need file storage, which the $5 budget makes a real decision. | P0-9; storage ADR |
| **Must** | **Hardening from MVP findings:** fixes from the load test, tracing gaps, anything the demo exposed | Prevents the MVP from becoming a pile of known issues. | MVP complete |
| **Should** | **Vouchers, stage 1:** one seller-level code and one platform code, applied in checkout | First non-goal to revisit (brief section 7). The MVP keeps the order total calculation extensible for this. | Checkout saga (P0-5) |
| **Should** | **Seller chat (async messages, not real-time presence)** | Chat is Strong on Shopee but parked. Start with async messaging; this avoids a connection-heavy real-time service on a tiny budget. | Identity; messaging ADR |
| **Should** | **Notification channels and preferences:** in-app centre, push or SMS, per-user settings | The MVP sends transactional email only. Returns, chat and vouchers will all need to notify, so extend the same provider interface. | P0-14 |
| **Should** | **Vietnam e-commerce law alignment check:** seller identity display, transparency items | The brief flags the 1 July 2026 law as a design input. A check, not a compliance programme. | Seller type captured in MVP |
| **Could** | **Admin lite:** seller approval queue, order search | Parked in the brainstorm. Only if there is a real need to operate the demo. | Identity roles |
| **Could** | **Real VNPay sandbox integration**, if not done in Now | Useful signal for payment engineering, not needed for the demo. | Payment interface (P0-7) |

**Won't (this horizon):** livestream, ads, BNPL, Mall tier, real-time chat presence, AKS.

## Later: directional bets (H2 2027 and beyond)

Nothing here is scoped. Each item needs its own spec and a fresh look at capacity and whether it still matters.

| MoSCoW | Item | Why later | Revisit when |
|---|---|---|---|
| **Should** | **Recommendations:** "similar products", then personalized | Needs real behavior data; pgvector already supports the "similar" case cheaply. | After Next, and once there is enough order and view data (or a synthetic generator) |
| **Should** | **Vouchers, stage 2:** stackable platform, seller and free-shipping rules | Large rules surface that changes by country. | Only after stage 1 proves the extension point |
| **Could** | **Image-based search** | Out of budget and time; depends on an embedding model that fits CPU. | After NL search is measured and stable |
| **Could** | **Kubernetes (AKS) deployment** | Costly versus the $5/month budget; mainly a CV signal. | When there is a budget, or a time-boxed local cluster exercise |
| **Could** | **Real carrier/logistics integration** behind the shipping interface | Needs carrier APIs and a business relationship. | If the project ever has real merchants |
| **Could** | **Livestream and short-video commerce, ads/promoted listings** | TikTok's core and a traffic business; far outside a solo budget. | Likely never; keep as the explicit "no" to stay honest |

## Dependencies and risks

| Risk or dependency | Affects | Mitigation |
|---|---|---|
| Product model and data source undecided | Now (catalog, seed data, search) | Decide by end of week 2, as planned; data tool is time-boxed |
| ~390 h may not hold all of Now | Now | PRD cut order; week-7 and week-9 checkpoints |
| Post-MVP capacity unknown | Next, Later | Treat Next as a priority order, not a plan; re-plan at MVP end |
| Storage and messaging costs on a $5/month budget | Next (photo reviews, chat) | Decide in ADRs before starting those items |
| Scope creep from "match Shopee" | Everything | Any addition to a horizon must name what leaves it |

## Changes this update

This is the first version of the roadmap, so nothing has changed yet. Two adjustments were made to your expected shape. See the notes below.

## Notes on how the requested shape was adapted

- **Reviews are in both Now and Next.** The PRD already makes ratings and text reviews a P0 (reviews feed the review-summary AI feature), so they cannot be wholly "Next". Next holds the deeper review work: photos, seller replies, and review summaries if they slip.
- **Returns are in both too.** A simplified return flow is a PRD P1 in Now; the full flow is the Next item.
