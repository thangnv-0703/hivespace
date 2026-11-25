# Acceptance Report: FEAT-010 — Shop settings: profile, pickup address, payout

> Verdict: Accepted · Date: 2026-11-25
> Standard: technical-design.md §6 (AC-1..AC-15) · Sources: srs.md, use-cases.md (UC-004)
> Repo state audited: ad5d6f5 plus one corrected test (see below)

## Verdict summary
All 15 criteria are covered by tests that assert what the criterion says. One test (AC-8, spoofed header) was too loose and was corrected toward the criterion; it passes. Every suite was re-run from the repo state and is green. No rework or design-defect findings. Pending environment: none that block.

## Audit table
| AC | Encodes | Test(s) | Audit | Observed |
| :- | :------ | :------ | :---- | :------- |
| AC-1 | FR-SHOP-005 | ShopSettingsFlowTests.Ac1, contract feature | faithful | green |
| AC-2 | FR-SHOP-005, FR-SHOP-003 | Ac2, A_name_conflict_found_only_at_commit | faithful | green |
| AC-3 | FR-SHOP-006 | Ac3, ShopSettingsDomainTests | faithful | green |
| AC-4 | FR-SHOP-007, FR-SHOP-010, NFR-SEC-008 | Ac4_ac5, system test | faithful | green |
| AC-5 | NFR-SEC-008 | BankAccountProtectorTests (ciphertext, wrong-shop, v1 after v2) + Ac4_ac5 | faithful | green |
| AC-6 | FR-SHOP-008, UC-004 3a | Ac6, contract scenarios | faithful | green |
| AC-7 | FR-SHOP-010 | Ac7 | faithful | green |
| AC-8 | FR-ROLE-003/004, NFR-SEC-006 | ShopApiTests Ac8 (buyer 403, spoof), system test (401, CSRF 403) | spoof test was weak, corrected | green |
| AC-9 | ADR-0002/0003, FR-SHOP-009 | Ac9, ShopChanged schema checks | faithful | green |
| AC-10 | D-9 | Ac10 | faithful | green |
| AC-11 | NFR-SEC-008 | Ac11 (logs, response), Spectral rule and fixture | faithful | green; Spectral passes the contract and fails the fixture |
| AC-12 | FR-LOC-004 | contract scenario "Errors come back in the request language" | faithful | green |
| AC-13 | dev-standards 9.1 | shop-settings.feature | faithful | 145 scenarios green |
| AC-14 | UC-004, SCR-SEL-007/008/002 | web settings.spec.ts, dashboard.spec.ts | faithful | green |
| AC-15 | UC-004 | e2e shop-settings.feature (desktop and 360 px) | faithful | green |

## Corrected tests
AC-8, ShopApiTests.Ac8_a_spoofed_user_id_header...: the final assertion was `Description == "mine" || PickupAddress is not null || Payout is not null`, which passes if any section changed, whichever endpoint ran. It now asserts that the section the endpoint writes changed on the caller's shop and the other sections did not. The other shop's untouched state was already asserted.

## Independent execution
- `dotnet build` and `dotnet test --solution HiveSpace.slnx`: 547 of 554 passed on the first run. The 7 failures were all in tests/system and came from my environment: `Parameters__postgres-password` was unset (scaffold-notes 7). With it set, `tests/system`: 7 of 7 passed. Identity integration (incl. migrations against a Postgres container) and unit suites passed.
- Web: `pnpm turbo run lint typecheck test build`: 13 of 13 tasks successful.
- Go and Python suites, mypy, import-linter: green.
- tests/contract: 145 scenarios, 742 steps passed.
- tests/e2e: 27 passed, 1 skipped (pre-existing viewport skip, not in this feature's diff). Earlier runs failed because I started the AppHost by hand without `RateLimits__PermitLimit=1000`, so the gateway's real limit of 10 per minute applied. That was my setup error, not a defect; with the setting the full suite was green.
- Corrected AC-8 test: ShopApiTests 17 of 17 passed.

## Direct verification
- No skips, deleted cases or loosened assertions in the feature's test diff beyond the AC-8 case above.
- Contract shapes match technical-design section 3 (contract tests and generated client agree).
- NFR-SEC-008 verified by the ciphertext, wrong-shop and key-rotation tests and the log-capture test. Pending environment: deployed bank-encryption keys (plan P-13).

## Findings
- Rework: none
- Design defect: none
- Minor: tests cite criteria loosely in places (ShopApiTests Ac8 also tests the 400 on a body shopId). Non-blocking. An unmeasured part of FR-SHOP-010 (logo and cover) is deferred by design D-8.

## RTM
Test ref appended for FR-SHOP-005, FR-SHOP-006, FR-SHOP-007, FR-SHOP-008, FR-SHOP-010 (partial: logo and cover deferred) and NFR-SEC-008.
