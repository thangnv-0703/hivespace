# Software Requirements Specification: HiveSpace (MVP)

> Version: 0.20 · Status: Draft (pending user confirmation of "Inferred" items) · Last updated: 2026-11-26
> Requirement syntax: EARS   *(governs §3.1 statements and all amendments)*

## Revision History

| Version | Date | Author | Changes |
| :------ | :--- | :----- | :------ |
| 0.9 | 2026-11-14 | Requirements engineering (Claude) | Initial draft derived from `docs/product/prd.md` v1 and `docs/product/brainstorm-notes.md`. Not yet finalized. |
| 0.10 | 2026-11-15 | Requirements engineering (Claude) | Change request CR-1 (all Must, user-requested, beyond the PRD): Google sign-in, change password, user profile, image upload (avatar, shop, product), email through Resend, extensible money model (VND only), vi/en localization. New areas FR-PROF, FR-MEDIA, FR-LOC, FR-CUR; new FR-AUTH-020..027, FR-NOTIF-014..015; new NFR-PERF-007, NFR-SEC-013..015, NFR-MNT-008..010, NFR-L10N-004, NFR-COST-004; EIR-SW-006..007, EIR-COM-005; use cases UC-025..028. FR-CAT-011 promoted to Must and reworded; OQ-10, OQ-11, OQ-25 resolved. |
| 0.17 | 2026-11-25 | Clarify-feature (Claude), FEAT-012 | Modified FR-CAT-005: a value outside the product limits is also invalid; the limits are listed in its notes (user decision in the FEAT-012 feature spec). UC-005 4a updated. No requirement added or removed. |
| 0.20 | 2026-11-26 | Clarify-feature (Claude), FEAT-014 amendment | Added FR-CAT-022 (a list attribute may accept seller-entered text besides its list values) and FR-CAT-023 (a leaf with no attributes of its own uses its nearest ancestor's). Modified FR-CAT-019: closing a list that accepts other values is a tightening; giving a leaf with products its own attributes that drop inherited ones is refused. UC-005 and UC-020 traces updated. |
| 0.19 | 2026-11-26 | Clarify-feature (Claude), FEAT-014 | Added FR-CAT-021 (a top-level category shows its image, or a placeholder; images come from the category file). UC-007 traces updated. |
| 0.18 | 2026-11-25 | Clarify-feature (Claude), FEAT-014 | Added FR-CAT-019 (loading the category tree: add, rename and loosen only; both languages required; whole load rejected on a breaking change) and FR-CAT-020 (multi-value list attributes up to a maximum). UC-005 and UC-020 traces updated. |
| 0.16 | 2026-11-16 | Requirements engineering (Claude) | Change request CR-7 (user-requested, from the `hivespace.web` reference): sign in with an email one-time code. New FR-AUTH-028..036, FR-NOTIF-016, NFR-SEC-016; modified NFR-SEC-005 (code request and code submit rate limits) and NFR-SEC-009 (code sign-in audited); new use case UC-029; new OQ-35. §1.2, §2.2 and the glossary updated. |
| 0.15 | 2026-11-16 | Requirements engineering (Claude) | Change request CR-6 (learning-first principle, gRPC): CON-3 reworded from a fixed language list to primary languages with other technologies allowed per service when recorded in an ADR. No requirement added, modified or removed. |
| 0.14 | 2026-11-16 | Requirements engineering (Claude) | Change request CR-5: Cart becomes its own service in Go; CON-3 and CON-4 updated. No requirement added, modified or removed. |
| 0.13 | 2026-11-15 | Requirements engineering (Claude) | Change request CR-4 (from the language review of `docs/architecture.md`): CON-3 now assigns Notification to Python. No requirement added, modified or removed. |
| 0.12 | 2026-11-15 | Requirements engineering (Claude) | Change request CR-3 (from `docs/architecture.md`, `docs/adr/`): modified FR-AUTH-015 (revocation applies to refresh tokens; access tokens are not individually revoked) and NFR-SEC-004 (access token lifetime tightened from 60 to 15 minutes); CON-4 updated (gateway/BFF, 8 services, 1 Azure Function); EIR-COM-003 names the broker; OQ-5 and OQ-10 resolved. No requirement added or removed. |
| 0.11 | 2026-11-15 | Requirements engineering (Claude) | Change request CR-2 (from `docs/dev-standards.md`): local orchestration moves from docker compose to .NET Aspire (NFR-PORT-001 reworded; §2.4 and NFR-PERF-001 updated); OQ-9 resolved as separate services (CON-4 updated). No requirement added or removed. |

## 1. Introduction

### 1.1 Purpose
This SRS specifies the requirements for the HiveSpace MVP: a Shopee-inspired, multi-seller marketplace built by a solo builder as a learning and portfolio project. It is the source of truth for architecture, UX, planning, and test work. Scope is limited to the PRD's MVP.

### 1.2 Product vision & scope
**Vision.** One complete, working hero flow (register → search → cart → split checkout → pay → track → review) with a seller side (list, fulfil, see orders), used to demonstrate three hard backend problems (multi-seller checkout saga, zero overselling under concurrency, idempotent payment webhooks) and one measured AI feature (natural-language Vietnamese search).

**Priority mapping from the PRD** (user decision, 2026-11-14):

| PRD tier | SRS priority | Meaning |
| :-- | :-- | :-- |
| P0 | Must (explicit in PRD) or Should (supporting behaviour the PRD implies but does not state; marked `Inferred` in the Source column) | MVP does not ship without Must |
| P1 | Could | Built only if on schedule; cut in the PRD's order (last item first) |
| P2 | not a requirement | Appears only as a design constraint (NFR-MNT-007, NFR-MNT-006) |

**In scope (MVP):** identity and roles; shop setup; catalog with variants and stock; keyword search with filters and sort; natural-language Vietnamese search with eval; multi-seller cart; checkout saga; inventory reservation; COD and mock online payment with idempotent callbacks; order lifecycle with mocked shipping; verified-purchase reviews; transactional email; engineering trail; demo deployment and video. Could: review summaries, AI listing assist, return request, large-scale data generator, VNPay sandbox, password reset.

**Out of scope (binding, from PRD §3):** vouchers/coupons; coins, loyalty, gamification, chat, livestream, ads; real carrier integration, real payouts, BNPL; Mall tier and guarantees; KYC queue, disputes admin, scheduled admin jobs; SMS, push, in-app notification centre, marketing email, notification preferences; image search, recommendations beyond "similar products", Kubernetes; Shopee feature parity; shaping the backend around `hivespace.web`.

**Also excluded, because the PRD does not require them (flagged for confirmation, Appendix B):** MFA, email change, account deletion and personal-data export, admin console, guest checkout, flash-sale pricing or campaigns (the "flash sale" is a concurrency load scenario on stock only), review editing/deletion, review moderation.

**Added by change request CR-1 (2026-11-15), all Must, beyond the PRD:** Google sign-in (auto-link by verified email); change password; user profile (display name, phone, gender, date of birth, language); image upload for avatar, shop logo and cover, and product images; email through Resend (Mailpit locally); money modelled as amount plus currency with VND the only enabled currency; Vietnamese and English for UI, system messages and emails. Seller-entered content is not translated and natural-language search stays Vietnamese-first (OQ-28). These additions raise MVP scope against the PRD's 390-hour budget; the PRD has not been edited.

### 1.3 Goals & success metrics

| Goal | Measure (PRD §7) | SRS trace |
| :-- | :-- | :-- |
| G1 Hero flow works end to end | Demoed in ≤3 min on the deployed slice, no manual DB edits; 10 completed orders from 3 seller accounts via the UI | NFR-DEM-001..003 |
| G2 Hard backend problems proven | 0 oversells at 200 concurrent buyers on 10 units, 3 runs; saga failure injection leaves no orphans; 3 duplicate webhooks give 1 effect | NFR-SCAL-001, NFR-REL-001..003 |
| G3 Measured AI feature | NL search hit rate recorded; beats keyword baseline; stretch ≥70% | FR-NLS-010..012 |
| G4 Budget and complexity cap | ≤$5/month Azure plus AI, budget alert configured | NFR-COST-001..003 |
| G5 Reviewable engineering trail | ADRs (≥5), IaC, CI, OpenTelemetry, architecture diagram, demo video | NFR-MNT, NFR-OBS, NFR-DEM |

### 1.4 Definitions, acronyms & abbreviations
See Appendix A.

### 1.5 References
- `docs/product/prd.md` (v1, 2026-11-14), primary input
- `docs/product/brainstorm-notes.md`, raw input. Where it conflicts with the PRD, the PRD wins (see Appendix B, conflict C-1).
- `docs/product/competitive-brief.md`, `docs/product/roadmap.md` (background only)
- ISO/IEC/IEEE 29148; ISO/IEC 25010; EARS (Mavin et al.)

## 2. Overall Description

### 2.1 Product perspective
HiveSpace is a new, self-contained web marketplace. Its boundary contains the buyer storefront, the seller centre, and the backend capabilities behind a single API gateway. External actors and systems:

- **Visitor, Buyer, Seller** (human users)
- **Operator/Builder** (runs seed, eval, load and generator scripts; not an end-user role)
- **Payment provider** (mock; VNPay sandbox is Could)
- **Email provider** (Resend for the demo; Mailpit locally)
- **Identity provider** (Google, OpenID Connect)
- **Object storage** for uploaded images (local volume or emulator locally; cloud blob storage for the demo, OQ-30)
- **LLM and embedding provider** (free-tier or local)
- **Mock shipping provider**
- **Cloud hosting** (Azure, scale-to-zero, for the demo slice)

**Added by change request CR-7 (2026-11-16), Should:** sign in with a 6-digit one-time code sent to the account's email, on the storefront and seller centre, for existing accounts only. It complements, and never replaces, email-and-password and Google sign-in. No SMS codes and no sign-in links.

`hivespace.web` (Vue 3, kept locally in `references/hivespace.web/`, not committed) is a UI reference, not a dependency.

### 2.2 Product functions (summary)
Authentication (including Google sign-in, email one-time code sign-in and change password); user profile; image upload; localization (Vietnamese, English); currency; roles and authorization; shop setup; buyer addresses; catalog; keyword search; natural-language search; cart; checkout saga; inventory; payment; order lifecycle; mock shipping; reviews; transactional email; AI listing assist (Could); return request (Could); data generator (Could). Detailed in §3.1.

### 2.3 User classes & characteristics
| Class | Description | Needs |
| :-- | :-- | :-- |
| Visitor | Unauthenticated person | Browse and search; register |
| Buyer | Vietnamese-speaking shopper; every registered user | Search without knowing the product name; variants; COD; clear tracking; reviews; one checkout across shops |
| Seller | A buyer who registered a shop (individual or small business) | Low-friction start; list products with variants and stock; simple order handling |
| Operator/Builder | Solo builder running scripts and demos | Seed, eval, load test, data generation, mock-provider controls |
| Reviewer/recruiter | Watches the ≤3-minute demo or reads the repo | Not a system role; a first-class consumer of docs and demo (NFR-DEM, NFR-MNT) |

### 2.4 Operating environment
- Locally: one command (`dotnet run --project apphost`) starts the .NET Aspire AppHost, which brings up the full stack on a developer machine.
- Demo: a small slice on Azure (scale-to-zero container hosting), a pgvector-capable relational database, a message broker (Azure Service Bus, OQ-5 resolved by CR-3).
- Clients: current evergreen desktop and mobile browsers.

### 2.5 Constraints
| ID | Constraint |
| :-- | :-- |
| CON-1 | Solo builder, ~30 h/week, ~13 weeks (~390 h); MVP by Feb 2027 |
| CON-2 | Budget about $5/month for Azure and AI combined; heavy workloads run locally |
| CON-3 | Primary languages: .NET (transactional core), Go (inventory and cart), Python (AI, data and notification). Other technologies (for example Azure Functions or NoSQL databases) are allowed per service when recorded in an ADR for their learning value (CR-6) |
| CON-4 | A gateway that also acts as the backend for the frontend, plus nine separately deployable services with a database each (Identity, Catalog, Order, Payment, Inventory, Cart, Search+AI, Notification, Media) and one Azure Function (image processor) (OQ-9 resolved by CR-2; Notification as its own deployable and Media added by CR-3, Cart added by CR-5, see `docs/architecture.md`) |
| CON-5 | pgvector for semantic search; no Azure AI Search; no AKS |
| CON-6 | Backend, product model and data pipeline are owned by HiveSpace, not shaped around `hivespace.web` |
| CON-7 | Checkout, inventory and payment must stay correct; if schedule slips, the saga and oversell guarantee are never cut |
| CON-8 | Google sign-in needs a Google Cloud project with an OAuth consent screen and client credentials (OQ-32) |
| CON-9 | Resend sends to arbitrary recipients only from a verified domain and has free-tier limits; verify current terms (OQ-29) |

### 2.6 Assumptions & dependencies
- A-1: VND is the only enabled currency in the MVP, but money is modelled as amount plus currency code (FR-CUR, NFR-MNT-008..009); VND amounts are whole dong.
- A-2: Most users are in Vietnam; the UI, system messages and emails support Vietnamese and English (FR-LOC); seller-entered content is not translated.
- A-3: This is a demo marketplace with no real money movement; legal exposure under Vietnamese personal-data rules is limited by collecting minimal data (NFR-LEG-001).
- A-4: Checkout is all-or-nothing across sellers (OQ-16).
- A-5: Dependencies: Azure free-tier terms, LLM and embedding model choice, email free-tier limits, data-source decision (OQ-1, OQ-3, OQ-4).

## 3. Specific Requirements

### 3.1 Functional requirements

Priority key: **Must** = explicit PRD P0; **Should** = implied by P0, marked `Inferred`; **Could** = PRD P1. Source `US-n` = PRD §5 user story n; `P0-n`/`P1-n` = PRD §6 item.

#### 3.1.1 Authentication (Identity)

| ID | Requirement | Priority | Status | Source | Notes / rules |
| :-- | :---------- | :------- | :----- | :----- | :------------ |
| FR-AUTH-001 | When a visitor submits registration with a valid email address, a display name and a password, the system shall create a user account in the unverified state holding the Buyer role. | Must | Active | P0-1, US-17 | Email is the unique login identifier. Google sign-in also creates accounts (FR-AUTH-020). |
| FR-AUTH-002 | If the submitted email address is already registered, then the system shall reject the registration and state that the email is in use. | Must | Active | P0-1 | |
| FR-AUTH-003 | If the submitted password does not satisfy the password policy, then the system shall reject the registration or password change and list the unmet rules. | Must | Active | P0-1 | Policy in NFR-SEC-003. |
| FR-AUTH-004 | If a registration field is missing or malformed, then the system shall reject the request and identify each invalid field. | Must | Active | P0-1 | |
| FR-AUTH-005 | When a user opens a valid verification link, the system shall mark the account's email as verified. | Must | Active | P0-14, US-17 | Link lifetime 24 h (OQ-13). |
| FR-AUTH-006 | If a verification link is expired, already used, or invalid, then the system shall reject it and offer to send a new link. | Must | Active | P0-14 | |
| FR-AUTH-007 | When a user with an unverified account requests a new verification email, the system shall issue a new link and invalidate earlier links. | Should | Active | Inferred | Rate-limited per NFR-SEC-005. |
| FR-AUTH-008 | While a user's account is unverified, the system shall allow browsing and search but reject order placement. | Must | Active | P0-14 | Enforced at checkout, FR-CHK-013. Google-created accounts are verified. |
| FR-AUTH-009 | When a user submits correct credentials, the system shall sign the user in and issue an access token and a refresh token. | Must | Active | P0-1 | Unverified users may sign in (to resend verification). Lifetimes in NFR-SEC-004. |
| FR-AUTH-010 | If the submitted credentials are incorrect, then the system shall reject the sign-in with a message that does not reveal whether the email exists. | Must | Active | P0-1 | |
| FR-AUTH-011 | If five consecutive sign-in attempts for one account fail within 15 minutes, then the system shall lock the account for 15 minutes. | Should | Active | Inferred | Thresholds are proposals (OQ-21). Audit per NFR-SEC-009. |
| FR-AUTH-012 | While an account is locked, the system shall reject sign-in attempts without evaluating the submitted password. | Should | Active | Inferred | |
| FR-AUTH-013 | When a signed-in user signs out, the system shall revoke that user's refresh token. | Should | Active | Inferred | |
| FR-AUTH-014 | When a valid, unexpired refresh token is presented, the system shall issue a new access token. | Should | Active | Inferred | |
| FR-AUTH-015 | If a request to a protected endpoint carries a missing, malformed, expired or invalid-signature access token, then the gateway shall reject it as unauthenticated. | Must | Active | P0-1, CR-3 | Revocation (sign-out, password change or reset) applies to refresh tokens (FR-AUTH-013, FR-AUTH-014, NFR-SEC-015); an access token already issued stays valid until it expires (NFR-SEC-004, 15 minutes). Modified by CR-3. |
| FR-AUTH-016 | When a request carries a valid token, the gateway shall forward the request with the caller's identity and roles. | Must | Active | P0-1 | "Token accepted by the gateway." |
| FR-AUTH-017 | When a user requests a password reset for an email address, the system shall send a reset link if an account exists, and shall give the same response whether or not it exists. | Could | Active | P1-6 | Uses the email pipeline, FR-NOTIF-013. |
| FR-AUTH-018 | When a user submits a new compliant password through a valid reset link, the system shall update the password and revoke the user's refresh tokens. | Could | Active | P1-6 | |
| FR-AUTH-019 | If a password-reset link is expired, already used, or invalid, then the system shall reject it and offer to send a new link. | Could | Active | P1-6 | |
| FR-AUTH-020 | When a visitor completes Google sign-in with a verified Google email address that matches no existing account, the system shall create a verified account holding the Buyer role and sign the user in. | Must | Active | CR-1 | No verification email is sent. Google display name and picture prefill the profile. |
| FR-AUTH-021 | When a visitor completes Google sign-in with a verified Google email address that matches an existing account, the system shall link the Google identity to that account and sign the user in. | Must | Active | CR-1 | Auto-link by verified email; the owner is told per FR-NOTIF-015. |
| FR-AUTH-022 | If Google reports the email as unverified, the user denies consent, or the Google response fails validation, then the system shall reject the sign-in and keep the user signed out. | Must | Active | CR-1 | Validation per NFR-SEC-014. |
| FR-AUTH-023 | If Google is unavailable, then the system shall tell the user and keep email-and-password sign-in available. | Must | Active | CR-1 | NFR-REL-008. |
| FR-AUTH-024 | When a signed-in user submits the correct current password and a new password that satisfies the password policy, the system shall update the password and revoke the user's other refresh tokens. | Must | Active | CR-1 | Policy NFR-SEC-003; token rule NFR-SEC-015. |
| FR-AUTH-025 | If the current password submitted for a password change is incorrect, then the system shall reject the change. | Must | Active | CR-1 | Counts toward the lockout threshold of FR-AUTH-011. |
| FR-AUTH-026 | If an email-and-password sign-in is attempted for an account that has no password, then the system shall reject it with the same message as FR-AUTH-010. | Must | Active | CR-1 | Google-created accounts start without a password. |
| FR-AUTH-027 | When a user whose account has no password signs in with Google and submits a password that satisfies the policy, the system shall set the password. | Should | Active | CR-1, Inferred | Re-authentication rule is OQ-33. |
| FR-AUTH-028 | When a visitor requests a sign-in code for an email address that belongs to an account, the system shall send a 6-digit one-time code to that address and return a sign-in challenge. | Should | Active | CR-7 | Code lifetime 5 minutes (OQ-35). Email per FR-NOTIF-016. Offered on the storefront and seller centre; there is no MVP admin console. A code never creates an account. |
| FR-AUTH-029 | When a visitor requests a sign-in code, the system shall give the same response, with a challenge, whether or not an account exists for the email address. | Should | Active | CR-7 | Same principle as FR-AUTH-010 and FR-AUTH-017. No email is sent for an unknown address. |
| FR-AUTH-030 | When a user submits the correct code for an active challenge before the code expires, the system shall sign the user in, issue an access token and a refresh token, and end the challenge. | Should | Active | CR-7 | Tokens and lifetimes as FR-AUTH-009 and NFR-SEC-004. A code is single-use. |
| FR-AUTH-031 | If the submitted code is wrong or expired, or the challenge has ended, then the system shall reject the sign-in with a message that does not reveal whether the email exists. | Should | Active | CR-7 | A wrong code counts toward the account lockout of FR-AUTH-011. |
| FR-AUTH-032 | If five wrong codes are submitted for one challenge, then the system shall end the challenge and require the user to request a new code. | Should | Active | CR-7 | Threshold proposed (OQ-35). |
| FR-AUTH-033 | If a sign-in code is requested for an email address less than 60 seconds after the previous request for that address, then the system shall reject the request and state the remaining wait. | Should | Active | CR-7 | Applied per address whether or not an account exists (FR-AUTH-029). Threshold proposed (OQ-35). |
| FR-AUTH-034 | When a new sign-in code is issued for an account, the system shall invalidate every earlier code for that account. | Should | Active | CR-7 | |
| FR-AUTH-035 | While an account is locked, the system shall reject code sign-in without evaluating the submitted code. | Should | Active | CR-7 | Lockout per FR-AUTH-011 and FR-AUTH-012. |
| FR-AUTH-036 | When a user whose account is unverified signs in with a correct code, the system shall mark the account's email as verified. | Should | Active | CR-7 | Entering the code proves control of the inbox, as the verification link does (FR-AUTH-005). |

#### 3.1.2 Authorization & roles

| ID | Requirement | Priority | Status | Source | Notes / rules |
| :-- | :---------- | :------- | :----- | :----- | :------------ |
| FR-ROLE-001 | The system shall support the roles Buyer and Seller, and every registered user shall hold the Buyer role. | Must | Active | P0-1 | No Admin role in the MVP. |
| FR-ROLE-002 | When a buyer completes shop registration, the system shall grant the Seller role while retaining the Buyer role. | Must | Active | P0-1, US-11 | Buyer access is kept. |
| FR-ROLE-003 | If a user without the Seller role requests a seller-only function, then the system shall reject the request as forbidden. | Must | Active | P0-1 | |
| FR-ROLE-004 | If a user requests to view or change a cart, order, address, shop or product owned by another user, then the system shall reject the request. | Must | Active | Inferred | Applies to every owned resource; tested per NFR-SEC-006. |
| FR-ROLE-005 | The system shall evaluate authorization on the server for every protected request. | Must | Active | Inferred | UI hiding is not enforcement. |

#### 3.1.3 Shop and seller setup

| ID | Requirement | Priority | Status | Source | Notes / rules |
| :-- | :---------- | :------- | :----- | :----- | :------------ |
| FR-SHOP-001 | When a verified buyer submits shop registration with a shop name and a seller type (individual or business), the system shall create a shop owned by that user. | Must | Active | P0-1, US-11 | Verified email is the only entry requirement (OQ-24). |
| FR-SHOP-002 | If a user who already owns a shop submits shop registration, then the system shall reject it. | Should | Active | Inferred | One shop per user. |
| FR-SHOP-003 | If the submitted shop name is already used by another shop, then the system shall reject it. | Should | Active | Inferred | Case-insensitive. |
| FR-SHOP-004 | The system shall record the seller type without verifying it. | Must | Active | PRD §3 | No KYC. |
| FR-SHOP-005 | When a shop owner submits shop profile changes (name, description), the system shall save them. | Must | Active | P0-2, US-12 | |
| FR-SHOP-006 | When a shop owner submits a pickup address (contact name, phone, province, district, ward, street), the system shall save it. | Must | Active | P0-2, US-12 | |
| FR-SHOP-007 | When a shop owner submits payout bank details (bank name, account number, account holder), the system shall save them without initiating any payout. | Must | Active | P0-2, US-12 | |
| FR-SHOP-008 | If required shop fields are missing or invalid, then the system shall reject the submission and identify each invalid field. | Must | Active | P0-2 | |
| FR-SHOP-009 | When any user opens a shop page, the system shall show the shop name, logo, cover image, description, seller type and active products, and shall not show the pickup address or bank details. | Must | Active | P0-2, US-10 | PRD says fields are "shown on the shop page"; interpreted as owner settings view plus a public view without private fields (OQ-27). |
| FR-SHOP-010 | When a shop owner opens shop settings, the system shall show the profile (including logo and cover image), pickup address and bank details, with the bank account number masked except the last four digits. | Must | Active | P0-2 | NFR-SEC-008. |
| FR-SHOP-011 | If a seller attempts to publish a product before the shop has a pickup address and payout bank details, then the system shall reject the publication and state what is missing. | Should | Active | Inferred, US-12 | "…so that I can list products." |

#### 3.1.4 Buyer addresses

| ID | Requirement | Priority | Status | Source | Notes / rules |
| :-- | :---------- | :------- | :----- | :----- | :------------ |
| FR-ADDR-001 | When a buyer submits a delivery address (recipient name, phone, province, district, ward, street), the system shall save it to the buyer's address book. | Should | Active | Inferred | Needed for checkout and seller fulfilment (OQ-19). |
| FR-ADDR-002 | When a buyer edits or deletes a saved address, the system shall apply the change without altering the addresses already recorded on existing orders. | Should | Active | Inferred | Orders keep a snapshot. |
| FR-ADDR-003 | When a buyer marks an address as default, the system shall preselect it at checkout. | Could | Active | Inferred | Convenience. |
| FR-ADDR-004 | If an address field is missing or invalid, then the system shall reject the submission and identify each invalid field. | Should | Active | Inferred | |

#### 3.1.5 Catalog

| ID | Requirement | Priority | Status | Source | Notes / rules |
| :-- | :---------- | :------- | :----- | :----- | :------------ |
| FR-CAT-001 | When a seller submits a new product with title, description, category, attribute values and at least one variant, the system shall create the product owned by the seller's shop. | Must | Active | P0-3, US-13 | HiveSpace's own product model (OQ-2). |
| FR-CAT-002 | The system shall represent each product as one or more variants (SKUs), each with option values (such as size and colour), a price (amount and currency, FR-CUR-003) and a stock quantity. | Must | Active | P0-3, US-2 | A product with 2+ variants must be supported. |
| FR-CAT-003 | The system shall provide a predefined category tree from which the seller selects exactly one leaf category per product. | Must | Active | P0-3 | Tree is seeded, no category admin UI. Category names have vi and en text (FR-LOC-007). |
| FR-CAT-004 | Where a category defines attributes, the system shall require the seller to enter values for the attributes marked required. | Must | Active | P0-3 | |
| FR-CAT-005 | If product data is invalid (missing title, price not a positive amount valid for its currency (whole dong for VND), negative stock, duplicate variant option combination, or a value outside the product limits), then the system shall reject it and identify each error. | Must | Active | P0-3 | Product limits (FEAT-012 spec, 2026-11-25): title 10–120 characters; description 20–5,000 characters; at most 2 options, option name up to 20 characters, up to 20 values per option, value up to 30 characters; at most 50 variants; price 1,000–100,000,000 ₫; stock 0–999,999. |
| FR-CAT-006 | When a seller submits changes to their own product, the system shall save them. | Must | Active | P0-3, US-13 | |
| FR-CAT-007 | When a seller sets the stock quantity of a variant, the system shall update the variant's on-hand stock subject to FR-INV-010. | Must | Active | P0-3, US-13 | |
| FR-CAT-008 | When a seller deactivates a product, the system shall hide it from browse and search and prevent new cart additions, while keeping it on existing orders. | Should | Active | Inferred | Soft delete; no hard delete in MVP. |
| FR-CAT-009 | When a seller reactivates a deactivated product, the system shall show it in browse and search again. | Should | Active | Inferred | |
| FR-CAT-010 | When a seller opens the product list, the system shall show the seller's own products with status, variants and stock, in pages. | Should | Active | Inferred | |
| FR-CAT-011 | When a seller attaches images to a product, the system shall store up to five images per product and show them on the product page. | Must | Active | Inferred, CR-1 | Upload per FR-MEDIA-003; OQ-11 resolved. |
| FR-CAT-012 | When a buyer opens a product page, the system shall show title, description, images, attributes, variants with price and availability, the shop and its seller type, the return window and the rating summary. | Must | Active | P0-3, US-2, US-10 | |
| FR-CAT-013 | When a buyer selects a variant, the system shall show that variant's price and availability. | Must | Active | US-2 | |
| FR-CAT-014 | While a variant has no available stock, the system shall show it as sold out and prevent adding it to the cart. | Must | Active | US-15 | |
| FR-CAT-015 | The system shall show the platform's return window, in days, on every product page. | Must | Active | US-10 | Single platform-wide value (OQ-12). |
| FR-CAT-016 | When the operator runs the seed command, the system shall load at least 200 Vietnamese products, with sellers, categories, variants and stock, into the catalog. | Must | Active | P0-3 | One command; data source OQ-1. |
| FR-CAT-017 | When the operator runs the seed command more than once, the system shall not create duplicate records. | Should | Active | Inferred | |
| FR-CAT-018 | When a product is created, changed, deactivated or reactivated, the system shall reflect the change in browse and search results within 60 seconds. | Should | Active | Inferred | |
| FR-CAT-019 | When the operator loads the category tree, the system shall add new categories, attributes and list values, apply changed names and changes that loosen an attribute, and shall reject the whole load, identifying each problem, if it would remove or move a category, give a category that has products a child, remove an attribute or a list value, tighten an attribute, add a required attribute to a category that has products, give a leaf that has products its own attributes that leave out any attribute it inherited, or leave a vi or en name missing. | Must | Active | FEAT-014 spec (user decisions 2026-11-25, 2026-11-26) | Loosen: required → optional, single → multiple values, a higher maximum, a list that starts accepting other values. Tighten: optional → required, multiple → single, a lower maximum, list ↔ free text, a list that stops accepting other values. Inheritance: FR-CAT-023. Categories and attributes are identified by a stable key in the operator's file; categories keep the file's order. |
| FR-CAT-020 | Where a list attribute allows several values, the system shall accept up to that attribute's maximum number of values for a product. | Must | Active | FEAT-014 spec (user decision 2026-11-25) | Each list attribute defines single or multiple values and its maximum. |
| FR-CAT-021 | Where a top-level category has an image, the system shall show it with the category; otherwise it shall show a placeholder image. | Must | Active | FEAT-014 spec (user decision 2026-11-26) | Images come from the operator's category file (links to Tiki's category images, NFR-LEG-002 accepted by the owner); a later load may change or remove an image. Shown on the home page category bar (SCR-STF-001). |
| FR-CAT-022 | Where a list attribute accepts other values, the system shall accept for a product either values from its list or text the seller enters. | Must | Active | FEAT-014 spec (user decision 2026-11-26) | The operator's category file marks which list attributes accept other values (first used for long lists such as brand). Entered text is cleaned and length-limited, shown as written, and text equal to a list value ignoring case counts as that value (FEAT-012). Entered text does not become a list value; only the operator's file adds values. |
| FR-CAT-023 | Where a leaf category defines no attributes, the system shall apply the attributes of its nearest ancestor category that defines attributes. | Must | Active | FEAT-014 spec (user decision 2026-11-26) | A leaf with attributes of its own uses only those. |

#### 3.1.6 Search, filter and sort (keyword baseline)

| ID | Requirement | Priority | Status | Source | Notes / rules |
| :-- | :---------- | :------- | :----- | :----- | :------------ |
| FR-SRCH-001 | When a user submits a keyword query, the system shall return active products whose title or description matches the keywords. | Must | Active | P0-4 | |
| FR-SRCH-002 | When a user submits a query written without Vietnamese diacritics, the system shall also match products whose text matches the query with diacritics. | Should | Active | Inferred | "ao khoac" matches "áo khoác". |
| FR-SRCH-003 | When a user browses a category, the system shall list the active products in that category and its descendants. | Must | Active | P0-4, US-2 | |
| FR-SRCH-004 | When a user applies a category filter to a result set, the system shall restrict the results to that category. | Must | Active | P0-4 | |
| FR-SRCH-005 | When a user applies a minimum price, a maximum price, or both, the system shall restrict results to products with a variant priced in that range. | Must | Active | P0-4 | |
| FR-SRCH-006 | When a user selects a sort order (relevance for keyword queries, price ascending, price descending, newest), the system shall order the results accordingly. | Must | Active | P0-4 | |
| FR-SRCH-007 | When a user applies several filters and a sort order together, the system shall apply all of them to the result set. | Must | Active | P0-4 | |
| FR-SRCH-008 | The system shall return result lists in pages of 20 with the total result count. | Should | Active | Inferred | |
| FR-SRCH-009 | If a search returns no products, then the system shall say so and suggest broadening the query or filters. | Should | Active | Inferred | |
| FR-SRCH-010 | The system shall show on each result the image, title, price (or price range), rating summary and shop name. | Should | Active | Inferred, US-2 | |
| FR-SRCH-011 | The system shall allow browsing, keyword search and natural-language search without authentication. | Should | Active | Inferred | |

#### 3.1.7 Natural-language Vietnamese search (hero AI feature)

| ID | Requirement | Priority | Status | Source | Notes / rules |
| :-- | :---------- | :------- | :----- | :----- | :------------ |
| FR-NLS-001 | When a buyer submits a free-text Vietnamese query, the system shall interpret it into structured constraints (category, minimum and maximum price, attribute values) and a residual descriptive text. | Must | Active | P0-11, US-1 | Example: "áo khoác mỏng đi mưa dưới 300k". |
| FR-NLS-002 | The system shall interpret Vietnamese price shorthand in a query (for example "300k", "dưới 300k", "từ 200k đến 500k", "1tr") as VND amounts and bounds. | Must | Active | P0-11, US-1 | |
| FR-NLS-003 | When constraints have been interpreted, the system shall return the active products satisfying the hard constraints, ranked by semantic similarity to the residual text. | Must | Active | P0-11 | Embeddings stored in pgvector (CON-5). |
| FR-NLS-004 | When the system answers a natural-language query, it shall show the buyer the constraints it interpreted. | Should | Active | Inferred | Transparency; buyer can adjust via filters FR-SRCH-004..005. |
| FR-NLS-005 | If query interpretation fails, times out (NFR-PERF-003), or returns output that does not match the expected structure, then the system shall fall back to keyword search on the raw query. | Must | Active | P0-11, NFR-REL-008 | Search never errors because the LLM is down. |
| FR-NLS-006 | If the interpreted constraints match no product, then the system shall return the semantically closest products and tell the buyer there was no exact match. | Should | Active | Inferred | |
| FR-NLS-007 | When a product is created or changed, the system shall refresh its semantic embedding within 60 seconds. | Must | Active | P0-11 | |
| FR-NLS-008 | When seed data is loaded, the system shall generate embeddings for all seeded products. | Must | Active | P0-11 | |
| FR-NLS-009 | If a user exceeds the natural-language search rate limit, then the system shall reject the query with a retry-after message and offer keyword search. | Should | Active | Inferred | Protects the LLM budget (NFR-SEC-005, NFR-COST-002). |
| FR-NLS-010 | When the operator runs the evaluation script, the system shall execute each query in the evaluation set against natural-language search and against the keyword baseline and print the hit rate of each. | Must | Active | P0-11 | Runs from the repo. |
| FR-NLS-011 | The repository shall contain an evaluation set of at least 50 Vietnamese queries, each with a defined expected-result criterion. | Must | Active | P0-11 | Definition of "hit" is OQ-15. |
| FR-NLS-012 | When an evaluation run completes, the system shall record the hit rates in a results file in the repository, whatever the outcome. | Must | Active | P0-11 | |

#### 3.1.8 Cart

| ID | Requirement | Priority | Status | Source | Notes / rules |
| :-- | :---------- | :------- | :----- | :----- | :------------ |
| FR-CART-001 | When a signed-in buyer adds a variant with a quantity to the cart, the system shall add the line, or increase the quantity if the variant is already in the cart. | Must | Active | P0-5, US-3 | Maximum 99 per line. |
| FR-CART-002 | If the requested quantity plus the quantity already in the cart exceeds the variant's available stock, then the system shall reject the change and state the available quantity. | Must | Active | P0-5, US-15 | Advisory check; authoritative check is the reservation at checkout. |
| FR-CART-003 | If the variant is sold out or its product is deactivated, then the system shall reject adding it to the cart. | Must | Active | US-15 | |
| FR-CART-004 | The system shall show cart lines grouped by shop, with a subtotal per shop and a cart total. | Must | Active | P0-5, US-3 | |
| FR-CART-005 | When a buyer changes a line's quantity, the system shall update it subject to FR-CART-002. | Must | Active | P0-5 | |
| FR-CART-006 | When a buyer removes a line, the system shall remove it from the cart. | Must | Active | P0-5 | |
| FR-CART-007 | The system shall keep a buyer's cart across sign-outs and devices. | Should | Active | Inferred | |
| FR-CART-008 | While a cart line's variant has become unavailable, its quantity exceeds available stock, or its price has changed since it was added, the system shall flag the line and prevent checkout until the buyer resolves it. | Must | Active | US-15 | |
| FR-CART-009 | If a seller attempts to add a variant from their own shop to the cart, then the system shall reject it. | Should | Active | Inferred | |
| FR-CART-010 | If an unauthenticated visitor attempts to add to the cart, then the system shall prompt the visitor to sign in and keep the intended item. | Should | Active | Inferred | |

#### 3.1.9 Checkout saga

| ID | Requirement | Priority | Status | Source | Notes / rules |
| :-- | :---------- | :------- | :----- | :----- | :------------ |
| FR-CHK-001 | When a signed-in buyer starts checkout with selected cart lines, the system shall show a summary grouped per seller with items, subtotal, shipping fee, delivery address, payment method and total. | Must | Active | P0-5, US-4 | |
| FR-CHK-002 | If a buyer attempts to place an order without a delivery address or a payment method, then the system shall reject the attempt and name what is missing. | Must | Active | P0-5 | |
| FR-CHK-003 | When the buyer confirms checkout, the system shall create exactly one order per distinct seller among the selected lines, linked by one checkout identifier. | Must | Active | P0-5, US-4 | 3 sellers give 3 orders. |
| FR-CHK-004 | When the buyer confirms checkout, the system shall reserve stock for every selected line before any order is placed. | Must | Active | P0-5 | Via FR-INV-002. |
| FR-CHK-005 | If stock cannot be reserved for any selected line, then the system shall fail the whole checkout, release the reservations already made, create no orders, take no payment, and tell the buyer which items were insufficient. | Must | Active | P0-5, US-15 | All-or-nothing (OQ-16). |
| FR-CHK-006 | If any checkout step fails (stock reservation, order creation, payment initiation), then the system shall compensate all previously completed steps so that no orphan orders or held stock remain. | Must | Active | P0-5, P0-12 | Verified by failure injection, NFR-REL-002. |
| FR-CHK-007 | If a checkout step does not complete within the checkout step timeout, then the system shall treat the step as failed under FR-CHK-006. | Must | Active | P0-5 | Value in OQ-13. |
| FR-CHK-008 | If the buyer submits the same checkout request more than once, then the system shall create the orders once and return the original outcome on every repeat. | Must | Active | P0-5, US-16 | Double-click and retry safety. |
| FR-CHK-009 | The system shall record each order line's unit price, quantity and shipping fee as they were at checkout, and later price changes shall not alter existing orders. | Must | Active | P0-5 | |
| FR-CHK-010 | When checkout succeeds, the system shall remove the purchased lines from the cart. | Must | Active | P0-5 | |
| FR-CHK-011 | If an item's current price differs from the price shown at confirmation, then the system shall reject the confirmation and show the updated summary. | Should | Active | Inferred | |
| FR-CHK-012 | When checkout completes, the system shall show the buyer the outcome and the orders created, one per seller. | Must | Active | P0-5, US-4 | |
| FR-CHK-013 | If a buyer with an unverified account attempts to place an order, then the system shall reject it and offer to resend the verification email. | Must | Active | P0-14 | |

#### 3.1.10 Inventory

| ID | Requirement | Priority | Status | Source | Notes / rules |
| :-- | :---------- | :------- | :----- | :----- | :------------ |
| FR-INV-001 | The system shall maintain, for each variant, the on-hand quantity, the reserved quantity and the available quantity (on-hand minus reserved). | Must | Active | P0-6 | |
| FR-INV-002 | When a reservation of quantity q is requested for a variant with available quantity of at least q, the system shall reserve q units and record the reservation against the checkout and order line. | Must | Active | P0-6 | Atomic. |
| FR-INV-003 | If a reservation of quantity q is requested for a variant with available quantity below q, then the system shall reject it without changing stock. | Must | Active | P0-6 | |
| FR-INV-004 | The system shall never let a variant's available quantity fall below zero, under any interleaving of concurrent reservation requests. | Must | Active | P0-6, P0-10 | Proven by NFR-SCAL-001. |
| FR-INV-005 | If a reservation request repeats for the same checkout line, then the system shall return the existing reservation and shall not reserve again. | Must | Active | P0-6, P0-5 | Idempotent. |
| FR-INV-006 | When an order is cancelled before shipment, or checkout is compensated, the system shall release the order's reservations and return the units to available stock. | Must | Active | P0-6, US-8 | |
| FR-INV-007 | If a reservation is not converted to a placed order or confirmed payment within the reservation hold time, then the system shall release it. | Must | Active | P0-6 | "Release on timeout"; value in OQ-13. |
| FR-INV-008 | If a release request repeats for the same reservation, then the system shall return the units to stock only once. | Must | Active | P0-6 | |
| FR-INV-009 | When an order ships, the system shall mark its reservations as consumed, so the units stay deducted permanently. | Should | Active | Inferred | |
| FR-INV-010 | If a seller sets a variant's on-hand quantity below its reserved quantity, then the system shall reject the change and state the reserved quantity. | Should | Active | Inferred | |
| FR-INV-011 | The system shall record each stock movement (reserve, release, consume, adjust) with timestamp, quantity and reference. | Should | Active | Inferred | Supports audit and load-test verification. |
| FR-INV-012 | The system shall keep the reservation hold time configurable without code change. | Should | Active | Inferred | |

#### 3.1.11 Payment

| ID | Requirement | Priority | Status | Source | Notes / rules |
| :-- | :---------- | :------- | :----- | :----- | :------------ |
| FR-PAY-001 | Where the buyer selects cash on delivery, the system shall place the orders with payment status "COD pending" without contacting a payment provider. | Must | Active | P0-7, US-5 | COD is an order state, not a gateway. |
| FR-PAY-002 | When a COD order is marked delivered, the system shall record its payment as collected. | Must | Active | P0-7 | |
| FR-PAY-003 | Where the buyer selects online payment, the system shall create a payment for the checkout total with the payment provider, hold the orders as awaiting payment, and send the buyer to the payment step. | Must | Active | P0-7, US-5 | |
| FR-PAY-004 | Where the mock payment provider is in use, the system shall let the operator simulate success, failure, delay and duplicated callbacks for a payment. | Must | Active | P0-7 | Enables demo and failure-injection tests. |
| FR-PAY-005 | When a provider callback reports success for a pending payment, the system shall mark the payment paid and move the checkout's orders from awaiting payment to placed. | Must | Active | P0-7 | |
| FR-PAY-006 | When a provider callback reports failure for a pending payment, the system shall mark the payment failed and let the buyer retry within the payment window. | Must | Active | P0-7 | |
| FR-PAY-007 | If the same provider callback is delivered more than once, then the system shall apply its effects once and acknowledge the repeats. | Must | Active | P0-7, US-16 | Three deliveries give one effect. |
| FR-PAY-008 | If a provider callback fails authenticity verification, then the system shall reject it and make no state change. | Must | Active | P0-7 | |
| FR-PAY-009 | If a provider callback refers to an unknown payment, then the system shall reject it and log it. | Should | Active | Inferred | |
| FR-PAY-010 | If an online payment is not completed within the payment window, then the system shall cancel the checkout's orders and release their reservations. | Must | Active | P0-7, P0-6 | Value in OQ-13. |
| FR-PAY-011 | If a success callback arrives after the payment window has expired and the orders are cancelled, then the system shall leave the orders cancelled and refund the payment through the provider interface. | Should | Active | Inferred | |
| FR-PAY-012 | When an order with a paid online payment is cancelled, the system shall refund the order's amount through the provider interface and record the refund status. | Should | Active | Inferred | Mock refund (OQ-23). |
| FR-PAY-013 | When an order reaches completed, the system shall mark its payment as released to the seller, as a ledger state only, with no real payout. | Must | Active | US-7, P0-8 | |
| FR-PAY-014 | The system shall record each payment's status history (pending, paid, failed, refunded, released) with timestamps. | Must | Active | P0-7 | |
| FR-PAY-015 | When a buyer opens an order, the system shall show the order's payment method and status. | Should | Active | Inferred | |
| FR-PAY-016 | Where the VNPay sandbox is enabled, the system shall initiate and verify online payments through the VNPay sandbox instead of the mock provider. | Could | Active | P1-5 | |

#### 3.1.12 Order lifecycle

| ID | Requirement | Priority | Status | Source | Notes / rules |
| :-- | :---------- | :------- | :----- | :----- | :------------ |
| FR-ORD-001 | When an order is created, the system shall set its status to placed for COD, or awaiting payment for online payment until FR-PAY-005 occurs. | Must | Active | P0-8 | Awaiting payment is an internal pre-state of the PRD's six statuses. |
| FR-ORD-002 | The system shall allow only these transitions: awaiting payment to placed or cancelled; placed to confirmed or cancelled; confirmed to shipped or cancelled; shipped to delivered; delivered to completed. | Must | Active | P0-8 | |
| FR-ORD-003 | If a requested status change is not an allowed transition or the requester may not perform it, then the system shall reject it and leave the order unchanged. | Must | Active | P0-8 | |
| FR-ORD-004 | The system shall record every status change with its time and actor, and show the history as a timeline to the buyer and the seller. | Must | Active | P0-8, US-6 | |
| FR-ORD-005 | When a seller confirms a placed order, the system shall change its status to confirmed. | Must | Active | P0-8, US-14 | |
| FR-ORD-006 | When a seller ships a confirmed order, the system shall change its status to shipped and record the mocked tracking number. | Must | Active | P0-8, US-14 | |
| FR-ORD-007 | When the mocked shipping status reports a shipped order delivered, the system shall change its status to delivered. | Must | Active | P0-8 | See FR-SHIP-003. |
| FR-ORD-008 | When a buyer confirms receipt of a delivered order, the system shall change its status to completed. | Must | Active | P0-8, US-7 | |
| FR-ORD-009 | If a delivered order is not confirmed by the buyer within the auto-completion period, then the system shall change its status to completed. | Must | Active | P0-8, US-7 | Value in OQ-14. |
| FR-ORD-010 | When a buyer cancels an order that is placed or confirmed, the system shall change its status to cancelled and release its reservations. | Must | Active | P0-8, US-8 | |
| FR-ORD-011 | When a seller cancels an order that is placed or confirmed, giving a reason, the system shall change its status to cancelled and release its reservations. | Must | Active | P0-8, US-14 | |
| FR-ORD-012 | If a cancellation is requested for an order that is shipped, delivered or completed, then the system shall reject it. | Must | Active | P0-8, US-8 | Post-delivery returns are FR-RTN (Could). |
| FR-ORD-013 | When a buyer opens the order list or an order, the system shall show orders newest first in pages, filterable by status, and for an order its items, amounts, delivery address, payment status and timeline. | Must | Active | P0-8, US-6 | |
| FR-ORD-014 | When a seller opens the order list or an order of their shop, the system shall show it filterable by status, with the items, amounts, buyer's delivery name, phone and address, and timeline. | Must | Active | P0-8, US-14 | Buyer details only for own-shop orders. |
| FR-ORD-015 | The system shall show a buyer's orders from the same checkout grouped together. | Should | Active | Inferred, US-3 | |
| FR-ORD-016 | The system shall assign each order a unique, human-readable order number. | Should | Active | Inferred | |

#### 3.1.13 Shipping (mocked)

| ID | Requirement | Priority | Status | Source | Notes / rules |
| :-- | :---------- | :------- | :----- | :----- | :------------ |
| FR-SHIP-001 | When an order is priced at checkout, the system shall add a mocked shipping fee obtained through a shipping interface. | Must | Active | P0-8 | Rule is OQ-22; no carrier integration. |
| FR-SHIP-002 | When an order is shipped, the system shall generate a mocked tracking number and record mocked shipping status events. | Must | Active | P0-8 | |
| FR-SHIP-003 | Where the mock shipping provider is in use, the system shall move shipped orders to delivered after a configurable delay or on an operator command. | Must | Active | P0-8 | Lets the hero flow complete within the demo (OQ-17). |

#### 3.1.14 Ratings and reviews

| ID | Requirement | Priority | Status | Source | Notes / rules |
| :-- | :---------- | :------- | :----- | :----- | :------------ |
| FR-REV-001 | When a buyer submits a rating from 1 to 5, with optional text, for an item of their own completed order, the system shall create the review. | Must | Active | P0-9, US-9 | |
| FR-REV-002 | If the item is not in a completed order of the submitting buyer, then the system shall reject the review. | Must | Active | P0-9 | Verified purchase only. |
| FR-REV-003 | If a review already exists for that order item, then the system shall reject a second review. | Must | Active | P0-9 | One review per order item. |
| FR-REV-004 | If the rating is missing or outside 1 to 5, or the text exceeds 2000 characters, then the system shall reject the review. | Must | Active | P0-9 | |
| FR-REV-005 | When a user opens a product page, the system shall list its reviews newest first in pages, each with rating, text, date, a masked reviewer name and a verified-purchase marker. | Must | Active | P0-9, US-9 | |
| FR-REV-006 | The system shall show each product's average rating and review count on the product page and in result lists. | Should | Active | Inferred, US-9 | |
| FR-REV-007 | When a review is created, the system shall update the product's average rating and count within one minute. | Should | Active | Inferred | |
| FR-REV-008 | When the review-summary batch job runs, the system shall generate a text summary of the reviews for each product with at least the minimum number of reviews. | Could | Active | P1-1 | Minimum count proposed 5; batch, not request-time. |
| FR-REV-009 | Where a review summary exists for a product, the system shall show it on the product page labelled as AI-generated. | Could | Active | P1-1 | |
| FR-REV-010 | If summary generation fails for a product, then the system shall keep any previous summary and continue with the other products. | Could | Active | P1-1 | |

#### 3.1.15 Notifications (email)

| ID | Requirement | Priority | Status | Source | Notes / rules |
| :-- | :---------- | :------- | :----- | :----- | :------------ |
| FR-NOTIF-001 | When an account-registered, order-placed, order-shipped or order-cancelled event occurs, the system shall create the corresponding email for each recipient. | Must | Active | P0-14 | Notification is owned separately from Order. |
| FR-NOTIF-002 | When an account is created, the system shall send the user a verification email containing the verification link. | Must | Active | P0-14, US-17 | |
| FR-NOTIF-003 | When an order enters the placed status, the system shall send the buyer an order-placed email for that order. | Must | Active | P0-14, US-18 | One email per order (OQ-26). For online payment this follows payment success. |
| FR-NOTIF-004 | When an order enters the placed status, the system shall send the order's seller a new-order email. | Must | Active | P0-14, US-19 | |
| FR-NOTIF-005 | When an order is shipped, the system shall send the buyer an order-shipped email. | Must | Active | P0-14, US-18 | |
| FR-NOTIF-006 | When an order is cancelled, the system shall send the buyer an order-cancelled email. | Must | Active | P0-14, US-18 | |
| FR-NOTIF-007 | When a buyer cancels an order, the system shall send the order's seller an order-cancelled email. | Should | Active | Inferred | |
| FR-NOTIF-008 | The system shall render emails from templates in the recipient's language (Vietnamese or English, FR-LOC-005) that include the order number, items and totals where applicable. | Must | Active | P0-14 | |
| FR-NOTIF-009 | If sending an email fails, then the system shall retry it per NFR-REL-004 and shall not block or fail the originating operation. | Must | Active | P0-14 | Checkout is not blocked. |
| FR-NOTIF-010 | If the same event is delivered more than once, then the system shall send exactly one email per recipient for that event. | Must | Active | P0-14 | |
| FR-NOTIF-011 | If all retries for an email are exhausted, then the system shall mark it failed and log it for inspection. | Should | Active | Inferred | |
| FR-NOTIF-012 | The system shall record each email's status (queued, sent, failed) per recipient and event. | Should | Active | Inferred | |
| FR-NOTIF-013 | When a password reset is requested for an existing account, the system shall send the reset email through the same pipeline. | Could | Active | P1-6 | |
| FR-NOTIF-014 | When a user's password is changed, the system shall send the user a password-changed security email. | Should | Active | CR-1, Inferred | |
| FR-NOTIF-015 | When a Google identity is linked to an existing account, the system shall send the account owner a notice email. | Should | Active | CR-1, Inferred | |
| FR-NOTIF-016 | When a sign-in code is issued, the system shall send the account's email address a sign-in code email containing the code and how long it stays valid. | Should | Active | CR-7 | Language per FR-LOC-005. If email sending is unavailable the user can still sign in with a password or Google (NFR-REL-008). |

#### 3.1.16 AI listing assist (Could)

| ID | Requirement | Priority | Status | Source | Notes / rules |
| :-- | :---------- | :------- | :----- | :----- | :------------ |
| FR-LAS-001 | When a seller requests an AI draft with short input text (keywords, attributes), the system shall generate a Vietnamese draft title and description. | Could | Active | P1-2 | Text only; no image understanding. |
| FR-LAS-002 | The system shall present the draft as editable text and shall not save it to the product until the seller saves. | Could | Active | P1-2 | |
| FR-LAS-003 | If draft generation fails or times out, then the system shall tell the seller and keep manual entry available. | Could | Active | P1-2 | |

#### 3.1.17 Return request (Could)

| ID | Requirement | Priority | Status | Source | Notes / rules |
| :-- | :---------- | :------- | :----- | :----- | :------------ |
| FR-RTN-001 | When a buyer requests a return for a delivered order within the return window, giving a reason, the system shall create a return request. | Could | Active | P1-3 | One happy path only. |
| FR-RTN-002 | If the order is not delivered or the return window has passed, then the system shall reject the return request. | Could | Active | P1-3 | |
| FR-RTN-003 | When a seller approves a return request, the system shall mark the order returned and refund the buyer through the provider interface. | Could | Active | P1-3 | Restocking and rejection flows are not specified in the MVP. |

#### 3.1.18 Large-scale data generator (Could)

| ID | Requirement | Priority | Status | Source | Notes / rules |
| :-- | :---------- | :------- | :----- | :----- | :------------ |
| FR-GEN-001 | When the operator runs the data generator with a target order count, the system shall generate synthetic users, shops, products and orders up to that count in the local environment. | Could | Active | P1-4 | Target 1M orders; fallback 100k. |
| FR-GEN-002 | When the operator supplies a random seed, the system shall generate the same dataset on each run. | Could | Active | P1-4 | |
| FR-GEN-003 | If the generator is pointed at the deployed demo environment, then it shall refuse to run. | Could | Active | P1-4 | Protects budget. |

#### 3.1.19 User profile

| ID | Requirement | Priority | Status | Source | Notes / rules |
| :-- | :---------- | :------- | :----- | :----- | :------------ |
| FR-PROF-001 | When a signed-in user opens their profile, the system shall show display name, email, phone, gender, date of birth, avatar and preferred language. | Must | Active | CR-1 | |
| FR-PROF-002 | When a signed-in user submits changes to display name, phone, gender or date of birth, the system shall save them. | Must | Active | CR-1 | Gender values: male, female, other, prefer not to say. Display name required; the rest optional. |
| FR-PROF-003 | If a profile field is invalid (display name empty or over 80 characters, phone not a valid Vietnamese number, date of birth in the future or implying an age over 120), then the system shall reject the change and identify each invalid field. | Must | Active | CR-1 | |
| FR-PROF-004 | The system shall show the account email as read-only on the profile. | Must | Active | CR-1 | Email change is out of scope. |
| FR-PROF-005 | When any user views another user's information (for example a review author), the system shall show only the masked display name and avatar, never email, phone, gender or date of birth. | Must | Active | CR-1 | |
| FR-PROF-006 | When a signed-in user selects a preferred language, the system shall save it to the profile. | Must | Active | CR-1 | Drives FR-LOC-002 and FR-LOC-005. |

#### 3.1.20 Image upload

| ID | Requirement | Priority | Status | Source | Notes / rules |
| :-- | :---------- | :------- | :----- | :----- | :------------ |
| FR-MEDIA-001 | When a signed-in user uploads an avatar image, the system shall store it and show it on the user's profile. | Must | Active | CR-1 | Limit 2 MB (OQ-30). |
| FR-MEDIA-002 | When a shop owner uploads a shop logo or shop cover image, the system shall store it and show it on the shop page. | Must | Active | CR-1 | One logo, one cover (OQ-31). Limit 5 MB. |
| FR-MEDIA-003 | When a seller uploads product images, the system shall store up to five per product in a seller-chosen order, the first being the main image. | Must | Active | CR-1 | Limit 5 MB each. Realises FR-CAT-011. |
| FR-MEDIA-004 | If an uploaded file is not a JPEG, PNG or WebP image, exceeds its size limit, or is not a valid image, then the system shall reject it and state the accepted types and the limit. | Must | Active | CR-1 | NFR-SEC-013. |
| FR-MEDIA-005 | When a user replaces or removes an image, the system shall make the previous file inaccessible. | Must | Active | CR-1 | |
| FR-MEDIA-006 | Where an image is shown in a list or card, the system shall serve a downsized version of it. | Should | Active | CR-1, Inferred | |
| FR-MEDIA-007 | When the system stores an uploaded image, it shall remove embedded metadata such as location. | Should | Active | CR-1, Inferred | Privacy. |
| FR-MEDIA-008 | If image storage is unavailable, then the system shall reject the upload, keep the existing image, and tell the user to retry. | Must | Active | CR-1 | NFR-REL-008. |

#### 3.1.21 Localization (Vietnamese and English)

| ID | Requirement | Priority | Status | Source | Notes / rules |
| :-- | :---------- | :------- | :----- | :----- | :------------ |
| FR-LOC-001 | The system shall provide the UI, system messages and emails in Vietnamese (vi) and English (en), with vi the default. | Must | Active | CR-1 | |
| FR-LOC-002 | When a user selects a language, the system shall switch the UI at once and save the choice to the profile if the user is signed in, otherwise to the browser. | Must | Active | CR-1 | |
| FR-LOC-003 | When a visitor with no saved language opens the site, the system shall use English if the browser prefers English, otherwise Vietnamese. | Must | Active | CR-1 | |
| FR-LOC-004 | When the system rejects a request, it shall return a stable error code and a message in the request's language. | Must | Active | CR-1 | |
| FR-LOC-005 | When the system sends an email, it shall use the recipient's saved language, and for the registration verification email the language in use at registration. | Must | Active | CR-1 | |
| FR-LOC-006 | The system shall format dates, numbers and money according to the active language. | Must | Active | CR-1 | Money per FR-CUR-002. |
| FR-LOC-007 | The system shall provide Vietnamese and English names for platform-owned content (categories, attribute names, order, payment and shipping statuses). | Must | Active | CR-1 | Seller-entered content is shown as written, not translated. |
| FR-LOC-008 | If a translation is missing for the active language, then the system shall show the Vietnamese text. | Must | Active | CR-1 | |

#### 3.1.22 Currency and money

| ID | Requirement | Priority | Status | Source | Notes / rules |
| :-- | :---------- | :------- | :----- | :----- | :------------ |
| FR-CUR-001 | The system shall keep a currency table (code, symbol, minor-unit exponent) and shall enable only VND (exponent 0) in the MVP. | Must | Active | CR-1 | No currency conversion in the MVP. |
| FR-CUR-002 | When the system shows a money amount, it shall format it according to the currency and the active language. | Must | Active | CR-1 | For example "300.000 ₫" in vi and "₫300,000" in en (OQ-34). |
| FR-CUR-003 | The system shall store every monetary amount (price, shipping fee, order total, payment, refund) together with its currency code. | Must | Active | CR-1 | NFR-MNT-008. |
| FR-CUR-004 | If selected checkout lines or an order would combine more than one currency, then the system shall reject the checkout. | Must | Active | CR-1 | Keeps one currency per order when currencies are added. |
| FR-CUR-005 | When the system calls a payment provider, it shall send the amount together with the currency code. | Must | Active | CR-1 | |

### 3.2 External interface requirements

| ID | Requirement | Priority | Status | Source |
| :-- | :---------- | :------- | :----- | :----- |
| EIR-UI-001 | The system shall provide a web buyer storefront covering browse, search, product page, cart, checkout, orders and reviews. | Must | Active | P0, US-1..10 |
| EIR-UI-002 | The system shall provide a web seller centre covering shop setup, products, stock and orders. | Must | Active | P0, US-11..14 |
| EIR-UI-003 | The web UIs shall work on mobile browsers at 360 px width and on desktop. | Should | Active | Inferred |
| EIR-UI-004 | The web UIs shall be available in Vietnamese and English (FR-LOC-001). | Must | Active | CR-1 |
| EIR-HW-001 | The system has no hardware interfaces. | Must | Active | n/a |
| EIR-SW-001 | The system shall call an LLM through a provider interface for query interpretation, and for listing assist and review summaries where those are built. | Must | Active | P0-11 |
| EIR-SW-002 | The system shall generate multilingual text embeddings through a provider interface using a model that runs on CPU or a free tier. | Must | Active | P0-11 |
| EIR-SW-003 | The system shall send email through an email-provider interface, through Resend for the demo and with Mailpit locally. | Must | Active | P0-14, CR-1 |
| EIR-SW-004 | The system shall access payment through a payment-provider interface implemented by a mock provider, with VNPay sandbox as an optional second implementation. | Must | Active | P0-7, P1-5 |
| EIR-SW-005 | The system shall obtain shipping fees and status through a shipping interface implemented by a mock provider. | Must | Active | P0-8 |
| EIR-SW-006 | The system shall authenticate Google sign-ins through Google's OpenID Connect interface. | Must | Active | CR-1 |
| EIR-SW-007 | The system shall store uploaded images through an object-storage interface (local volume or emulator locally, cloud blob storage for the demo). | Must | Active | CR-1 |
| EIR-COM-001 | Clients shall reach all backend capabilities through a single HTTPS API gateway. | Must | Active | P0-1 |
| EIR-COM-002 | The system shall accept inbound payment-provider callbacks over HTTPS and authenticate each by signature. | Must | Active | P0-7 |
| EIR-COM-003 | The backend capabilities shall exchange domain events asynchronously through a message broker (Azure Service Bus; Azure Service Bus emulator locally, RabbitMQ as an optional implementation; OQ-5 resolved by CR-3). | Must | Active | P0-5, P0-14 |
| EIR-COM-004 | The system shall export traces, metrics and logs in OpenTelemetry format. | Must | Active | P0-12 |
| EIR-COM-005 | The system shall make outbound HTTPS calls to Google, Resend and the object store, each authenticated with credentials held outside the repository. | Must | Active | CR-1 |

### 3.3 Non-functional requirements

| ID | Category | Requirement (metric, target, condition) | Priority | Source |
| :-- | :------- | :-------------------------------------- | :------- | :----- |
| NFR-PERF-001 | Performance | 95% of product-page and category-browse requests complete within 300 ms at 50 concurrent users, on the local Aspire-orchestrated stack with the seed data. | Should | Inferred (OQ-21) |
| NFR-PERF-002 | Performance | 95% of keyword searches complete within 500 ms at 20 concurrent users with 10,000 products loaded. | Should | Inferred (OQ-21) |
| NFR-PERF-003 | Performance | 95% of natural-language searches, including query interpretation, return within 5 s; interpretation that exceeds 3 s triggers the fallback in FR-NLS-005. | Should | Inferred (OQ-21) |
| NFR-PERF-004 | Performance | 95% of checkout confirmations for carts with up to 5 sellers complete within 3 s at 20 concurrent checkouts. | Should | Inferred (OQ-21) |
| NFR-PERF-005 | Performance | The inventory reservation path shall record its p95 latency in the k6 report at 200 concurrent buyers; target p95 ≤ 200 ms (proposed; the PRD only requires it be recorded). | Must | P0-10 |
| NFR-PERF-006 | Performance | After scale-to-zero idle, the first request on the deployed demo shall respond within 30 s. | Could | Inferred |
| NFR-PERF-007 | Performance | 95% of image uploads up to 5 MB shall be processed and visible within 5 s. | Must | CR-1 |
| NFR-SCAL-001 | Scalability | With 10 units of one variant and 200 concurrent buyers each attempting to buy 1, exactly 10 reservations succeed and stock never goes below 0; the run is repeated 3 times and the k6 reports are committed. | Must | P0-6, P0-10 |
| NFR-SCAL-002 | Scalability | The system shall meet NFR-PERF-001..004 with up to 10,000 products and 100,000 orders in the database. | Should | Inferred |
| NFR-SCAL-003 | Scalability | Where the generator is built, the load test shall run against 1,000,000 orders, or 100,000 if the target is not met in time. | Could | P1-4 |
| NFR-REL-001 | Reliability | Across all automated and load tests, the count of oversold units shall be 0. | Must | P0-6, P0-10 |
| NFR-REL-002 | Reliability | An automated test shall inject a failure at each checkout step (stock, order creation, payment initiation, timeout) and verify 0 orphan orders and 0 held reservations afterward. | Must | P0-5 |
| NFR-REL-003 | Reliability | An automated test shall deliver the same payment callback 3 times and verify exactly 1 payment effect and 1 order transition. | Must | P0-7 |
| NFR-REL-004 | Reliability | A failed email send shall be retried up to 5 times with exponential backoff, within 1 hour in total, before being marked failed. | Must | P0-14 |
| NFR-REL-005 | Reliability | Every event consumer shall tolerate at-least-once delivery, producing no duplicate side effect when an event is delivered more than once. | Must | P0-7, P0-14 |
| NFR-REL-006 | Reliability | The deployed demo has best-effort availability with no uptime target; it shall be available during demo and review windows. | Should | Inferred |
| NFR-REL-007 | Reliability | The full system state shall be restorable from scripts (migrations plus seed) in 10 minutes or less on a clean environment. | Should | Inferred |
| NFR-REL-008 | Reliability | Failure of the LLM, embedding, email (Resend), image storage, Google sign-in or mock-shipping dependency shall not make checkout, cart, catalog browse, keyword search or email-and-password sign-in fail. | Must | P0-11, P0-14 |
| NFR-SEC-001 | Security | All traffic to the deployed demo shall use TLS 1.2 or higher. | Must | Inferred |
| NFR-SEC-002 | Security | Passwords shall be stored only as salted hashes using argon2id or bcrypt, never in plain text or reversible form. | Must | P0-1 |
| NFR-SEC-003 | Security | The password policy requires at least 8 characters including at least one letter and one digit. | Must | P0-1 (proposed) |
| NFR-SEC-004 | Security | Access tokens shall expire within 15 minutes and refresh tokens within 7 days. | Must | P0-1 (proposed), CR-3 |
| NFR-SEC-005 | Security | Rate limits: register, sign-in, resend-verification, sign-in code request and sign-in code submit at most 10 requests per minute per IP; natural-language search at most 20 per minute per user or IP; change password, the Google sign-in callback and image upload at most 10 per minute per user; excess requests receive a rate-limit response. | Should | Inferred, CR-7 |
| NFR-SEC-006 | Security | Automated tests shall verify that a user cannot read or change another user's resources (cart, order, address, shop, product) and that SQL-injection inputs on key endpoints are rejected. | Should | Inferred |
| NFR-SEC-007 | Security | Secrets shall not be stored in the repository; CI shall fail on detected secrets. | Must | P0-12 |
| NFR-SEC-008 | Security | Bank account numbers shall be encrypted at rest and shown masked except the last 4 digits. | Should | Inferred |
| NFR-SEC-009 | Security | The system shall keep an audit log, retained at least 90 days, of sign-in success and failure (password, Google and one-time code), lockouts, role grants, order status changes and payment status changes. | Should | Inferred, CR-7 |
| NFR-SEC-010 | Security | The system shall pass user text to the LLM only as data, shall validate LLM output against a fixed schema, and shall allow it no capability beyond producing search constraints. | Must | P0-11 |
| NFR-SEC-011 | Security | The system shall neither store nor process payment card numbers. | Must | P0-7 |
| NFR-SEC-012 | Security | CI shall fail on known high-severity vulnerabilities in dependencies. | Should | Inferred |
| NFR-SEC-013 | Security | Uploaded images shall be validated by file signature rather than extension, re-encoded on the server and stored under random names; SVG and executable content shall be rejected. | Must | CR-1 |
| NFR-SEC-014 | Security | Google sign-in shall use the authorization-code flow with PKCE, verify state and nonce, validate the ID token's signature, issuer, audience and expiry, and request only the openid, email and profile scopes. | Must | CR-1 |
| NFR-SEC-015 | Security | After a password change or reset, refresh tokens issued before the change shall be rejected. | Must | CR-1 |
| NFR-SEC-016 | Security | Sign-in codes shall be generated with a cryptographically secure random generator, stored only as a hash, compared in constant time, and accepted at most once. | Must | CR-7 |
| NFR-USE-001 | Usability | The hero flow (search, cart, split checkout, pay, track, review) shall be completable in 3 minutes or less in a recorded demo. | Must | P0-13, §7 |
| NFR-USE-002 | Usability | Every list and asynchronous action shall have defined loading, empty and error states, with error messages that say what the user can do next. | Should | Inferred |
| NFR-USE-003 | Usability | The buyer hero-flow screens shall meet WCAG 2.1 level AA. | Could | Inferred |
| NFR-COMPAT-001 | Compatibility | The UIs shall work on the latest two versions of Chrome, Edge, Firefox and Safari on desktop, and Chrome and Safari on mobile. | Should | Inferred |
| NFR-MNT-001 | Maintainability | CI shall build, test and lint on every push, and main shall stay green. | Must | P0-12 |
| NFR-MNT-002 | Maintainability | The repository shall contain at least 5 ADRs: service boundaries, message broker, search store, payment idempotency, hosting. | Must | P0-12, §7 |
| NFR-MNT-003 | Maintainability | The repository shall contain an architecture diagram and a README with run steps. | Must | P0-12 |
| NFR-MNT-004 | Maintainability | The deployed slice shall be defined as infrastructure as code. | Must | P0-12 |
| NFR-MNT-005 | Maintainability | Automated tests from NFR-REL-002, NFR-REL-003 and the oversell concurrency test shall run from the repository with documented commands. | Must | P0-10, P0-12 |
| NFR-MNT-006 | Maintainability | Payment, email, shipping, LLM, embedding, identity (Google) and object-storage providers shall each sit behind an interface so an implementation can be swapped without changes to calling code. | Must | P0-7, P0-14, P2 |
| NFR-MNT-007 | Maintainability | The order-total calculation shall be isolated so adding discounts later does not require rewriting checkout. | Should | P2 |
| NFR-MNT-008 | Maintainability | Every monetary amount in storage, APIs, events and logs shall carry an amount and a currency code; bare numeric money values are not allowed. | Must | CR-1 |
| NFR-MNT-009 | Maintainability | Adding a currency shall require only currency configuration, formatting rules and a payment-provider capability, with no change to cart, checkout, order or inventory logic. | Must | CR-1 |
| NFR-MNT-010 | Maintainability | Image storage shall sit behind an interface so the local and cloud implementations can be swapped without changes to calling code. | Must | CR-1 |
| NFR-PORT-001 | Portability | A fresh clone shall run the full stack with one command through the .NET Aspire AppHost (`dotnet run --project apphost`) and no manual steps beyond documented prerequisites. | Must | P0-12, CR-2 |
| NFR-PORT-002 | Portability | The demo slice shall run on Azure container hosting that scales to zero. | Must | P0-13 |
| NFR-OBS-001 | Observability | A checkout shall produce one OpenTelemetry trace with spans across the gateway and each service it touches. | Must | P0-12 |
| NFR-OBS-002 | Observability | Every service shall write structured logs carrying a correlation identifier and shall expose a health endpoint. | Should | Inferred |
| NFR-OBS-003 | Observability | The k6 load-test reports shall be committed to the repository. | Must | P0-10 |
| NFR-COST-001 | Cost | Azure plus AI cost shall stay under $5 per month, with a budget alert configured. | Must | G4, §7 |
| NFR-COST-002 | Cost | LLM and embedding usage shall be bounded by a configurable cap, and the system shall degrade to keyword search when the cap is reached. | Should | Inferred |
| NFR-COST-003 | Cost | Batch embeddings, data generation and load tests shall run in the local environment, not on the deployed demo. | Must | G4 |
| NFR-COST-004 | Cost | Image storage and email sending shall stay within the $5 per month budget and within the free-tier limits of the chosen providers. | Must | CR-1 |
| NFR-L10N-001 | Localization | Money shall be handled as an amount plus currency code and shown according to the currency's minor unit (VND as whole dong). | Must | A-1, CR-1 |
| NFR-L10N-002 | Localization | Timestamps shall be stored in UTC and shown in Asia/Ho_Chi_Minh, with date and number formats following the active locale. | Should | Inferred |
| NFR-L10N-003 | Localization | The system shall store, search and email Vietnamese text with full diacritic support (UTF-8 throughout). | Must | P0-11, P0-14 |
| NFR-L10N-004 | Localization | CI shall fail when the Vietnamese and English translation sets contain different keys. | Must | CR-1 |
| NFR-LEG-001 | Compliance | The system shall collect only email, name, phone, address, payout bank fields and the optional gender, date of birth and avatar, and the demo shall state that no real transactions occur. | Should | Inferred (OQ-18) |
| NFR-LEG-002 | Compliance | Seed data shall come from a source whose terms permit its use, recorded in the repository. | Must | OQ-1 |
| NFR-DEM-001 | Demo | The deployed URL shall let a user complete a checkout. | Must | P0-13 |
| NFR-DEM-002 | Demo | A recorded demo video of the hero flow shall be 3 minutes or less. | Must | P0-13 |
| NFR-DEM-003 | Demo | The hero flow shall run with no manual database edits, and 10 orders from 3 seller accounts shall reach completed through the UI. | Must | §7 |

### 3.4 Other requirements
Compliance, localization, retention and demo items are covered in §3.3 (NFR-LEG, NFR-L10N, NFR-DEM). Data retention: audit logs 90 days (NFR-SEC-009); all other retention is unspecified in the MVP (OQ-18).

## Appendix A — Glossary

| Term | Meaning |
| :-- | :-- |
| Variant / SKU | A purchasable combination of option values (for example size M, colour red) with its own price and stock |
| On-hand / reserved / available | Physical units / units held for pending orders / on-hand minus reserved |
| Reservation | A hold on variant stock tied to a checkout line |
| Checkout | One buyer action that turns selected cart lines into one order per seller |
| Saga | The multi-step checkout process with compensation (undo) steps when a step fails |
| Compensation | Undoing the effects of completed steps after a later step fails |
| Awaiting payment | Internal order pre-state for online payment before success |
| Idempotent | Repeating an operation yields the same result with no extra effect |
| COD | Cash on delivery |
| Mock provider | A fake implementation of an external service (payment, shipping) with controllable behaviour |
| NL search | Natural-language Vietnamese search using LLM interpretation and embeddings |
| Hit rate | Share of evaluation queries whose result satisfies the expected-result criterion |
| Return window | Platform-wide number of days after delivery in which a return may be requested |
| Seller type | Individual or business; recorded, not verified |
| Money | An amount together with a currency code; never a bare number |
| Currency | An entry in the currency table (code, symbol, minor-unit exponent); VND is the only one enabled in the MVP |
| Locale / language | Vietnamese (vi) or English (en) for UI, system messages and emails |
| Object storage | Service holding uploaded image files, accessed through an interface |
| Auto-link | Attaching a Google identity to an existing account with the same verified email |
| One-time code (OTP) | A short-lived, single-use 6-digit code sent by email that signs the user in instead of a password (FR-AUTH-028..036) |
| ADR | Architecture decision record |
| PDPD | Vietnamese personal data protection rules (Decree 13/2023/ND-CP) |

## Appendix B — Open questions / TBD

PRD questions are kept under their PRD numbers. New questions from this analysis start at OQ-11.

| # | Question | Blocking? | Affects |
| :-- | :------- | :-------- | :------ |
| OQ-1 | Data source for the 200+ products: LLM-generated, public dataset, or private crawl | Yes, before week 3 | FR-CAT-016, NFR-LEG-002 |
| OQ-2 | Product model and ID strategy | Yes, before week 3 | FR-CAT-001..004 |
| OQ-3 | Azure free offers and terms | Yes, before infra design | NFR-COST, NFR-PORT-002 |
| OQ-4 | LLM and embedding model choice | Before week 10 | FR-NLS, NFR-PERF-003 |
| OQ-5 | RESOLVED by CR-3: Azure Service Bus (emulator locally, RabbitMQ optional). Open cost caveat: the tier that supports topics is billed per hour while the namespace exists; see ADR-0005 | n/a | EIR-COM-003, NFR-COST-001 |
| OQ-6 | Payment: mock only, or VNPay sandbox | No (default mock) | FR-PAY-016 |
| OQ-7 | Which `hivespace.web` pages are reused | Before week 3 | EIR-UI |
| OQ-8 | Validate the Shopee/TikTok search gap with 10-20 queries | No | FR-NLS |
| OQ-9 | RESOLVED by CR-2: separate services, a database each; record as an ADR | n/a | CON-4 |
| OQ-10 | RESOLVED: provider is Resend (Mailpit locally); Notification is its own deployable (CR-3) | n/a | FR-NOTIF, EIR-SW-003 |
| OQ-11 | RESOLVED by CR-1: product images use upload (FR-MEDIA-003) | n/a | FR-CAT-011 |
| OQ-12 | Return window value (days) and where configured | No | FR-CAT-015, FR-RTN |
| OQ-13 | Timeout values: verification link (24 h proposed), reservation hold and payment window (15 min proposed), checkout step timeout (10 s proposed) | Before week 5 | FR-AUTH-005, FR-INV-007, FR-PAY-010, FR-CHK-007 |
| OQ-14 | Auto-completion period after delivery (3 days proposed; shortened for demo) | No | FR-ORD-009 |
| OQ-15 | Definition of a search "hit" (for example expected product in the top 10) | Before week 10 | FR-NLS-011 |
| OQ-16 | Confirm checkout is all-or-nothing across sellers rather than partial success | Before week 5 | FR-CHK-005, FR-CHK-006 |
| OQ-17 | How mock delivery is triggered: timer, operator command, or seller action | No | FR-SHIP-003 |
| OQ-18 | Account deletion, personal-data export and PDPD obligations for a demo with real user emails | No | NFR-LEG-001 |
| OQ-19 | Is a buyer address book needed, or only an address entered at checkout | Before week 5 | FR-ADDR |
| OQ-20 | Should a placed order that the seller never confirms auto-cancel after some time | No | FR-ORD |
| OQ-21 | Confirm proposed numeric targets (lockout 5/15 min, p95 latencies, rate limits) | No | FR-AUTH-011, NFR-PERF, NFR-SEC-005 |
| OQ-22 | Mock shipping fee rule (flat per order proposed) | No | FR-SHIP-001 |
| OQ-23 | Should cancelling a paid online order trigger a (mock) refund | No | FR-PAY-011, FR-PAY-012 |
| OQ-24 | Is email verification required before becoming a seller | No | FR-SHOP-001 |
| OQ-25 | RESOLVED by CR-1: Vietnamese and English (FR-LOC) | n/a | EIR-UI-004, NFR-L10N |
| OQ-26 | Buyer order-placed email: one per order (proposed) or one per checkout | No | FR-NOTIF-003 |
| OQ-27 | What the PRD means by fields "shown on the shop page" (owner view versus public) | No | FR-SHOP-009, FR-SHOP-010 |
| OQ-28 | Natural-language search for English queries (excluded for now; NL search is Vietnamese-first) | No | FR-NLS |
| OQ-29 | Resend: sending domain to verify, free-tier daily and monthly limits, whether demo recipients are limited without a domain | Before week 3 | FR-NOTIF, NFR-COST-004 |
| OQ-30 | Image storage choice (cloud blob storage versus alternatives, local volume or emulator) and size limits (2 MB avatar, 5 MB others proposed) | Before week 3 | FR-MEDIA, EIR-SW-007 |
| OQ-31 | Shop images: logo plus cover assumed; confirm | No | FR-MEDIA-002 |
| OQ-32 | Google Cloud project, consent screen and test users for the demo | Before auth work | FR-AUTH-020..023, CON-8 |
| OQ-33 | Re-authentication rule before a Google-only user sets a password (recent Google sign-in proposed) | No | FR-AUTH-027 |
| OQ-34 | Money display formats per locale and the first future currency | No | FR-CUR-002 |
| OQ-35 | Confirm proposed one-time code numbers: 6 digits, 5-minute lifetime, 60-second resend wait, 5 wrong codes per challenge | No | FR-AUTH-028, FR-AUTH-032, FR-AUTH-033 |

**Source conflicts**
- C-1: The brainstorm says Notification lives inside Order; PRD P0-14 makes it separately owned. This SRS follows the PRD.
- C-2: PRD story 11 is worded "As a buyer, I want to convert my account…" under the Seller heading; treated as a seller-onboarding story.
- C-3: PRD numbering lists P0-14 before P0-13; no content impact.
