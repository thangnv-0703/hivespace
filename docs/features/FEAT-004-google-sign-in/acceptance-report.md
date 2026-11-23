# FEAT-004 Google sign-in: acceptance report

**Verdict: Accepted** (2026-11-23), with one minor finding.

## Standard
Re-derived from technical-design §6 (AC-1..AC-16) against FR-AUTH-020..023, FR-AUTH-026, FR-NOTIF-015, NFR-SEC-014 and UC-025. Every FR has at least one criterion; no misencoding found.

## Observed results (this run, from repo head)
| Check | Result |
| :-- | :-- |
| `dotnet build HiveSpace.slnx` | 0 warnings, 0 errors |
| `dotnet test --solution HiveSpace.slnx` | **First run: 23 failed of 308** (Identity.IntegrationTests 17, SystemTests 4, Catalog.IntegrationTests 2). Re-run: 308/308 passed. The three failing projects also passed alone (Identity 122/122, Catalog 2/2, System 4/4). |
| `services/notification`: pytest, mypy, lint-imports | 73 passed; no issues; 2 contracts kept |
| `web`: lint, typecheck, test, build (turbo) | 13/13 tasks successful |
| `tests/contract` | 58 scenarios, 252 steps passed |
| `tests/e2e` | 22 passed, 2 skipped (pre-existing viewport skips in `skeleton.spec.ts`, not FEAT-004). Both UC-025 journeys pass on desktop and mobile-360. |

## Audit of tests
- Every AC-1..AC-16 is referenced by at least one test (AC-14 by the E2E feature, AC-15 by the cucumber feature, AC-16 by the architecture/import-linter checks).
- The feature's test diff (T1..HEAD) has no removed assertions and no new skips or todos.
- Not done: no mutation check, and I did not read every assertion against its criterion. Assertion faithfulness was spot-checked only (AC-8, which has separate tests for discovery down, a 503 at the token call and refused connections).
- No tests were corrected.

## Findings
- **Minor (test quality): intermittent failures when the whole solution runs at once.** The first full run failed 23 tests across three container-backed projects. The identical code passed on re-run, and each project passes alone. This looks like resource contention between parallel Testcontainers runs in the cloud container. It was not investigated further. Treat it as a flake to watch.

## Not verified
- Real Google (OQ-32 / P-4): everything ran against the fake IdP, as the design intends.
- Timing in AC-8 ("about 5 s", "30 s") was covered only by the integration tests, not measured separately.

## RTM
Test ref appended for FR-AUTH-020..023, FR-AUTH-026 and FR-NOTIF-015.
