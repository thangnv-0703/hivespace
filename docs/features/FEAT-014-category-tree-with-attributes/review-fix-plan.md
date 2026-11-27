# FEAT-014 review fixes: feature side (main repo)

> Status: Plan, not applied · Date: 2026-11-27
> Fixes the gaps found in the review of `d5d1958` (open lists and inheritance, SRS 0.20). Decisions made with the owner on 2026-11-27.
> Crawler side: `references/hivespace.crawler/PLAN-feat014-crawler-gaps.md` (G1–G8, and its §7 review). **That work is blocked until steps 1–3 here land** (crawler review item 1).
> Scope: documents only. No code changes, and T9 (`53f4e18`) stands. T10–T14 stay pending.

## Decisions to record (2026-11-27)
| # | Decision | Replaces |
| :-- | :-- | :-- |
| D1 | **Brand (603)** offers the 1,000 brands found on the most crawled products, sorted A–Z: today the 24 used crawled brands plus the top 976 bundle brands (about 96% of products). | Q26 ("1,000 most used in Tiki's products") |
| D2 | **Brand ids:** a brand that is among Tiki's crawled options keeps its Tiki id. Every other brand gets an owner id from 100,000,000 up, fixed for good. There is no full fetch of Tiki's brand list. | — |
| D3 | **The other 7 open lists** keep Tiki's crawled 1,000 options, sorted A–Z. Lists that are not open keep Tiki's order. | — |
| D4 | **The 1,000 limit** applies to preparing the first file. Later loads may add options; the loader does not enforce a cap. | the "at most 1,000" in BR-014-14 |
| D5 | **A–Z** means by vi name with accents removed (Đ sorts with D). | — |
| D6 | **Several values on an open list:** a seller may mix picked and typed values; together they count toward the attribute's maximum; entries equal ignoring case count once. | — |
| D7 | **Proper names keep the same text in vi and en:** brand, author, compatible device, publisher, distributor and supplier. Only colour/pattern, size and the lists that aren't open are translated. | — (applies BR-014-4 / AC-014-4.5) |

## 1. FEAT-014 feature spec (`/clarify-feature FEAT-014`)
File: `docs/features/FEAT-014-category-tree-with-attributes/feature-spec.md`. The spec goes back to `Status: Draft` and needs a new sign-off.
- **BR-014-14**, replaced by:
  > For a list that accepts other values, the first file offers up to 1,000 options, sorted A–Z by vi name (accents ignored). For brand, they are the brands found on the most crawled Tiki products; a brand among Tiki's crawled options keeps its Tiki id, and any other brand gets an owner id from 100,000,000 up. For the other lists, they are Tiki's crawled options. Later loads may add options. Only the file adds options; text a seller types never becomes an option.
  - Source column: FR-CAT-022 / Q26, Q27, Q29, Q38, Q40.
- **BR-014-4:** add to the source column "Q41": proper-name lists keep `en = vi` (D7). The rule's text already allows it.
- **Q&A log:**
  - Q38: D1 and D4 (refines Q26).
  - Q39: D6.
  - Q40: D2 (owner ids; no full fetch).
  - Q41: D7.
- **§7 "Input format":** mark it replaced by the v2 layout (technical-design §3.4, D-13).
- **§7:** add "FEAT-039 maps a bundle's brand text to an option through the crawler's `owner-ids.json` (D2)".
- **§6:** add the SRS 0.21 row.
- **Header:** `Status: Draft` while editing. After the owner's explicit "Agree": `Status: Agreed`, plus `Signed off: 2026-11-27` on its own line, with the date note "(second amendment and review fixes Q38–Q41)".

## 2. SRS 0.21 (part of step 1, with the owner's yes)
File: `docs/srs.md`.
- Version 0.20 → 0.21, with a revision row: "Modified FR-CAT-022 notes: picked and typed values together count toward a multi-value list's maximum; the first file offers up to 1,000 options per such list."
- **FR-CAT-022 notes:** append "On a list that allows several values, picked and typed values together count toward its maximum (FR-CAT-020). The first category file offers up to 1,000 options per such list; later loads may add more."
- The FR statement, the use cases and the RTM are unchanged (no new FR).

## 3. FEAT-012 feature spec (stays Draft)
File: `docs/features/FEAT-012-create-a-product-with-variants-and-stock/feature-spec.md`.
- **BR-012-7:** append "On a list that accepts other values and allows several, the seller may combine list values and typed text; together they count toward the maximum, and entries equal ignoring case count once."
- **New AC-012-1.15:** "Given a list attribute that accepts other values and allows at most 5, when the seller submits 3 list values and 2 typed values, then the product is created; when they submit 3 list values and 3 typed values, then the product is not created and the attribute is named with its maximum of 5; a typed value equal to a picked one, ignoring case, counts once."
- **Order:** list US-012-1's criteria in number order, AC-012-1.1 to 1.15.
- **Q13:** "FEAT-014 Q39: several values on an open list → BR-012-7, AC-012-1.15".
- The status stays Draft; FEAT-012 still needs its own sign-off.

## 4. FEAT-014 technical design (detailed-design amendment)
File: `docs/features/FEAT-014-category-tree-with-attributes/technical-design.md`. Notes only: no change to schema, contract, problem codes or tasks, because `MaxValues` and `AllowsCustomValue` already cover this.
- **§2 amendment survey:**
  - 2 of the 8 open lists (498 author, 653 compatible device) are multi-select up to 5.
  - The crawled 1,000 are in Tiki's order, not A–Z.
  - Only 24 crawled brands are used by the bundles.
- **§3.4 Mapping:**
  - An option id ≥ 100,000,000 is an owner id (D-9, D2). The loader treats it like any key.
  - The loader does not limit how many options a list has (D4).
- **§8 FEAT-012 obligations:** typed and picked values count together toward `MaxValues`; duplicates ignoring case are merged before counting (D6, AC-012-1.15).
- **§8 value list size:** brand's options are ranked by use (D1); the others are Tiki's crawled 1,000 (D3). Later loads can grow a list past 1,000, so TAC-014-6 is re-measured after any big addition.
- **§8, new FEAT-039 obligation:** map a bundle's brand text to the option id through `owner-ids.json` (crawler) or by cleaned name, the same way the export does.
- **§8 owner's file:** before the first real load, reset the local Catalog database (or load from empty). The sample's categories 919, 920, 2349, 2350, 2351, 8062 and 8063 are not in the crawl, so loading the real file over the sample is refused as `category.removed`.

## 5. FEAT-014 tasks
File: `docs/features/FEAT-014-category-tree-with-attributes/tasks.md`.
- Amendment prerequisite: remove "commit the pending port-layout split first" (done in `78957fe`, `9644418`).
- T12 done-when: add "the converted 17-category sample still loads; the real crawler export is loaded only after a local database reset (§8)".
- T14: add to the PR list "TAC-014-6 re-measured on the owner's real v2 file".

## 6. Amendment plan status
File: `docs/features/FEAT-014-category-tree-with-attributes/amendment-plan.md`.
- Header: `Status: Resolved 2026-11-27 (feature-spec Q25–Q41; technical-design D-11..D-13; review fixes in review-fix-plan.md)`.
- Under each of Q-A1..Q-A7, one answer line pointing at its Q number.

## 7. Not in this plan (owners named)
- **Crawler export:** `PLAN-feat014-crawler-gaps.md` G1–G8 and its §7 items (one duplicate-id rule: lowest id; `owner-ids.json` keyed by id; `en = vi` for proper-name lists; the sample guard).
- **Implementation-plan rows** for FR-CAT-021, 022 and 023: implementation-planning.
- **ADR-0009 and ADR-0018 amendments:** architecture owner.

## Order and sign-off
1. Steps 1 and 2 through `/clarify-feature FEAT-014`: edit the spec, show the SRS change, ask "Agree".
2. Step 3, in the same run (FEAT-012 stays Draft).
3. Steps 4 and 5 through `/detailed-design FEAT-014`, as notes only.
4. Step 6.
5. Then tell the crawler session that the spec is Agreed, so G4 and G5 are unblocked.

## Verification
- `git diff --stat` touches only: the two feature specs, `srs.md`, `technical-design.md`, `tasks.md`, `amendment-plan.md`, and this file.
- Grep checks:
  - BR-014-14 reads as in step 1;
  - FR-CAT-022's notes and SRS 0.21 match step 2;
  - Q38–Q41 and FEAT-012 Q13 resolve;
  - FEAT-012's criteria read AC-012-1.1 to 1.15 in order;
  - no "most used in Tiki's products" text is left.
- The FEAT-014 spec header shows `Agreed` only after the owner's explicit sign-off.
- No code changed, so no build or test run is needed.
- **Commit:** one commit on `feat/014-category-tree-with-attributes`, "FEAT-014: review fixes for open lists (SRS 0.21)". Not pushed.
