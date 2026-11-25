# Feature Spec: FEAT-014 — Category tree with attributes

> Feature from: docs/implementation-plan.md · Epic: E3 Catalog
> Implements: FR-CAT-003, FR-CAT-019, FR-CAT-020 (definition; FEAT-012 applies it to products), FR-LOC-007 (categories, attribute names and values), FR-CAT-021 (top-level category images: the data; FEAT-018 shows them on the home page) · Uses: FR-CAT-004 (definition of required attributes), FR-LOC-008 · Realizes: UC-005 (step 1, what the seller picks from), UC-020 (loading categories), UC-007 (the home page category bar's images) · Screens: none of its own (operator command); the tree is shown in SCR-SEL-004 by FEAT-012 and in SCR-STF-001/002 by FEAT-018
> Status: Agreed
> Signed off: 2026-11-26
> Date: 2026-11-26 (reopened to change BR-014-12: category images are used)

## 1. Summary
The operator loads HiveSpace's category tree, with each category's attributes, from a file they prepare. The file is adapted from Tiki's seller category taxonomy and carries Vietnamese and English names, and an image for top-level categories. Every product must sit in exactly one leaf of this tree (FEAT-012), browse and search walk it (FEAT-018), and AI search matches its attribute values (FEAT-019). Later loads can add, rename and loosen, but never break products that already use the tree.

## 2. User stories

### US-014-1: Load the category tree
As the operator, I want to load the category tree and its attributes from one file with one command, so that sellers have categories to list products in.
Traces: FR-CAT-003, FR-CAT-019, FR-LOC-007, UC-020 main 1–2

Acceptance criteria:
- **AC-014-1.1** Given an empty catalog and a valid file, when the operator loads it, then every category, attribute and list value in the file exists with its vi and en names, its parent and its position.
- **AC-014-1.2** Given the load finished, then the operator is told how many categories, leaves, attributes and list values were added, renamed and loosened, and how many were unchanged.
- **AC-014-1.3** Given a file where a category, attribute or list value lacks its vi or its en name, when the operator loads it, then nothing changes and every missing name is listed with what it belongs to.
- **AC-014-1.4** Given a file with any problem in BR-014-6 or BR-014-7, when the operator loads it, then nothing changes and every problem is listed, not only the first.
- **AC-014-1.5** Given the same file is loaded twice, then the second load changes nothing and reports everything as unchanged.

### US-014-2: Change the tree without breaking products
As the operator, I want to reload an updated file to add categories, fix names and relax attributes, so that the tree can grow without breaking products already listed.
Traces: FR-CAT-019, UC-020 alt 1a

Acceptance criteria:
- **AC-014-2.1** Given a loaded tree, when the file adds a new category, attribute or list value, then it is added.
- **AC-014-2.2** Given a loaded tree, when the file changes the vi or en name of a category, attribute or list value under the same key, then the name changes and nothing else does.
- **AC-014-2.3** Given a loaded tree, when the file loosens an attribute (required → optional, single → multiple values, a higher maximum), then the change is applied.
- **AC-014-2.4** Given a loaded tree, when the file leaves out a category, moves a category under another parent, leaves out an attribute or a list value, or tightens an attribute (optional → required, multiple → single, a lower maximum, list ↔ free text), then the whole load is refused, nothing changes, and each problem is named.
- **AC-014-2.5** Given a leaf with products, when the file gives it a child category, then the whole load is refused and that category is named.
- **AC-014-2.6** Given a leaf with no products, when the file gives it a child category, then the change is applied and it is no longer a leaf.
- **AC-014-2.7** Given a category with products, when the file adds a required attribute to it, then the whole load is refused; the same attribute added as optional is accepted.

### US-014-3: See the categories in my language
As a seller (and later any shopper), I want to see the category tree in my language and in a sensible order, so that I can find the right category quickly.
Traces: FR-CAT-003, FR-LOC-007, FR-LOC-008, UC-005 main 1

Acceptance criteria:
- **AC-014-3.1** Given the tree is loaded, when the category list is requested in vi, then every category shows its Vietnamese name; in en, its English name.
- **AC-014-3.2** Given the category list, then categories appear in the order the file gives them, under their parent, the same order in vi and en.
- **AC-014-3.3** Given the category list, then each category shows whether it is a leaf, and only leaves are offered as a product's category.
- **AC-014-3.4** Given a name that is missing in en at display time, then the Vietnamese name is shown (FR-LOC-008).

### US-014-4: Know a category's attributes
As a seller choosing a leaf category, I want to see which attributes it has and how each one is filled in, so that I can describe my product correctly.
Traces: FR-CAT-004, FR-CAT-020, FR-LOC-007, UC-005 main 2

Acceptance criteria:
- **AC-014-4.1** Given a leaf, when its attributes are requested, then each shows its name in the requested language, whether it is required, and whether it is a list or free text.
- **AC-014-4.2** Given a list attribute, then it shows whether one or several values may be chosen, the maximum when several, and its values in the requested language, in file order.
- **AC-014-4.3** Given an attribute with no list values in the file, then it is free text.
- **AC-014-4.4** Given a leaf with no attributes in the file, then it shows none and a product in it needs no attribute values.
- **AC-014-4.5** Given a list value that is a brand or proper name with the same text in vi and en, then it shows that text in both languages.

### US-014-5: See top-level categories with their images
As a shopper on the home page, I want each top-level category to show a picture, so that I can recognise and pick a category at a glance.
Traces: FR-CAT-021, UC-007

Acceptance criteria:
- **AC-014-5.1** Given a top-level category with an image in the file, when the category list is requested, then it shows that image.
- **AC-014-5.2** Given a top-level category with no image in the file, when the category list is requested, then it shows a placeholder image.
- **AC-014-5.3** Given a loaded tree, when the file changes a top-level category's image, then the new image is shown; when the file leaves the image out, then the placeholder is shown; either change is applied without refusing the load.
- **AC-014-5.4** Given a category below the top level with an image in the file, then no image is shown for it.

## 3. Business rules
| ID | Rule | Source |
| :-- | :-- | :-- |
| BR-014-1 | The category tree is exactly what the operator's file contains. The owner prepares the file: no Tiki-only groups (TikiNow, Dori Dori, Tiki Outlet, Logistic, Operation Service and similar), no "Tiki" in names, only attributes that describe the product (no warehouse, delivery area, warranty, gift or dangerous-goods fields). Loading does not filter. | Q10, Q11, Q15 |
| BR-014-2 | A category without children is a leaf; a product goes in exactly one leaf. | FR-CAT-003 |
| BR-014-3 | Categories, attributes and list values keep the order of the file, the same in vi and en. | Q4 |
| BR-014-4 | Every category, attribute and list value has a vi and an en name. A brand or proper name may use the same text in both. English names may be machine-translated and are corrected later by renaming. | FR-LOC-007, FR-CAT-019 / Q6, Q16 |
| BR-014-5 | Each category, attribute and list value has a stable key in the file; the same key on a later load means the same thing. | FR-CAT-019 |
| BR-014-6 | A load may add categories, attributes and list values, change names, loosen an attribute (required → optional, single → multiple, a higher maximum), and add, change or remove a top-level category's image. | FR-CAT-019, FR-CAT-021 / Q3, Q18, Q22 |
| BR-014-7 | A load is refused as a whole, naming each problem, if it would: remove or move a category; give a category with products a child; remove an attribute or a list value; tighten an attribute (optional → required, multiple → single, a lower maximum, list ↔ free text); add a required attribute to a category with products; leave a vi or en name missing. | FR-CAT-019 / Q3, Q6, Q8, Q18 |
| BR-014-8 | An attribute is either a list or free text: an attribute with list values in the file is a list, one with none is free text. | Q14 |
| BR-014-9 | A list attribute allows either one value or several; when several, it states the maximum (Tiki's data uses 5). | FR-CAT-020 / Q7 |
| BR-014-10 | Attributes belong to a leaf; a product uses its own leaf's attributes and does not inherit attributes from parent categories. | FR-CAT-004 |
| BR-014-11 | Names and values are cleaned of invisible control characters and surrounding spaces; two values in one list that are then equal, ignoring case, are a problem that refuses the load. | Q9 data review (12,951 values had control characters) |
| BR-014-12 | The tree is adapted from Tiki's seller category taxonomy, and top-level categories use Tiki's category images, linked from Tiki as the file gives them. The owner has checked and accepts both for this project. | NFR-LEG-002 / Q13, Q20, Q23 |
| BR-014-13 | Only top-level categories show an image. A top-level category without an image shows a placeholder image; images on deeper categories are not shown. | FR-CAT-021 / Q19, Q21 |

## 4. Out of scope
- **The category picker and attribute form** for sellers — FEAT-012 (SCR-SEL-004).
- **Browsing a category, category filters, the home page layout** — FEAT-018 (FR-SRCH-003/004, SCR-STF-001/002). FEAT-018 places the top-level images (FR-CAT-021) on the home page; this feature provides them and the placeholder rule.
- **The 200-product seed** (FR-CAT-016/017) — FEAT-039.
- **Producing the English names** (the owner's LLM translation) and finalizing the file — the owner, outside the product.
- **A category admin screen** — not in the MVP (FR-CAT-003 notes).
- **Retiring a category** — not in the MVP (Q3).

## 5. Q&A log
| # | Asked by | Question | Answer | Date | Change made |
| :-- | :-- | :-- | :-- | :-- | :-- |
| Q1 | Agent | How big should the tree be, and where does its content come from? | "I will provide data format." Followed up in Q5 and Q9. | 2026-11-25 | — |
| Q2 | Agent | Which leaf categories get attributes? | Most leaves, 2–5 each. Superseded: the owner's file decides (Q10, Q11). | 2026-11-25 | — |
| Q3 | Agent | After products use the tree, what changes may a reload make? | Add and rename only; removing, moving, or giving a category with products a child is refused. | 2026-11-25 | BR-014-6, BR-014-7 |
| Q4 | Agent | In what order are categories shown? | Curated: the file's order, same in vi and en. | 2026-11-25 | BR-014-3 |
| Q5 | Agent | What should the spec say about your data? | "I'll send the file now." | 2026-11-25 | — |
| Q6 | Agent | Must every category, attribute and list value have vi and en before it can load? | Both required; FR-LOC-008 stays as a safety net. | 2026-11-25 | BR-014-4, BR-014-7 |
| Q7 | Agent | Can a list attribute take several values on a product? | Per attribute. | 2026-11-25 | BR-014-9; FR-CAT-020; FEAT-012 impact (§7) |
| Q8 | Agent | When the operator adds an attribute to a category with products, what happens to them? | It must be optional; otherwise the load is refused. | 2026-11-25 | BR-014-7 |
| Q9 | User | Use `references/hivespace.crawler/crawler-state/tiki-v2` for design and clarification; vi only for now, an LLM will add en. | Reviewed: 6,101 categories, 4,897 leaves, depth up to 8, 38 top groups (some Tiki-only); 312 attribute names, about 11 per leaf; single-choice (Dropdown) and multi-choice up to 5 (Checkbox); value lists cut at 20; 12,951 values with control characters. | 2026-11-25 | Q10–Q16; BR-014-11 |
| Q10 | Agent | Which part of the Tiki tree becomes HiveSpace's tree? | "These files are not final for seed data, but the structure needs to be based on this; I only update the file to cover en." | 2026-11-25 | The file's shape is the input; its content is the owner's (BR-014-1, §7) |
| Q11 | Agent | Which attributes are kept? | Product facts only; Tiki's logistics, warranty and compliance fields are dropped. | 2026-11-25 | BR-014-1 |
| Q12 | Agent | Lists cut at 20 values and empty lists: how do they behave? | "I'm fixing it now" — the owner completes the lists. | 2026-11-25 | Q14 |
| Q13 | Agent | How is the Tiki source recorded (NFR-LEG-002)? | Record it; the owner has checked and accepts its use. Tiki images not used (superseded by Q20, Q23). | 2026-11-25 | BR-014-12 |
| Q14 | Agent | Is an attribute with an empty value list free text? | Yes. | 2026-11-25 | BR-014-8 |
| Q15 | Agent | Who removes Tiki-only groups, "Tiki" in names and operational attributes? | Done in the owner's file; loading does not filter. | 2026-11-25 | BR-014-1 |
| Q16 | Agent | Are the LLM English names checked before going live? | Accepted as is; fixed later by renaming. | 2026-11-25 | BR-014-4 |
| Q17 | Agent | Add FR-CAT-019 (loading rules) and FR-CAT-020 (multi-value attributes) to the SRS? | Add both. | 2026-11-25 | SRS 0.18 (§6) |
| Q18 | Agent | What may change on an existing attribute? | Only loosen: required → optional, single → multi, a higher maximum, new values. Removing an attribute or value and tightening are refused. | 2026-11-25 | BR-014-6, BR-014-7; FR-CAT-019 extended |
| Q19 | User | Change BR-014-12: the category `imageUrl` is used (the home page needs it for top-level categories). Which categories show an image? | Top-level only. | 2026-11-26 | BR-014-13; US-014-5 |
| Q20 | Agent | The links point to Tiki's servers. Whose images are shown? | Tiki's links as given in the file. | 2026-11-26 | BR-014-12 |
| Q21 | Agent | A top-level category has no image (11 of 38 in the Tiki data). What do shoppers see? | A placeholder image. | 2026-11-26 | BR-014-13; AC-014-5.2 |
| Q22 | Agent | On a later load, can the file change or remove an image? | Change and remove; removal shows the placeholder. | 2026-11-26 | BR-014-6; AC-014-5.3 |
| Q23 | Agent | Do you accept showing Tiki's category images (NFR-LEG-002), recorded like the taxonomy? | Yes, accepted. | 2026-11-26 | BR-014-12 |
| Q24 | Agent | Add FR-CAT-021 for category images to the SRS? | Add it. | 2026-11-26 | SRS 0.19 (§6) |

Open, deferred: none. The final file (with en and completed lists) is pending from the owner; it does not change these rules.

## 6. SRS amendments
| ID | Change | Before → after | SRS version |
| :-- | :-- | :-- | :-- |
| FR-CAT-019 | Added | — → loading the category tree: add, rename and loosen only; whole load refused, each problem named, on remove, move, child under a category with products, removed attribute or value, tightened attribute, new required attribute on a category with products, or a missing vi/en name. | 0.18 |
| FR-CAT-020 | Added | — → a list attribute allowing several values accepts up to its maximum per product. | 0.18 |
| UC-020, UC-005 | Traces | UC-020 adds FR-CAT-019; UC-005 adds FR-CAT-020. | 0.18 |
| RTM | New rows | FR-CAT-019 → FEAT-014; FR-CAT-020 → FEAT-014, FEAT-012. | — |
| FR-CAT-021 | Added | — → where a top-level category has an image, show it with the category; otherwise a placeholder image. | 0.19 |
| UC-007 | Traces | UC-007 adds FR-CAT-021. | 0.19 |
| RTM | New row | FR-CAT-021 → FEAT-014, FEAT-018. | — |

## 7. For detailed design
- **Category images:** the file's `imageUrl` on top-level categories (Q19–Q23); how the image link reaches the home page, how the placeholder is supplied, and what the design changes (it previously ignored `imageUrl`).
- **Input format:** the shape of `categories.json` and `category-attributes/chunk-*.json` in `references/hivespace.crawler/crawler-state/tiki-v2` (category key, parent key, name, path; attribute key, name, required, single/multi with maximum, values with key and name), extended by the owner with en names. Design the load around it and agree the exact en fields with the owner.
- What happens to attributes that the file puts on a non-leaf category (BR-014-10), and to the attributes of a leaf that gains children.
- How the tree is exposed to FEAT-012's picker with ~4,000 leaves (search in the picker is a FEAT-012 UI concern) and how other services learn about tree changes.
- How a load is made all-or-nothing, and how its report is shown to the operator.
- **FEAT-012 impact (business, for clarify-feature):** FEAT-012's agreed BR-012-7 says list attributes take one value; FR-CAT-020 now allows several per attribute. FEAT-012's spec needs a one-line amendment (back to Draft and a new sign-off) before its detailed design.
