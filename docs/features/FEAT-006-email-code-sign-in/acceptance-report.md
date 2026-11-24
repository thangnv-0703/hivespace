# Acceptance Report: FEAT-006 — Sign in with an email code

> Verdict: Accepted · Date: 2026-11-24
> Standard: technical-design.md §6 @ 82eace4 · Sources: srs.md (FR-AUTH-028..036, FR-NOTIF-016, NFR-SEC-005/009/016), use-cases.md (UC-029)
> Repo state audited: 206432a (`origin/feat/006-email-code-sign-in`, T1–T13; the local branch was still at the design commit 82eace4 and was fast-forwarded first). No test file was changed by this audit.

## Verdict summary
All 19 acceptance criteria hold. Every suite was re-run from the repo head and was green (nothing taken from tasks.md's verification record). The feature's test diff is additive: no skip, todo or deleted case, and the only existing assertions edited are justified by the new criteria. A mutation (`MaxWrongCodes` 5 → 6) turned a unit test red. No rework and no design-defect findings; three minor observations are recorded below. The RTM Test ref is appended for FR-AUTH-028..036 and FR-NOTIF-016.

## Standard (re-derived)
FR-AUTH-028 (6-digit code by email, 5-minute lifetime), -029 (same response for any address), -030 (correct code signs in, single use, tokens as FR-AUTH-009), -031 (wrong or expired or ended is rejected without revealing existence, wrong code counts on the lockout), -032 (five wrong codes end the challenge), -033 (60 s per address, states the wait), -034 (new code invalidates earlier ones), -035 (locked account: code not evaluated), -036 (correct code verifies the email), FR-NOTIF-016 (email with code and validity, in the user's language), NFR-SEC-016 (CSPRNG, hash only, constant-time, once), NFR-SEC-005 (10 per minute per IP), NFR-SEC-009 (audited), UC-029 main, 2a, 3a, 1a, 4a, 4b, 4c, 2b. Each FR has at least one criterion and the criteria say what their FR says. UC-029 2b is intentionally re-scoped by the design (D-6, requirements note in §8), not silently dropped.

## Audit table
| AC | Encodes | Test(s) | Audit | Observed |
| :- | :------ | :------ | :---- | :------- |
| AC-1 | FR-AUTH-028, NFR-SEC-016 | `CodeSignInFlowTests.Ac1_*` (event valid against its schema, `code_hash` = SHA-256(id ‖ code), row text holds no code or email), `CodeSignInApiTests.Ac1_ac3_*` | faithful | green |
| AC-2 | FR-AUTH-029, -031 | `CodeSignInFlowTests.Ac2_*` (two: request, then submit side by side with a real account) | faithful, see O-1 | green |
| AC-3 | FR-AUTH-033 | `CodeSignInFlowTests.Ac3_*` (59 s / 60 s, known and unknown, eight parallel), `CodeSignInApiTests.Ac1_ac3_*` (`Retry-After: 1`) | faithful | green |
| AC-4 | FR-AUTH-034 | `CodeSignInFlowTests.Ac4_*` (two) | faithful | green |
| AC-5 | FR-AUTH-030, NFR-SEC-016 | `CodeSignInFlowTests.Ac5_*` (three: once, trimmed code, return URL rule, no stamp without a password, six parallel), `CodeSignInApiTests.Ac5_*` (cookie, `/connect/authorize` and `/connect/token` with and without a password, `amr` otp, `auth_time`; six parallel over HTTP) | faithful | green |
| AC-6 | FR-AUTH-031, -032 | `CodeSignInFlowTests.Ac6_*` (counts on challenge and lockout, fifth ends, correct code after is ended) | faithful | green |
| AC-7 | FR-AUTH-031 | `CodeSignInFlowTests.Ac7_*` (exactly at 5 min, correct and wrong code, nothing counted) | faithful | green |
| AC-8 | FR-AUTH-035, -011, -012 | `SignInWithCodeHandlerTests.Ac8_*` (`Matches` spy, count unchanged), `CodeSignInFlowTests.Ac8_*` (three wrong passwords + two wrong codes lock; correct code while locked → 423; success clears the row; ten parallel → 5/5, one `account_locked`), `CodeSignInApiTests.Ac8_*` (no 500) | faithful | green |
| AC-9 | FR-AUTH-036 | `CodeSignInFlowTests.Ac9_*`, `CodeSignInApiTests.Ac5_ac9_*` (`email_verified` true in the access token) | faithful | green |
| AC-10 | NFR-SEC-009, -016 | `CodeSignInFlowTests.Ac10_*` (row per submission, null user for unknown, no row for a request, text columns hold no code or email), `CodeSignInApiTests.Ac10_*` (log capture: no code, code hash, email hash or email) | faithful | green |
| AC-11 | FR-AUTH-031 | `CodeSignInFlowTests.Ac11_*` (two), `SignInWithCodeHandlerTests.Ac11_*` (incl. Arabic-Indic digits), `CodeSignInApiTests.Ac11_*` | faithful | green |
| AC-12 | D-3 | `SignInChallengeSweeperTests.Ac12_*` (2 days and 1 day + 1 min deleted, 23 h and open kept) | faithful | green |
| AC-13 | FR-NOTIF-016, FR-LOC-005 | `test_sign_in_code.py`, `test_verification_email.py` FEAT-006 block (vi and en subject and "5 phút"/"5 minutes", redelivery sends once, extra key and non-6-digit code write nothing) | faithful | green |
| AC-14 | FR-AUTH-028, -030, FR-NOTIF-016 | `tests/system` `CodeSignInFlowTests` (real stack: gateway → Identity → Service Bus → Notification → Mailpit → submit) | faithful, see O-2 | green |
| AC-15 | NFR-SEC-005 | gateway `RateLimitTests.Feat006_ac15_*` (both paths, 11th call, shared window, other IP unaffected) | faithful | green |
| AC-16 | design §3.5 | `ApiErrorTests.Retry_after_*`, `No_retry_after_*` | faithful | green |
| AC-17 | FR-AUTH-028..033, -035, FR-LOC-004 | storefront `code-sign-in.spec.ts` (link with `returnUrl`, 201/400/both 429/no-`returnUrl` redirect, paste and submit, wrong, expired, ended, locked, resend, no stored challenge), ui `code-input.spec.ts`, key-count and parity checks | faithful | green |
| AC-18 | ADR-0025 | `code-sign-in.feature` (13 scenarios: request 201/429/unknown/malformed, correct, reuse, wrong, fifth, expired, superseded, locked, unknown-then-wrong, not six digits) | faithful | 90/90 scenarios (new and old) |
| AC-19 | dev-standards §5, FEAT-005 | `LayerRulesTests` (Identity architecture tests), Spectral, Sheriff/i18n (web lint), import-linter, FEAT-005 lockout/sign-in/change-password tests unchanged | faithful | green |

## Corrected tests
None.

## Anti-fake-green review
Diff of test files, `main..206432a`: additive. Existing assertions edited, all justified by the new criteria: `MigrationTests` (the FEAT-005 check that `method = 'code'` is rejected now uses `'sms'`, since `code` becomes legal; table list gains `sign_in_code_challenges`), `ApiErrorTests` (helper extracted so AC-16 can read the headers; the original case is unchanged in meaning), `Rig.cs` (new handlers and helpers), `tests/contract/support/world.ts` (new world fields). Grep of the added lines for `Skip =`, `.skip(`, `xit(`, `xfail`, `todo(`: none. No case deleted. Mutation: `SignInCodePolicy.MaxWrongCodes` 5 → 6 failed `The_fifth_wrong_code_ends_the_challenge_and_a_sixth_changes_nothing`; reverted.

## Independent execution
All run from the repo head on this machine (Docker Desktop started for the run).

| Check | Result |
| :-- | :-- |
| `dotnet build HiveSpace.slnx` | 0 warnings, 0 errors |
| `dotnet test --solution HiveSpace.slnx` (Identity unit, architecture and Testcontainers integration, gateway, Errors, Catalog, `tests/system` with the real stack) | 420 passed, 0 failed, 0 skipped |
| Notification `pytest`, `mypy`, `lint-imports` | 96 passed; no issues in 17 files; 2 contracts kept |
| `go test -short` (libs, inventory, cart) | pass |
| `cd web && pnpm turbo run lint typecheck test build` | 13/13 tasks |
| `tests/contract` (cucumber-js, ADR-0025) | 90/90 scenarios, 434 steps |
| Spectral (CI command, `contracts/`) | 0 errors, 18 warnings (all pre-existing missing operation descriptions, none from the two new operations) |
| Migrations | `MigrationTests`: `AddSignInCodeChallenges` applies down and up from `AddLockoutAndAudit`; the check constraints reject values outside the design |
| `dotnet format --verify-no-changes`, `ruff` | not re-run by this audit (the build is warning-free) |

`tests/e2e` was not run: UC-029 is not a critical flow (architecture §8.6), so no E2E path is owed by this feature.

## Direct verification
- **NFR-SEC-016:** `SignInCodeGenerator` reviewed in source: `RandomNumberGenerator.GetInt32(0, 1_000_000)` zero-padded, SHA-256 over the 16 id bytes and the code, `CryptographicOperations.FixedTimeEquals`; single use is the challenge's `MarkUsed` plus the row version (six parallel correct submits give one 200, over the service and over HTTP). Only the hash is stored (AC-1 reads the raw row as JSON).
- **FR-AUTH-035:** the spy test proves `Matches` is not called for a locked account; the handler checks expiry, then the lock, then compares.
- **NFR-SEC-009 and NFR-LEG-001:** audit rows carry type, method, reason, user id and correlation id only; asserted not to contain the code or the email. Log capture shows only the challenge id and user id.
- **NFR-SEC-005:** the gateway test asserts the 11th call from one IP is 429 with `Retry-After`, and that another IP is unaffected. Not re-measured by hand.
- **Mail path:** AC-14 observed the real email in Mailpit with the code and "Your HiveSpace sign-in code", then signed in with it.
- **Screens:** not inspected visually in this run (no dev-server session). The states listed in the manifest for SCR-STF-021 and SCR-STF-022 are covered by `code-sign-in.spec.ts` (default, too soon, rate limited, 400, wrong, expired, ended, locked, resend, no stored challenge); "starting", "sending", "verifying" and the generic network error are not asserted by a test. SCR-STF-022 stays `escalation-pending` in the manifest for E-1 (`CodeInput`), which is a design-system decision, not a defect of this feature.
- **Pending environment:** a rate-limit store shared across several gateway replicas (R-5) needs a deployed environment; not blocking.

## Findings
- **Rework:** none.
- **Design defect:** none.
- **O-1 (observation, design wording):** AC-2 says an unknown address's challenge gives "the same 401 bodies as for a real account (invalid, then ended at the fifth)". For a real account whose lockout count is zero, the fifth wrong code is a `423 account_locked` (§3.3 and AC-8), so the two differ at the fifth. The test therefore compares the first four answers and asserts the unknown address ends at the fifth, which is the faithful reading given AC-8. This is the lock-reveals-existence trade-off already accepted in FEAT-005 D-7 (registration already reveals existence). Suggest rewording AC-2 to say "up to the account's lock". Owner: detailed-design.
- **O-2 (observation, test strength):** the log check in the system test looks for the code only as a space-delimited token (`" {code} "`) in service console output. The strict check (the code anywhere in captured logs) is in `CodeSignInApiTests.Ac10_*`, so AC-10 is covered; the system test covers AC-14. Left as is, because a bare six-digit match could collide with ports or timestamps in a real stack's logs.
- **O-3 (process):** the local branch was at the design commit; the implementation lived only on `origin/feat/006-email-code-sign-in`. Nothing to fix in the feature. The `tasks.md` verification record says `tests/system` needs `Parameters:postgres-password`; in this run it passed with the machine's existing configuration. The record's "pending local run" for `tests/e2e` is not owed (see above).

## RTM
Test ref appended for FR-AUTH-028, FR-AUTH-029, FR-AUTH-030, FR-AUTH-031, FR-AUTH-032, FR-AUTH-033, FR-AUTH-034, FR-AUTH-035, FR-AUTH-036 and FR-NOTIF-016: `features/FEAT-006-email-code-sign-in/acceptance-report.md`.
