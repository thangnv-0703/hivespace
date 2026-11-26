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
