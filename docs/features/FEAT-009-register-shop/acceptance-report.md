# Acceptance Report: FEAT-009 — Register a shop and become a seller

> Verdict: **Accepted** · Date: 2026-11-24
> Standard: technical-design.md §6 @ 7288959 · Sources: srs.md (FR-SHOP-001..004, FR-ROLE-002/003, FR-SHOP-008 registration fields), use-cases.md UC-003
> Repo state audited: 9a7a5f6 (`feat/009-register-shop`, feature code at 2f573a7 plus two acceptance test corrections)

## Verdict summary
All 17 criteria are covered by tests that assert what the criterion says, and every suite was run fresh in this audit and passed. Two tests were weaker than their criteria (AC-9 never checked the correlation id; AC-4 had no web test for the one-time session refresh). Both were corrected and committed, and the corrected tests pass. No rework or design-defect findings. The RTM Test ref is appended for the six FRs this feature implements.

## Standard check (Phase 2)
Every FR in the trace has at least one criterion: FR-SHOP-001 (AC-1, AC-4), -002 (AC-5), -003 (AC-6), -004 (AC-2), FR-ROLE-002 (AC-1, AC-11), FR-ROLE-003 (AC-10). UC-003 alternates 1a and 2a are AC-4, AC-5 and AC-6. The criteria say what their FR statements say. FR-SHOP-008 belongs to FEAT-010 in the RTM and the plan; this feature covers only its registration fields (AC-3), which is the scope audited. NFR-SEC-009 (audit of role grants) is AC-9. No stale or tombstoned references.

## Audit table
| AC | Encodes | Test(s) | Audit | Observed |
| :- | :------ | :------ | :---- | :------- |
| AC-1 | FR-SHOP-001, FR-ROLE-002 | `ShopApiTests.Ac1_…201_with_a_location`; `ShopFlowTests.Ac1_ac2_ac9_…` (both roles, owner); contract "A verified buyer registers…" | faithful | green |
| AC-2 | FR-SHOP-004 | `ShopFlowTests.Ac1_ac2_ac9_…` (business), contract outline both types, `ShopDomainTests` | faithful | green |
| AC-3 | FR-SHOP-008 (registration), UC-003 2 | `ShopFlowTests.Ac3_*` (all together, each rule, 61 chars, nothing saved), `ShopApiTests.Ac3_*` (unknown key, vi/en), contract name rules 2/3/60/61, control char, missing type, trimming | faithful | green |
| AC-4 | FR-SHOP-001, UC-003 1a, D-3 | `ShopFlowTests.Ac4_*` (both), `ShopApiTests.An_unverified…`, contract unverified, `register-shop.spec.ts` unverified + resend | web refresh on load was untested → corrected | green |
| AC-5 | FR-SHOP-002 | `ShopFlowTests.Ac5_*` (sequential and parallel), contract "One shop per user" | faithful | green |
| AC-6 | FR-SHOP-003 | `ShopFlowTests.Ac6_*` (case and spacing, parallel two users), contract incl. accent case | faithful | green |
| AC-7 | FR-ROLE-002, D-1 | `ShopFlowTests.Ac7_…` (conflict at commit via name-blind handler; `AssertNothingSavedAsync` checks shop, grant, audit, outbox) | faithful | green |
| AC-8 | ADR-0002, NFR-REL-005 | `ShopFlowTests.Ac8_…` (one row, schema-validated, correlation id = shop id); Notification `test_verification_email.py` (envelope twice: no error, no email, one inbox row) | faithful | green |
| AC-9 | NFR-SEC-009 | `ShopFlowTests.Ac1_ac2_ac9_…`, `ShopDomainTests` | correlation id not asserted → corrected | green |
| AC-10 | FR-ROLE-003/004, NFR-SEC-006, FR-AUTH-015 | `ShopApiTests.Ac10_*` (Buyer 403, Seller 200, stale token 403, spoofed `X-User-Id`, none/tampered/wrong-audience 401), contract equivalents, gateway tests (401 without session, 403 without CSRF) | faithful | green |
| AC-11 | FR-ROLE-002, UC-003 4 | `ShopRegistrationFlowTests` (system test: register, `/bff/session/refresh`, `/v1/shops/me` 200), `ShopApiTests.Ac10_ac11_…` | faithful | green |
| AC-12 | UC-003 4, ADR-0027 | `guards.spec.ts` (visitor, buyer, seller-on-register, seller), `dashboard.spec.ts`, `account.spec.ts` (sign out), e2e journey (no password, checklist "0 of 2", register page again lands on dashboard) | faithful | green |
| AC-13 | UC-003, D-10 | e2e journey follows the storefront header link; skeleton spec checks the link | faithful | green (mobile-360 skip: link is hidden by design, D-10) |
| AC-14 | FR-LOC-002 | `account.spec.ts` (save + session refresh when signed in, visitor local only, failure keeps language); e2e `profile-and-language` for the cross-app effect | faithful | green |
| AC-15 | FR-LOC-004 | `ShopApiTests` vi and en messages; contract language scenario; error-codes parity check in web and contract suites | faithful | green |
| AC-16 | dev-standards §9.1, ADR-0025 | `register-shop.feature`, every operation tagged | faithful | green (115/115 scenarios) |
| AC-17 | UC-003 main | `tests/e2e/features/register-shop.feature` | faithful | green on desktop and mobile-360 |

## Corrected tests
- **AC-9** (`66f0b7a`, `ShopFlowTests.Ac1_ac2_ac9_…`): asserted only `Method` and `Reason` null. The criterion also requires a correlation id, and no `Activity` existed under test, so the field was never exercised. The test now runs inside an `Activity`, asserts the audit row's `CorrelationId` equals its trace id, and asserts exactly one `seller_role_granted` row. Mutation check: removing `SetCorrelationId` in `Adapters.cs` turned it red; restored (verified clean).
- **AC-4** (`9a7a5f6`, `register-shop.spec.ts`): no test asserted D-3's one-time session refresh when the claim says unverified, nor that the form then enables. Added a test with a reactive verified flag. It first failed because of my own non-reactive stub, then passed once corrected.

## Anti-fake-green review
Diff of test files from the design commit to head: no new skips, todos or deleted cases. Only test change to existing files is `tests/e2e/specs/skeleton.spec.ts`: the seller-centre check moved from "sidebar visible" to "visitor is sent to sign in", a declared consequence of AC-12 (every seller page now needs a session); the sidebar and deep-link behaviour is covered by the new journey. No coverage thresholds exist in the feature's range.

## Independent execution
All run fresh from the repo, with Docker up, no processes of another session reused (nothing was listening on :5080 before the E2E run).
- `dotnet build HiveSpace.slnx`: 0 warnings, 0 errors.
- `dotnet test --solution HiveSpace.slnx`: 484 passed, 0 failed, 0 skipped (includes Identity unit/integration, migration up/down/up, architecture tests, gateway, system test). Re-ran `ShopFlowTests` after the AC-9 correction: 16/16.
- `uv run pytest && mypy && lint-imports` (Notification): 97 passed, mypy clean, 2 contracts kept.
- `pnpm turbo run lint typecheck test build` (web): 13/13 tasks; seller-centre 25 tests pass (after the AC-4 test).
- `tests/contract` (`pnpm test`): 115 scenarios, 547 steps passed.
- `tests/e2e` (`pnpm test`, started the AppHost cold): 25 passed, 1 skipped (pre-declared viewport skip), no retries needed, no flakiness seen.
- `go test -short`: passed (unaffected area).
- `dotnet format --verify-no-changes`: printed no violations.

## Direct verification
- Side effects (AC-7/8/9): inspected in the database by the integration tests: one `ShopRegistered` outbox row per registration, schema-valid; one `seller_role_granted` audit row with null method, no email, correlation id set; nothing written on any rejected request.
- Contracts: request, response, status and error codes match technical-design §3.1 and §3.5 in the contract suite; 401 versus 403 versus 409 ordering observed.
- Screens: the e2e journey renders SCR-SEL-001 and SCR-SEL-002 in desktop and 360px; unverified, name-taken and validation states are covered by component tests. No visual pixel review beyond that.
- Binding NFRs: none measurable beyond the above. **Pending environment:** none for this feature.

## Findings
- **Rework:** none.
- **Design defect:** none. The open items in the design's §8 (Service Bus subscriptions for later consumers, the 3–60 name rule awaiting user confirmation, plan row correction for `ShopRegistered` consumers) are already recorded there and are not defects of this feature.
- **Minor (non-blocking):** (1) The two weak tests above (corrected). (2) Several tests carry AC references only in names, not on every assertion; traceability is by behavior. (3) The storefront-link e2e spec is skipped at 360px because the header link is hidden below `lg` (accepted gap in D-10). (4) During this audit another process changed unrelated working-tree files (an Identity application refactor and `docs/repo-structure.md`) after all suites had finished; those are not part of the audited commit and were left untouched.

## RTM
Test ref appended (`features/FEAT-009-register-shop/acceptance-report.md`) for FR-SHOP-001, FR-SHOP-002, FR-SHOP-003, FR-SHOP-004, FR-ROLE-002, FR-ROLE-003. FR-SHOP-008 not written: its plan ref is FEAT-010.
