# Acceptance Report: FEAT-014 — Category tree with attributes

> Verdict: Accepted · Date: 2026-11-26
> Standard: feature-spec.md (AC-014-*, BR-014-*) + technical-design.md §6 (AC mapping, TAC-014-1..8) @ 7f94b97 · Sources: srs.md v0.19, use-cases.md (UC-005, UC-007, UC-020)
> Repo state audited: 7f94b97 (no code changed by this audit)

## Verdict summary
All 22 business criteria and the 8 technical criteria hold. Every suite was run fresh from the repo state and is green. A mutation check turned a test red as it should. TAC-014-5 and TAC-014-6 were measured on a Tiki-size synthetic source and are well inside their bounds. There are no rework or design-defect findings. Four minor notes and the end-to-end checks the design defers to FEAT-012 and FEAT-018 are listed below, and none of them block.

## Audit table
| AC | Encodes | Test(s) | Audit | Observed |
| :- | :------ | :------ | :---- | :------- |
| 1.1 | FR-CAT-003, FR-LOC-007, UC-020 main | `CategoryLoadTests.Ac_1_1…`; `CategoryTreeSourceTests.Ids_are_the_file_keys…`; contract `Root categories…` | faithful (ids, both names, parent, position, one outbox row) | green |
| 1.2 | UC-020 main 2 | `Ac_1_2_the_report_counts…`; `CategoryTreeDiffTests.Growth_is_applied_and_counted` | faithful | green |
| 1.3 | FR-CAT-019, FR-LOC-007 | `Ac_1_3…` (3 × `name.missing`, tables empty, no outbox); `Missing_names_are_listed_with_kind_and_owner` | faithful | green |
| 1.4 | FR-CAT-019 | `Ac_1_4_and_2_4…`; `Every_refusal_rule_is_listed_together`; `Structure_problems…` | faithful (all problems listed, rows unchanged) | green; mutation check: removing the `category.removed` problem turned `Every_refusal_rule_is_listed_together` red |
| 1.5 | UC-020 | `Ac_1_5…` (UNCHANGED, one outbox row); `The_same_file_again_changes_nothing` | faithful | green |
| 2.1 | FR-CAT-019 | `Ac_2_1…`; `Growth_is_applied_and_counted` | faithful | green |
| 2.2 | FR-CAT-019 | `Ac_2_2…`; `Renames_change_names_only` | faithful | green |
| 2.3 | FR-CAT-019 | `Ac_2_3…`; `Each_loosening_is_applied_on_its_own` | faithful | green |
| 2.4 | FR-CAT-019 | `Ac_1_4_and_2_4…`; `An_omitted_attribute…`; `Changing_between_list_and_free_text…`; `Multiple_to_single_and_a_lower_maximum…` | faithful | green |
| 2.5 | FR-CAT-019 | `A_child_under_a_category_with_products_is_refused…` (fake usage set) | faithful at unit level; end-to-end deferred to FEAT-012 (design §8) | green |
| 2.6 | FR-CAT-019 | `Ac_2_6…`; same unit test; contract `A non-leaf category shows no attributes` | faithful | green |
| 2.7 | FR-CAT-019 | `A_new_required_attribute_on_a_category_with_products…` | faithful at unit level; end-to-end deferred to FEAT-012 | green |
| 3.1 | FR-LOC-007, UC-005 | contract `Root categories…`, `Children come back…` | faithful | green |
| 3.2 | BR-014-3 | contract `Children come back in file order whatever the language`; `Sibling_order_follows_the_file` | faithful | green |
| 3.3 | FR-CAT-003 | contract `A leaf has no children`, `isLeaf` steps | faithful | green |
| 3.4 | FR-LOC-008 | `ListCategoriesHandlerTests.A_blank_english_name…`; `GetCategoryHandlerTests.A_blank_english_name…` | faithful | green |
| 4.1 | FR-CAT-004, FR-CAT-020 | contract `A leaf shows its list attributes…`; `GetCategoryHandlerTests` | faithful | green |
| 4.2 | FR-CAT-020 | contract `A leaf shows its list attributes in file order`; `A_list_attribute_returns_its_selection_maximum…` | faithful | green |
| 4.3 | BR-014-8 | `Attribute_without_values_is_free_text…`; contract `A text attribute has no values` | faithful | green |
| 4.4 | UC-005 | contract `A leaf with no attributes returns an empty list` | faithful | green |
| 4.5 | FR-LOC-007 | `Same_vi_and_en_names_are_fine`; contract `A value with the same Vietnamese and English name…` | faithful | green |
| 5.1 | FR-CAT-021, UC-007 | `Ac_5_1_and_5_3…` (stored); contract `A root with an image…` | faithful; on-screen part is FEAT-018 | green |
| 5.2 | FR-CAT-021 | contract `…a root without one returns null` | faithful; placeholder on screen is FEAT-018 | green |
| 5.3 | FR-CAT-021 | `Ac_5_1_and_5_3…` (v2-images); `Images_can_be_added_changed_and_removed` | faithful | green |
| 5.4 | BR-014-13 | `An_image_dropped_below_the_top_level…`; contract `Children have no image` | faithful | green |
| TAC-1 | all-or-nothing | `Tac_1_a_failure_after_the_rows_are_written_leaves_nothing` | faithful | green |
| TAC-2 | outbox event | `Tac_2_the_outbox_row_matches_the_event_schema` | faithful | green |
| TAC-3 | serialized loads | `Tac_3_two_loads_at_once…` | faithful | green |
| TAC-4 | cleaning | `NameCleanerTests`; `Two_values_equal_after_cleaning…` | faithful | green |
| TAC-5 | load time | measured below | pass | 14.3 s |
| TAC-6 | read latency | measured below | pass | p95 54 ms / 78 ms |
| TAC-7 | contract hygiene | Spectral, client regeneration, contract scenarios (200 and 404) | pass | green |
| TAC-8 | boundaries | `Catalog.ArchitectureTests` | pass | green |

## Corrected tests
None. The test diff from `042ce89` to head has no skips, deleted cases, loosened assertions or newly mocked behavior. The `.Skip(1)` hit is a LINQ call in an assertion, and the `CommentHandling.Skip` hit is a JSON parser option. No coverage configuration changed.

## Independent execution
- `dotnet build HiveSpace.slnx`: 0 errors.
- `dotnet test --solution HiveSpace.slnx`: 620 passed and 7 failed. All 7 failures were in `tests/system`, because `Parameters__postgres-password` was not set and the AppHost waited for it (scaffold-notes §7). That is an environment issue, not a product failure. After rerunning `tests/system` with the variable set, it passed 7 of 7. All other assemblies passed, including `Catalog.UnitTests`, `Catalog.IntegrationTests` (migrations up, down and up again; check constraints) and `Catalog.ArchitectureTests`.
- `tests/contract` (`pnpm test`): 158 of 158 scenarios, 801 of 801 steps.
- `web`: `pnpm turbo run lint typecheck test build` passed 13 of 13 tasks; storefront had 108 tests.
- Spectral (CI command, `contracts/.spectral.yaml`): 0 errors, 18 pre-existing warnings. Regenerating `web/packages/api-client` left no diff.
- Mutation check: removing the `category.removed` problem turned `Every_refusal_rule_is_listed_together` red. The source was restored and the tree is clean.
- Observed no flaky tests. No E2E path was owed (design §8).

## Direct verification
- **TAC-014-5 (load time):** The synthetic source has 6,101 categories, 6,063 leaves, 72,756 links and 1,455,120 list values. That is heavier than the Tiki-size figure of 54,992 links. A first load into a fresh Postgres 17 container on this sandbox took **14.3 s** against a limit of 2 minutes. A reload with no changes took 7.6 s and 7.9 s, and both reported UNCHANGED.
- **TAC-014-6 (read latency):** Catalog.Api was started cold against that database. At 50 concurrent clients and 5,000 requests per set: `GET /v1/categories?parentId=…` had p50 29.9 ms and p95 54.1 ms, and `GET /v1/categories/{leaf}` had p50 45.4 ms and p95 77.9 ms. There were no non-200 responses. This is indicative only: it ran on the sandbox, and each leaf has 12 attributes with 20 values (about 15 KB), not the 1,000-value lists the design warns about. The first 500 warm-up requests after a cold start had p95 of about 2.2 s, which is JIT and connection warm-up.
- **Contracts:** the implemented request and response shapes match the §3 contract scenarios, and the system test follows the new `listCategories` shape.
- **Pending environment:** none beyond the above.

## Findings
- **Rework:** none.
- **Design defect:** none.
- **Minor:**
  1. Tests carry the AC id only in their names (`Ac_1_1…`), and most are traced by behavior. A comment or trait with the AC id would make tracing mechanical.
  2. TAC-014-5 was owed "recorded in the PR" by T5. There is no recorded figure in the repo, so this report holds the measurement.
  3. TAC-014-6 should be re-measured once the owner's final file lands, if its value lists are large.
  4. `tests/system` needs `Parameters__postgres-password` in the environment (known; `scripts/cloud-setup.sh` should export it).
- **Deferred (by design §8, not findings):** AC-014-2.5 and 2.7 end-to-end (FEAT-012); AC-014-5.1 and 5.2 on screen (FEAT-018).

## RTM
Test ref appended for FR-CAT-003, FR-CAT-019, FR-CAT-020 (partial), FR-CAT-021 (partial), FR-LOC-007 (partial). FR-CAT-020, FR-CAT-021 and FR-LOC-007 carry the marker because their Plan ref includes other features.

---

# Re-verification 2026-11-27 — open lists and inheritance (SRS 0.20/0.21, tasks T9–T14)

> Verdict: Accepted · Date: 2026-11-27
> Standard: feature-spec.md (AC-014-1.1–5.4, BR-014-1..14) + technical-design.md §6 (TAC-014-1..10) · Sources: srs.md 0.21 (FR-CAT-019..023), use-cases.md
> Repo state audited: f10c821 (no code changed by this audit; a mutation check was reverted, tree clean)

## Verdict summary
The amended criteria hold. The standard was re-derived from the 0.21 SRS and the amended spec. Every new or changed criterion (AC-014-2.3, 2.4, 2.6–2.9, 4.1, 4.3, 4.4, 4.6–4.8) and TAC-014-9 and 10 has a faithful test, and every suite was run fresh and is green. A mutation check on the "closing an open list" rule turned its test red. There are no rework or design-defect findings. TAC-014-5 and TAC-014-6 are pending re-measurement on the owner's real v2 file, as the design says (see Direct verification). That is flagged, not blocking.

## Audit table (amended and new criteria; the rest are unchanged from the 2026-11-26 section and re-ran green)
| AC | Encodes | Test(s) | Audit | Observed |
| :- | :------ | :------ | :---- | :------- |
| 2.3 | FR-CAT-019, FR-CAT-022 | `CategoryTreeDiffTests.Ac_2_3_each_loosening_is_applied_on_its_own`; `CategoryLoadTests.Ac_2_3_opening_a_list_is_stored` (`v2-open-list`) | faithful (closed → open applied and stored) | green |
| 2.4 | FR-CAT-019 | `Ac_2_4_a_list_that_stops_accepting_other_values_is_tightening`; `…an_omitted_attribute_definition…`; `CategoryLoadTests.Ac_2_4_closing_a_list_is_refused_and_nothing_changes` | faithful (REFUSED, nothing changes) | green; mutation: breaking the open → closed check turned the unit test red |
| 2.5 | FR-CAT-019 | unchanged unit test | faithful at unit level; end-to-end deferred to FEAT-012 | green |
| 2.6 | FR-CAT-019, FR-CAT-023 | `Ac_2_6_a_leaf_without_products_can_gain_a_child`; `Ac_2_6_the_former_leaf_shows_no_attributes_and_its_new_child_inherits_them` | faithful (read path, `isLeaf: false`, `attributes: []`, child inherits) | green |
| 2.7 | FR-CAT-019, FR-CAT-023 | `Ac_2_7_…_on_a_category_with_products_…`; `Ac_2_7_…_on_a_parent_a_used_leaf_inherits_…` | faithful (own and inherited; optional accepted); end-to-end deferred to FEAT-012 | green |
| 2.8 | FR-CAT-019 | `Ac_2_8_own_links_that_leave_out_an_inherited_attribute_are_refused`; `Ac_2_8_links_on_a_nearer_ancestor_…` | faithful (leaf and dropped attribute named); end-to-end deferred to FEAT-012 | green |
| 2.9 | FR-CAT-019 | `Ac_2_9_own_links_with_every_inherited_attribute_…` (superset applied; new required refused); `CategoryLoadTests.Ac_2_9_…` (`inherit` → `inherit-own-superset`) | faithful | green |
| 4.1 | FR-CAT-004, FR-CAT-020, FR-CAT-022 | contract `An open list…`, `A text attribute does not accept other values`; `GetCategoryHandlerTests` | faithful (`allowsCustomValue` present on every attribute) | green |
| 4.3 | BR-014-8 | `Ac_4_3_attribute_without_values_is_free_text_whatever_its_input_type_or_marker`; contract `A text attribute has no values` | faithful | green |
| 4.4 | UC-005 | `EffectiveAttributesTests.Ac_4_4_…`; contract `A leaf with no attributes returns an empty list` | faithful | green |
| 4.6 | FR-CAT-022 | `GetCategoryHandlerTests.Ac_4_6_…`; contract `An open list accepts other values and keeps its values in order` | faithful | green |
| 4.7 | FR-CAT-023 | `EffectiveAttributesTests.Ac_4_7_…`; `GetCategoryHandlerTests.Ac_4_7_…`; contract `A leaf with no attributes of its own shows its parent's attributes` | faithful (inherits the ancestor's required, selection and other-values settings) | green |
| 4.8 | FR-CAT-023 | `EffectiveAttributesTests.Ac_4_8_…`; contract `A leaf with its own attributes shows only those` | faithful | green |
| TAC-9 | migration | `MigrationTests.Tac_9_*` (up, down, up; both checks and the unique index reject bad rows) | faithful | green |
| TAC-10 | file format | `CategoryLoadTests.Tac_10_*` (4 tests); `CategoryTreeSourceTests.Tac_10_*`; sample load below | faithful | green |

AC-014-1.x, 2.1, 2.2, 3.x, 4.2, 4.5 and 5.x still pass on the converted fixtures, and their assertions were only rewritten for the v2 record shapes (see the diff review below).

## Corrected tests
None.

## Independent execution (cold start, repo state f10c821)
- `dotnet build HiveSpace.slnx`: 0 warnings, 0 errors.
- `dotnet test --solution HiveSpace.slnx` (with `Parameters__postgres-password` set): **669 passed, 0 failed, 0 skipped.** This includes `Catalog.UnitTests` (75 tests, run again after the mutation), `Catalog.IntegrationTests` (migrations up/down/up), `Catalog.ArchitectureTests` including the port-layout rule, Gateway tests and the system tests.
- `tests/contract`: **162 of 162 scenarios, 827 of 827 steps.**
- `web`: `pnpm turbo run lint typecheck test build` passed 13 of 13 tasks.
- Spectral on `catalog.yaml`: 0 errors, 1 warning (`operation-description` on `/v1/catalog/ping`, pre-existing).
- Mutation check: changing the open → closed tightening check in `CategoryTreeDiff` (line 69) so it can never fire turned `Ac_2_4_a_list_that_stops_accepting_other_values_is_tightening` red (1 of 75). Source restored with `git checkout`, tree clean.
- No flaky tests observed. No E2E path was owed.

## Direct verification
- **Sample load (TAC-014-10):** against a fresh Postgres 17 container, `load-categories --source data/category-tree` on the converted 17-category sample printed APPLIED (17 categories, 11 leaves, 7 attributes, 19 links, 24 values, 2 images), exit 0. A second run printed UNCHANGED for every row, exit 0. The `SharedAttributeLists` migration applied cleanly on the way. Container removed afterwards.
- **Diff review (anti-fake-green):** the test diff from 3e88864 to head has no new skips or todos, no deleted cases and no newly mocked behavior. Removed assertions are the old per-link `ValueIds`/`Kind` shapes, replaced by the shared-attribute equivalents (`CK_attributes_*`, `Position`, `AllowsCustomValue`). The only csproj change adds the ASP.NET shared framework reference needed by the AC-014-2.6 read-path test. No coverage configuration exists or changed.
- **Written rules:** the port-layout split (one port per file) is enforced by `PortLayoutTests`, green.
- **Pending environment:**
  - **TAC-014-5 (load time)** and **TAC-014-6 (read latency)** were measured on 2026-11-26 on a v1-shaped synthetic source (14.3 s; p95 54 ms and 78 ms). They are not re-measured here: the v2 shape (shared attribute lists of up to 1,000 values, inherited reads) has no Tiki-size source until the owner's real v2 file lands. Both must be re-measured on that file, which the design and T14 already say. This is **not blocking**, but the user should treat it as owed before the real data is loaded. The inherited read adds one query for ancestors' links and a list of up to 1,000 values per open attribute, which is the case TAC-014-6 was written to catch.
  - End-to-end checks deferred to FEAT-012 (AC-014-2.5, 2.7, 2.8 with real products; typed-text entry) and to FEAT-018 (AC-014-5.1, 5.2 on screen), by design §8.

## Findings
- **Rework:** none.
- **Design defect:** none.
- **Minor:**
  1. Test names carry the AC id but no trait or comment (carried over from 2026-11-26, minor 1).
  2. `tests/system` still needs `Parameters__postgres-password` exported by `scripts/cloud-setup.sh` (carried over, minor 4).
  3. No automated test loads the committed `data/category-tree/` sample; TAC-014-10's "sample loads APPLIED then UNCHANGED" was verified by hand here. A one-test addition would make it mechanical.

## RTM
Test ref appended for FR-CAT-022 and FR-CAT-023, both with `(partial)` because their Plan ref also names FEAT-012. FR-CAT-019 already carries this report; its amended scope is covered by this section and no cell changed.
