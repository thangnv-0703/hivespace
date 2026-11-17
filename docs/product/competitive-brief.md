# Competitive Brief: Shopee, TikTok Shop, Lazada and Tiki (Vietnam)

Date: 2026-11-14 · Lazada and Tiki are covered in section 4b; the feature split in section 5 already accounts for them. Scope: buyer purchase flow and seller onboarding · Decision it informs: which features HiveSpace must match, differentiate on, or skip for the Feb 2027 MVP (solo, ~390 h, see [brainstorm-notes.md](brainstorm-notes.md)).

**Evidence labels.** [W] = from web search this session (third-party sources, not official docs). [A] = approximate, from training knowledge; verify before quoting. Shopee help-centre rules cited here come from the Singapore and Malaysia sites, not Vietnam, so treat them as indicative.

## 1. Market context

| | Shopee | TikTok Shop |
|---|---|---|
| Vietnam share of major-platform GMV, 2025 | ~56-57.5% (down from ~65% in 2024) [W] | ~40-41% (up from ~28-29% in 2024) [W] |
| Vietnam GMV, 2025 | >US$11.8B (Momentum Works) [W] | not isolated in sources found |
| Positioning | Search-and-shop marketplace; the default place to compare and buy | Discovery commerce: video and live content create demand; buy in-feed |
| Momentum | Leader, growth slowing | Fastest gainer, +~12 pts in a year |

- The four big platforms did ~VND 429.7T (~US$16.4B) in 2025, +~35% YoY. Shopee and TikTok Shop together hold ~97% [W].
- Over 80,000 online shops recorded no orders in H1 2025 [W]. Marketplace onboarding is easy; getting orders is the hard part.

## 2. Buyer purchase flow

Ratings: Strong / Adequate / Weak / Absent, relative to what a Vietnamese buyer expects. Ratings are judgement calls from reviews and training knowledge, not tested hands-on.

| Step | Shopee | TikTok Shop | Why it matters |
|---|---|---|---|
| Discovery | Strong: search, categories, flash sale, daily deals [A] | Strong: video/live feed is the main funnel [A] | Shopee = intent; TikTok = impulse |
| Search | Adequate: keyword, filters, sort; weak on natural-language Vietnamese [A] | Adequate: search is secondary to feed [A] | Open gap for HiveSpace |
| Product page | Strong: variants/SKUs, ratings with photos, shop info, Q&A [A] | Adequate: video-led, reviews present [A] | Variants and reviews are expected |
| Cart | Strong: grouped by shop, per-shop vouchers [A] | Adequate [A] | Multi-seller cart is the core structure |
| Vouchers at checkout | Strong: seller, platform and free-shipping vouchers, max 1 of each per checkout (SG rules; MY raised limits in Feb 2025) [W] | Strong: platform and seller vouchers, frequent campaigns [A] | Heavy price-sensitivity; but see skip list |
| Shipping | Strong: free-shipping vouchers (SG cap ~$1.99/shop) [W], integrated carriers | Strong: subsidised shipping, own logistics partners [A] | Per-shop shipping fee on a split order |
| Payment | Strong: COD, cards, bank/e-wallet, ShopeePay, SPayLater (BNPL) [W/A] | Strong: COD, e-wallet, cards [A] | COD is non-negotiable in Vietnam [A] |
| Buyer protection | Strong: Shopee Guarantee holds payment until receipt confirmed; refunds back to wallet [W] | Adequate-Strong: platform-held payment, return/refund policy [A] | Trust is the reason buyers use marketplaces |
| Order tracking | Strong [A] | Adequate [A] | Expected |
| Returns/refunds | Strong: in-app return request, voucher reinstatement rules vary by country [W] | Adequate [A] | Needs a state machine; costs real effort |
| Chat with seller | Strong: in-app chat [A] | Adequate: via content/inbox [A] | Parked in HiveSpace notes |
| Reviews | Strong: photo/video reviews, coin rewards [A] | Strong: video reviews tied to content [A] | Feeds the review-summary AI feature |

**Takeaway.** Shopee wins on a complete, trusted, comparison-friendly checkout. TikTok Shop wins on getting the buyer to the product page. Neither is strong at conversational/semantic Vietnamese search.

## 3. Seller onboarding

| Step | Shopee | TikTok Shop | Why it matters |
|---|---|---|---|
| Entry | Seller Centre ("Kênh Người Bán") from shopee.vn or app; a buyer account converts into a seller account [W] | Seller Center, linked to a TikTok account [A] | Low friction is the baseline |
| Account types | Individual or Business [W] | Individual (Vietnamese citizen, 18+) or registered Vietnamese entity [W] | Two tiers: simple start, verified business |
| Documents | Phone + email to start; ID/business docs for verification (Vietnam list unconfirmed) [W] | ID or business licence, tax registration, bank details [W, partly unconfirmed] | KYC is the admin-side workload |
| Shop profile | Name, logo, pickup address, shipping channels [W] | Similar [A] | Minimal set needed to list |
| Payout | Bank account linking [W] | Bank account linking [W] | Needed before first payout |
| Product listing | Strong: category-driven attributes, variants, bulk upload, mobile app [A] | Strong: mobile/video-first listing, bulk tools [A] | Listing quality drives search and conversion |
| Fees | Commission + transaction/service fees, varies by category and seller type (Vietnam figures not confirmed) [A] | Sources conflict: ~1% platform fee [W] vs higher commissions elsewhere [W]; new sellers can get up to ~90 days commission-free [W] | Verify in each Seller Centre before quoting |
| New-seller support | Seller education hub, onboarding tasks [W/A] | Onboarding tasks with fee holiday as incentive [W] | Incentives pull sellers; HiveSpace has no budget for this |

**Takeaway.** Both make "start selling" a phone-number-level action and defer heavy verification. The real differentiator is what happens next: listing tools, fee incentives, and getting traffic.

## 4. Strengths and weaknesses

**Shopee**
- Strengths: complete, trusted checkout; deep logistics and payments; the habitual place to compare prices.
- Weaknesses: losing share; voucher and rule complexity (rules differ by country and change often) [W]; search relies on keywords [A].

**TikTok Shop**
- Strengths: content-driven demand, fast share gains, strong sell-through for impulse and trend products.
- Weaknesses: checkout and post-purchase tooling less mature than Shopee [A]; discovery is feed-driven, weak for "I know what I want" shoppers [A]; fee structure unclear across sources [W].

## 4b. Lazada and Tiki (added 2026-11-14)

Research depth is lower than for Shopee and TikTok Shop: search found little Vietnam-specific checkout or onboarding documentation for either. Ratings and details marked [A] are from training knowledge; verify before quoting.

### Market position

| | Lazada | Tiki |
|---|---|---|
| Vietnam share, 2025 | ~3% (Q3 2025); combined with Tiki fell from ~6% to ~3% [W] | ~1% or less; too small to appear in some quarterly reports [W] |
| Sales trend | H1 2025 sales down ~48% YoY [W] | H1 2025 sales down ~63% YoY [W] |
| Position | Alibaba-backed; LazMall (brand stores) is ~60% of its revenue [W] | Vietnamese homegrown; historically a books/quality-first, own-fulfilment retailer [A] |
| Status | Shrinking, still operating | Effectively squeezed out of the mainstream [W] |

Context: Shopee and TikTok Shop hold ~97% of GMV. Big platforms are pulling back from cash-burn growth, and the Law on E-Commerce takes effect on 1 July 2026, which should push the market toward more transparency [W].

### Buyer purchase flow

| Step | Lazada | Tiki |
|---|---|---|
| Payment | COD, Visa/Mastercard/JCB, Napas, instalments, ZaloPay, MoMo [W] | COD, cards, e-wallets [A] |
| Returns | LazMall: 15-day return including change of mind (Vietnam seller doc); other markets moved to 30 days, Vietnam not confirmed [W] | Returns policy exists; details unconfirmed [A] |
| Authenticity | LazMall "100% Authentic" with 2x refund on counterfeits (regional) [W] | Historically a trust and genuine-goods positioning [A] |
| Delivery promise | Guaranteed Delivery with late compensation (rolled out regionally, incl. Vietnam) [W] | Fast delivery via own logistics (TikiNOW) [A] |
| Cart / checkout | Same marketplace pattern as Shopee: per-shop cart, vouchers [A] | Same pattern, smaller seller base [A] |

### Seller onboarding

| Step | Lazada | Tiki |
|---|---|---|
| Entry | Seller Center; Marketplace (open) and LazMall (invite/criteria) tiers [W/A] | Seller portal; historically more curated than open [A] |
| LazMall criteria | Brand ownership or authorised distribution, ~6 months selling, ~30 orders/month, seller rating >70% (single vendor blog, unverified) [W] | n/a |
| Fees | Cube.asia estimate (Dec 2024): ~4.6-5.5% commission plus ~1% transaction fee, varies by category. Predates 2025; verify [W] | Not found [A] |

### What they teach HiveSpace

- **Authenticity-first tiering works but is not enough.** LazMall earns most of Lazada's revenue, yet Lazada still lost share. Trust badges and a curated "official store" tier do not substitute for traffic or price.
- **Strong guarantees did not save Tiki or Lazada.** Delivery guarantees and authenticity promises are table stakes, not moats.
- **Scale and traffic decide the market.** This reinforces the earlier takeaway: do not compete on breadth; compete on one hard, visible thing (search, checkout correctness).
- **Regulation is a design input.** The 1 July 2026 E-Commerce Law raises expectations on seller identification and transparency, so a seller verification step is worth including even in a demo.

### Feature split changes from adding these two

- **Table stakes (added):** show seller identity/type (individual vs business) on shop and product pages; a clear return window stated on the product page.
- **Differentiators:** unchanged. Neither Lazada nor Tiki is strong on natural-language Vietnamese search as far as I can tell [A].
- **Skip (added):** a brand-verified Mall tier (LazMall/Shopee Mall style), authenticity compensation schemes, delivery-guarantee compensation, own-fulfilment/logistics (TikiNOW-style).

## 5. Feature split for HiveSpace

### Table stakes (must match)
| Feature | Slice | Notes |
|---|---|---|
| Multi-seller cart and checkout that splits into one order per seller | Buyer | The saga in the brainstorm notes; core engineering value |
| Variants/SKUs on product page | Buyer | Decide product model early (open question 2) |
| COD plus one online method (VNPay sandbox or mock) | Buyer | COD can be a simple order state, no gateway |
| Escrow-style buyer protection: payment released on receipt or timeout | Buyer | Simplified: order status plus payment hold in the saga |
| Order status tracking | Buyer | Status timeline; no live carrier integration |
| Basic search, filters, sort | Buyer | Baseline before AI layer |
| Ratings and text reviews | Buyer | Needed as input for review summaries |
| Seller registration: buyer converts to seller, individual vs business | Seller | Mirror Shopee's low-friction start |
| Shop profile, pickup address, payout bank details | Seller | Fields only; no real payouts |
| Product create/edit with variants and stock | Seller | Listing without AI first (weeks 3-4) |
| Seller order management: confirm, ship, cancel | Seller | Drives the order state machine |
| Cancel/return request, simplified | Buyer | One happy-path return flow; cut if behind |

### Differentiators (your angle)
| Feature | Why it differentiates | Evidence |
|---|---|---|
| Natural-language Vietnamese search (LLM filter parsing + embeddings), with a measured eval hit rate | Both platforms are keyword/feed-led; this is the hero feature and the CV story | Gap observed, not proven demand; measure it |
| Zero-oversell stock reservation under flash-sale load, proven with k6 | Shopee-style flash sales are a known hard problem; proof beats claims | Engineering differentiator, not buyer-visible |
| AI listing assist (text-only) | Lowers the listing effort that both platforms leave to the seller | Secondary |
| Review summaries (batch) | Condenses many reviews into a buyer-readable summary | Secondary |
| Idempotent payment webhooks and tracing across services | Reads as senior; invisible to buyers | Engineering signal only |

### Skip (not in MVP)
| Feature | Reason |
|---|---|
| Ads / promoted listings | Needs traffic and budget; no marketplace to sell ads into |
| Livestream and short video commerce | TikTok's core, far outside 390 h and a $5/month budget |
| Coins / loyalty / gamification | Large rules surface, little engineering learning |
| Voucher engine (stackable platform/seller/shipping vouchers) | Shopee's rules vary by country and change often [W]; start with none, or one seller-level code if time allows |
| BNPL (SPayLater-style) | Regulated credit product; out of scope |
| Real carrier/logistics integration | Mock shipping fee and status updates |
| Live chat | Already parked in the brainstorm notes |
| Fee incentives for new sellers | Marketplace growth lever; not applicable |
| Full KYC queue and disputes admin | Build only if time allows (already parked) |

## 6. Opportunities and threats

- **Opportunity:** semantic Vietnamese search for shoppers who know the need but not the product name. Neither giant leads here as far as I can tell [A].
- **Opportunity:** a small, fast, correct checkout as a demonstrable engineering artefact.
- **Threat to the project, not the market:** scope creep from "match Shopee". Both competitors have years of accumulated rules (vouchers, returns, regional variants). Matching surface area is not achievable; match the happy path only.

## 7. Strategic implications

1. Build the table-stakes list as a thin vertical slice: browse, search, cart, split checkout, pay (COD/mock), track, review.
2. Put differentiation budget into search and flash-sale correctness; put none into engagement features.
3. Treat the skip list as final until the MVP ships; revisit vouchers first.
4. Do not copy Shopee's or TikTok's fee schedule; set a placeholder commission in config.

## 8. Gaps and what to verify

- Shopee and TikTok Shop Vietnam commission and fee tables, and exact seller document lists: not confirmed; check each Seller Centre.
- Search quality claims (point 6) are unmeasured; a quick manual test of 10-20 Vietnamese natural-language queries on Shopee and TikTok Shop would confirm or kill the gap.
- Buyer-flow ratings are not from hands-on testing. Walk through one real checkout on each app before locking priorities.
- Market share figures differ by source by a few points; direction is consistent.

## Sources

- [Vietnam e-commerce shake-out, VnEconomy](https://en.vneconomy.vn/vietnams-e-commerce-market-enters-a-major-shake-out-phase.htm)
- [Online retail sales in Vietnam jump 35%, The Investor](https://theinvestor.vn/online-retail-sales-in-vietnam-jump-35-to-over-16-bln-in-2025-report-d18140.html)
- [Over 80,000 online shops record no orders in H1, The Investor](https://theinvestor.vn/over-80000-online-shops-in-vietnam-record-no-orders-in-h1-d16481.html)
- [TikTok Shop narrows gap with Shopee, KrASIA](https://www.kr-asia.com/pulses/156101)
- [After the boom, Vietnam's e-commerce becomes a tougher game, Vietnam News](https://vietnamnews.vn/economy/1766383/after-the-boom-viet-nam-s-e-commerce-market-becomes-a-tougher-game.html)
- [Shopee, TikTok Shop squeeze Tiki almost entirely out, VnExpress](https://e.vnexpress.net/news/business/data-speaks/shopee-tiktok-shop-squeeze-tiki-almost-entirely-out-of-e-commerce-market-4877173.html)
- [LazMall revenue share, Báo Pháp luật](https://doanhnhan.baophapluat.vn/doanh-thu-lazmall-chiem-60-lazada-van-truot-dai-xuong-3-thi-phan-sau-shopee-va-tiktok-shop-88817.html)
- [Lazada take rate tracker, Cube.asia](https://cube.asia/lazada-take-rate-tracker)
- [Operating Lazada Vietnam, eFex](https://efex.vn/en/blog/operating-lazada-vietnam-marketplace)
- [Lazada Vietnam homepage (payment methods)](https://www.lazada.vn/)
- [Lazada LazMall return period, community article](https://community.lazada.com.ph/article/what-is-the-lazmall-30-day-return-period-1309500-hc)
- [Operating on Shopee Vietnam, eFex](https://efex.vn/en/blog/operating-on-shopee-vietnam)
- [Register on Shopee, Fynd docs](https://documentation.fynd.com/konnect/channels/marketplaces-webstores/shopee/register-on-shopee)
- [TikTok Shop Vietnam seller registration guide](https://kakaflow-kb.pages.dev/entries/huong-dan-dang-ky-nguoi-ban-tiktok-shop-viet-nam)
- [TikTok local store opening guide for SEA, BigSeller](https://www.bigseller.pro/blog/articleDetails/1636/TikTok-Local-Store-Opening-Guide-for-Southeast-Asia.htm)
- [Shopee Free Shipping Vouchers, Shopee SG help](https://help.shopee.sg/portal/4/article/76861-[Vouchers]-What-are-Free-Shipping-Vouchers%3F)
- [Applying vouchers at checkout, Shopee SG help](https://help.shopee.sg/portal/4/article/76829-[Vouchers]-How-do-I-apply-vouchers-at-checkout)
