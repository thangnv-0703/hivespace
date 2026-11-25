# Feature Spec: FEAT-012 — Create a product with variants and stock

> Feature from: docs/implementation-plan.md · Epic: E3 Catalog
> Implements: FR-CAT-001, FR-CAT-002, FR-CAT-004, FR-CAT-005, FR-CAT-020 (applied to products), FR-SHOP-011, FR-INV-001, FR-CUR-003 (product prices) · Uses: FR-CAT-003 (category tree, from FEAT-014), FR-ROLE-003, FR-ROLE-004, FR-LOC-004, FR-LOC-007, FR-CUR-002 · Realizes: UC-005 (main 1, 2, 3, 4; exc 4a, 4b) · Screens: SCR-SEL-004 (create mode)
> Status: Agreed
> Signed off: 2026-11-25
> Date: 2026-11-25

## 1. Summary
A seller whose shop is set up (FEAT-010) can list a product: they pick a category, fill in the title, description and the category's attributes, and describe what they sell as one or more variants, each with its own price in dong and its own stock. The product is live as soon as it is created. This is the first step that puts goods on the marketplace; without it buyers have nothing to find, put in a cart or order.

## 2. User stories

### US-012-1: Create a product
As a seller, I want to list a product in my shop with a category, a title, a description and the category's attributes, so that buyers can find and buy it.
Traces: FR-CAT-001, FR-CAT-003, FR-CAT-004, FR-CAT-020, FR-ROLE-003, FR-ROLE-004, FR-LOC-007, UC-005 main 1, 2, 4

Acceptance criteria:
- **AC-012-1.1** Given a seller whose shop has a pickup address and payout details, when they submit a product with a leaf category, a valid title and description, every required attribute filled and at least one valid variant, then the product is created in their shop with status Active.
- **AC-012-1.2** Given the product was just created, when the confirmation appears, then it shows the product's title, its number of variants and its total stock, with the actions "Create another product" and "Back to dashboard".
- **AC-012-1.3** Given the category picker, when the seller chooses a category, then only leaf categories can be selected, and category and attribute names show in the seller's language (vi or en).
- **AC-012-1.4** Given a category with attributes, when the seller fills the form, then each attribute is a pick-from-list or a free-text field as the category defines; a list attribute takes one value, or several up to its maximum, as the category defines; required attributes must be filled and optional ones may stay blank.
- **AC-012-1.5** Given the seller submits without a required attribute, then the product is not created and that attribute is named as missing.
- **AC-012-1.9** Given a list attribute that allows at most 5 values, when the seller submits 6, then the product is not created and the attribute is named with its maximum of 5.
- **AC-012-1.10** Given a list attribute, when the seller submits a value that is not in its list, then the product is not created and that attribute is named.
- **AC-012-1.6** Given a signed-in user without the Seller role, when they try to create a product, then they are refused as not allowed and nothing is created.
- **AC-012-1.7** Given a seller, when they create a product, then it always belongs to their own shop; they cannot create a product in another seller's shop.
- **AC-012-1.8** Given a shop that already has a product titled "Áo thun cotton", when the seller creates another product with the same title, then it is created (titles need not be unique).

### US-012-2: Describe what I sell as variants with price and stock
As a seller, I want to describe my product's options (such as colour and size) and give each combination its own price and stock, so that buyers can pick exactly what they want and I don't sell what I don't have.
Traces: FR-CAT-002, FR-CUR-003, FR-INV-001, FR-CUR-002, UC-005 main 3

Acceptance criteria:
- **AC-012-2.1** Given a product with no options, when the seller enters one price and one stock quantity, then the product is created with exactly one variant carrying that price and stock.
- **AC-012-2.2** Given the seller adds the options "Màu" with values Đỏ, Xanh and "Size" with values M, L, XL, then six variants are listed — one per combination — each needing its own price and stock.
- **AC-012-2.3** Given a variant list, when the seller adds or removes an option value, then the list always contains every combination of the current values (no combination can be left out).
- **AC-012-2.4** Given a variant priced 150000, when the product is created, then the price is kept as 150,000 VND and shown as "150.000 ₫" in vi and "₫150,000" in en.
- **AC-012-2.5** Given a variant created with stock 25, then its on-hand quantity is 25, its reserved quantity is 0 and its available quantity is 25.
- **AC-012-2.6** Given a variant created with stock 0, then the product is created and that variant counts as sold out.

### US-012-3: Know exactly what to fix
As a seller, I want every problem in my product named at its field, so that I can correct them all in one go.
Traces: FR-CAT-005, FR-LOC-004, UC-005 exc 4a

Acceptance criteria:
- **AC-012-3.1** Given a product with several invalid values at once (for example a 5-character title, a price of 0 and a stock of −1), when the seller submits, then nothing is created and each invalid value is reported at its own field.
- **AC-012-3.2** Given any value outside a limit in BR-012-3, when the seller submits, then the product is not created and the message names the field and its limit.
- **AC-012-3.3** Given a price that is not whole dong (for example 1500.5), when the seller submits, then that price is rejected.
- **AC-012-3.4** Given an option with the values "Đỏ" and "đỏ", when the seller submits, then the product is rejected as having a duplicate variant combination.
- **AC-012-3.5** Given options whose combinations exceed 50 variants (for example 8 colours × 7 sizes = 56), when the seller submits, then the product is rejected and the limit of 50 variants is stated.
- **AC-012-3.6** Given the seller's language is en (or vi), when a product is rejected, then every error message is in that language.

### US-012-4: Be told my shop is not ready to sell
As a seller who has not finished shop setup, I want to be told what is missing when I try to list a product, so that I know what to do first.
Traces: FR-SHOP-011, UC-005 exc 4b

Acceptance criteria:
- **AC-012-4.1** Given a shop without a pickup address, when the seller tries to create a product, then nothing is created and the seller is told the pickup address is missing.
- **AC-012-4.2** Given a shop without payout details, when the seller tries to create a product, then nothing is created and the seller is told payout details are missing.
- **AC-012-4.3** Given a shop missing both, then both are named.
- **AC-012-4.4** Given the "shop not ready" message, then it offers a link to the shop settings where the missing part is filled in.
- **AC-012-4.5** Given a seller who just completed shop setup, when they then create a product, then it is accepted.

## 3. Business rules
| ID | Rule | Source |
| :-- | :-- | :-- |
| BR-012-1 | Creating a product publishes it: a new product is Active at once. There are no drafts. | FR-SHOP-011, UC-005 precondition / Q2 |
| BR-012-2 | A product can be created only when the seller's shop has both a pickup address and payout details; otherwise nothing is saved and every missing part is named. | FR-SHOP-011 / Q2 |
| BR-012-3 | Product limits: title 10–120 characters; description 20–5,000 characters; at most 2 options; option name up to 20 characters; up to 20 values per option, each up to 30 characters; at most 50 variants; price 1,000–100,000,000 ₫ in whole dong; stock 0–999,999 whole units. | FR-CAT-005 (amended, SRS 0.17) / Q5, Q9 |
| BR-012-4 | A product has either no options (exactly one variant) or 1–2 options named by the seller; every combination of the option values is a variant with its own price and stock. | FR-CAT-002 / Q3, Q7 |
| BR-012-5 | Values within one option must differ, ignoring case; two equal values make a duplicate variant combination. | FR-CAT-005 / Q7 |
| BR-012-6 | Every price is in VND, the only enabled currency, and is kept with its currency. | FR-CUR-001, FR-CUR-003 |
| BR-012-7 | Each product has exactly one leaf category from the platform's category tree; required attributes of that category must be filled, optional ones may be blank. A list attribute takes one value, or several up to its maximum, as the category defines, and only values from its list. | FR-CAT-003, FR-CAT-004, FR-CAT-020 / Q8, Q10 |
| BR-012-8 | Product titles need not be unique, within a shop or across shops. | Q6 |
| BR-012-9 | Seller-entered text (title, description, option names and values, free-text attribute values) is shown as written, not translated; category and attribute names show in vi or en. | FR-LOC-007 |
| BR-012-10 | A new variant's available stock equals the stock the seller entered; nothing is reserved yet. | FR-INV-001 |
| BR-012-11 | Only a user with the Seller role can create a product, and only in their own shop. | FR-ROLE-003, FR-ROLE-004 |

## 4. Out of scope
- **The category tree itself** and its attribute definitions — FEAT-014. FEAT-012 depends on it (see Q1 and §7).
- **Product images** — FEAT-036 (FR-CAT-011, FR-MEDIA-003). Nothing about images is created or shown here.
- **Showing the product in browse and search** within 60 s (FR-CAT-018, FR-NLS-007, UC-005 main 5) — FEAT-018 and FEAT-019; the product page — FEAT-015; the public shop page — FEAT-011.
- **Editing a product, changing stock later, deactivating and reactivating, the product list** — FEAT-013 (FR-CAT-006..010, FR-INV-010, FR-INV-011). After FEAT-013 ships, the confirmation may lead to the product list instead.
- **Drafts** — not in the MVP (BR-012-1).
- **AI listing draft** (UC-005 alt 2a, UC-023) — FEAT-016.
- **Notifications** — no one is notified when a product is created.

## 5. Q&A log
| # | Asked by | Question | Answer | Date | Change made |
| :-- | :-- | :-- | :-- | :-- | :-- |
| Q1 | Agent | A product needs a leaf category (FR-CAT-003), but the tree arrives in FEAT-014, after FEAT-012. What can a seller pick from? | Build FEAT-014 first (the category tree before FEAT-012). | 2026-11-25 | Dependency recorded in §4 and §7; a build-order change for implementation-planning. |
| Q2 | Agent | What happens when a seller whose shop has no pickup address or payout tries to create a product? | Block, no drafts: creating is publishing; nothing is saved and the missing parts are named with a link to settings. | 2026-11-25 | BR-012-1, BR-012-2; US-012-4 |
| Q3 | Agent | How can a seller shape variants? | 0–2 seller-named options; with none, one variant; otherwise each combination is a variant with its own price and stock. | 2026-11-25 | BR-012-4; US-012-2 |
| Q4 | Agent | Where does the seller land after creating a product (list and edit are FEAT-013)? | A confirmation with title, variant count and total stock, plus "Create another product" and "Back to dashboard". | 2026-11-25 | AC-012-1.2 |
| Q5 | Agent | Which product limits apply? | Marketplace-standard: title 10–120, description 20–5,000, up to 20 values per option and 50 variants, option names ≤ 20 and values ≤ 30 characters, price 1,000–100,000,000 ₫, stock 0–999,999 (0 allowed, shows sold out). | 2026-11-25 | BR-012-3; AC-012-2.6, US-012-3 |
| Q6 | Agent | Can one shop have two products with the same title? | Allowed. | 2026-11-25 | BR-012-8; AC-012-1.8 |
| Q7 | Agent | Are option values unique, and can a seller leave a combination out? | Full grid only: every combination is a variant. | 2026-11-25 | BR-012-4, BR-012-5 (values must differ, since two equal values would duplicate a combination); AC-012-2.3, AC-012-3.4 |
| Q8 | Agent | How are category attributes entered, and what about optional ones? | As the category defines: pick-from-list or free text; required ones filled, optional ones may be blank; names in vi/en, seller values as written. | 2026-11-25 | BR-012-7, BR-012-9; AC-012-1.4 |
| Q9 | Agent | Should FR-CAT-005 be amended to include the limits? | Yes, amend FR-CAT-005. | 2026-11-25 | SRS 0.17 (§6); UC-005 4a |
| Q10 | Agent | FEAT-014 added FR-CAT-020 (a list attribute may allow several values, up to a maximum). Amend BR-012-7 and AC-012-1.4 to match? | Yes, amend FEAT-012. | 2026-11-25 | BR-012-7, AC-012-1.4; added AC-012-1.9, AC-012-1.10; status back to Draft |

Open, deferred: none.

## 6. SRS amendments
| ID | Change | Before → after | SRS version |
| :-- | :-- | :-- | :-- |
| FR-CAT-005 | Modified | "…negative stock, duplicate variant option combination)" → "…negative stock, duplicate variant option combination, or a value outside the product limits)"; the limits (BR-012-3) added to its notes. | 0.17 |
| UC-005 4a | Modified | "(price ≤ 0, duplicate variant combination, negative stock)" → adds "a value outside the product limits of FR-CAT-005". | 0.17 |

## 7. For detailed design
- **Build order (for implementation-planning, not a design choice):** the category tree with its attribute definitions (FEAT-014's FR-CAT-003 part) must exist before FEAT-012 is built (Q1). FEAT-014's seed (FR-CAT-016) needs products, so only the tree part can move ahead; implementation-planning decides whether to split FEAT-014 or reorder it.
- How Catalog knows whether a shop is ready (FEAT-010's `ShopChanged` / `setupComplete` note) and what happens if that knowledge is briefly behind a just-completed setup (AC-012-4.5).
- How the initial stock reaches Inventory and what the seller sees if that step is slow or fails (FR-INV-001, BR-012-10).
- Protection against creating the same product twice on a double submit or retry.
- How "characters" are counted for the limits (Vietnamese diacritics, surrounding spaces).
