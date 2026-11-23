# Acceptance Report: FEAT-005 — Account hardening: lockout, set a password, rate limits, audit

> Verdict: Accepted · Date: 2026-11-23
> Standard: technical-design.md §6 @ 5696db0 · Sources: srs.md, use-cases.md
> Repo state audited: 2592013 (`feat/005-account-hardening`, with FEAT-004 merged)

## Verdict summary
All 19 acceptance criteria hold. Every suite was re-run from the repo head and was green, apart from one transient Docker failure in the Notification integration tests that passed on re-run (recorded below). The test diff is additive, with no skips and no weakened assertions, and a mutation of the lockout threshold turned the tests red. No rework or design-defect findings. The RTM Test ref is appended for FR-AUTH-011, FR-AUTH-012 and FR-AUTH-027.

## Standard (re-derived)
FR-AUTH-011 (5 failures / 15 min → lock 15 min), FR-AUTH-012 (locked → reject without evaluating the password), FR-AUTH-027 (set a password after Google sign-in), the FR-AUTH-025 note (wrong current password counts), NFR-SEC-005 (rate limits) and NFR-SEC-009 (audit, 90 days), UC-002 2b and UC-026 1a/2a. Each FR has at least one criterion; the criteria say what their FR says. No misencoding.

## Audit table
| AC | Encodes | Test(s) | Audit | Observed |
| :- | :------ | :------ | :---- | :------- |
| AC-1 | FR-AUTH-011, UC-002 2b | `LockoutDomainTests.Ac1_*`, `LockoutFlowTests.Ac1_*`, `LockoutApiTests.Ac1_*` (423, `locked_until` +15 min) | faithful | green; mutation (threshold 6) → red |
| AC-2 | FR-AUTH-012 | `SignInLockoutHandlerTests.Ac2_*` (hasher spy), `LockoutFlowTests.Ac2_*` (fake clock, row cleared) | faithful | green |
| AC-3 | FR-AUTH-011 | `LockoutDomainTests.Ac3_*` (two), `LockoutFlowTests.Ac3_*` | faithful | green |
| AC-4 | FR-AUTH-011, D-3 | `LockoutApiTests.Ac4_*` (10 parallel: no 500, count 5, one `account_locked` row) | faithful | green |
| AC-5 | FR-AUTH-025 note, UC-026 2a | `ChangePasswordLockoutTests.Ac5_*` (three) | faithful | green |
| AC-6 | FR-AUTH-010/026/011 | `LockoutFlowTests.Ac6_*` (two) | faithful | green |
| AC-7 | D-2 | `LockoutApiTests.Ac7_*` (fake IdP, lockout row untouched) | faithful | green |
| AC-8 | NFR-SEC-009 | `LockoutFlowTests.Ac8_*`, `LockoutApiTests.Ac8_*` (Google) | faithful | green |
| AC-9 | NFR-SEC-009 | `AuditRetentionTests.Ac9_*` (90 d deleted, 89 d kept) | faithful | green |
| AC-10 | FR-AUTH-027, FR-NOTIF-014 | `SetPasswordApiTests.Ac10_*` (204, credential, outbox row against its schema, other grant revoked, silent authorize, new password signs in) | faithful | green |
| AC-11 | FR-AUTH-027, FR-AUTH-003 | `SetPasswordApiTests.Ac11_*` (seven, incl. exactly 10 min, parallel 409, nothing written) | faithful | green |
| AC-12 | FR-AUTH-027 | `ReauthOidcTests.Ac12_*` (three), `LockoutApiTests.Ac12_*`, gateway `AuthTests.Ac12_*` | faithful | green |
| AC-13 | NFR-SEC-005 | gateway `RateLimitTests.Ac13_*` (three) | faithful | green |
| AC-14 | NFR-SEC-005, D-6 | gateway `RateLimitTests.Ac14_*` (two) | faithful | green |
| AC-15 | FR-AUTH-027 | `SetPasswordApiTests.Ac15_*` | faithful | green |
| AC-16 | FR-AUTH-012/027, FR-LOC-004 | storefront `auth.spec.ts`, `password.spec.ts`, `profile.spec.ts`; vi/en parity in lint | faithful | green |
| AC-17 | ADR-0025 | `lockout.feature`, `set-password.feature`, `change-password.feature` | faithful | 77/77 scenarios |
| AC-18 | FR-NOTIF-015 | `test_external_login_linked.py` (`test_ac18_*`, vi and en) | faithful | green |
| AC-19 | dev-standards §5, NFR-SEC-007 | `LayerRulesTests`, Spectral, Sheriff/i18n (web lint), log-capture tests `LockoutApiTests.Ac19_*`, `SetPasswordApiTests.Ac19_*` | faithful | green |

## Corrected tests
None.

## Anti-fake-green review
Diff of test files from FEAT-004's merge (`a24a172`) to head: additive. Edits to existing assertions are two, both justified by the new criteria: `ProfileFlowTests` gains the `hasPassword` argument (AC-15), and `test_external_login_linked.py` now expects the one `/profile/password` link only when `passwordRemoved` (AC-18). `MigrationTests` gains the two new tables. No skip, todo or `.skip` was added and no case was deleted. No coverage configuration is enforced for this feature. Mutation check: `LockoutPolicy.Threshold` 5 → 6 failed 2 Identity unit tests; reverted.

## Independent execution
All run from the repo head.

| Check | Result |
| :-- | :-- |
| `dotnet build HiveSpace.slnx` | 0 warnings, 0 errors |
| `dotnet test --solution HiveSpace.slnx` (includes Identity integration with Testcontainers, gateway, `tests/system`) | 358 passed, 0 failed, 0 skipped |
| `dotnet format HiveSpace.slnx --verify-no-changes` | clean |
| Notification `pytest` | **first run 55 passed, 20 errors (integration tests, Docker client error)**; re-run of `tests/integration` 20/20, full re-run 75/75 passed. Recorded as a transient Docker issue, not laundered. |
| Notification `mypy`, `lint-imports`, `ruff` | clean, 2 contracts kept, clean |
| `go test -short` (libs, inventory, cart) | pass |
| `cd web && pnpm turbo run lint typecheck test build` | 13/13 tasks |
| `tests/contract` (cucumber-js, cold start by its own runner) | 77/77 scenarios, 357 steps |
| `tests/e2e` (starts its own stack) | 22 passed, 2 skipped by design (narrow viewport) |
| Spectral (CI command, `contracts/`) | 0 errors, 18 warnings (pre-existing placeholder-service descriptions) |
| Migrations | `MigrationTests` apply `AddLockoutAndAudit` down and up from `AddExternalLogins`, and the check constraints reject values outside the design |

## Direct verification
- **Side effects:** audit rows, the single `account_locked` row under parallel load, the outbox `PasswordChanged` row (validated against its schema, no password in it) and the log lines are asserted by the tests above, not just the status codes.
- **NFR-SEC-005:** limits are 10 per minute by default; the gateway tests assert them with the defaults. The test stacks raise them to 1000 (D-9). Not re-measured by hand in this run; the implementer's manual check of the 11th request returning 429 is a claim, covered here by `RateLimitTests`.
- **NFR-SEC-009:** the sign-in part is covered; role grants, order and payment audit belong to later features (design §8).
- **Pending environment:** a shared rate-limit store for more than one gateway replica (risk R-5) needs a deployed environment; not blocking.
- **Screens:** not re-inspected visually in this run; AC-16 is verified through the component specs and the e2e sign-in journeys.
- **Contracts:** `identity.yaml` and the error codes pass Spectral and the contract runner; no undeclared divergence found.

## Findings
- **Rework:** none.
- **Design defect:** none.
- **Minor:** the Notification integration tests failed once with a Docker client error and passed on re-run; FEAT-004's report recorded a similar first-run flake in a different suite. Worth watching.

## RTM
Test ref appended for FR-AUTH-011, FR-AUTH-012, FR-AUTH-027: `features/FEAT-005-account-hardening/acceptance-report.md`.
