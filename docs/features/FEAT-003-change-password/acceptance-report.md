# Acceptance Report: FEAT-003 — Change password

> Verdict: Accepted · Date: 2026-11-23
> Standard: technical-design.md §6 @ 434b97f · Sources: srs.md, use-cases.md (UC-026)
> Repo state audited: 64a2f3c (434b97f + one acceptance test correction)

## Verdict summary
Accepted. All 12 criteria are covered by tests that assert what the criteria say, every suite was run fresh in this audit and is green, and the behavior was observed directly on a cold-started AppHost stack. One test gap was found and corrected (AC-9 gateway coverage of the password route). The findings are minor and non-blocking. The RTM Test ref is appended for the five requirements this feature implements.

## Audit table
| AC | Encodes | Test(s) | Audit | Observed |
| :- | :------ | :------ | :---- | :------- |
| AC-1 | FR-AUTH-024, NFR-SEC-002, UC-026 1–3 | `ChangePasswordFlowTests.Ac1…` (argon2id prefix, hash differs, stamp returned, sign-in new ok / old fails); contract "A correct change signs in with the new password only" (401 `identity.invalid_credentials`) | faithful | green |
| AC-2 | FR-AUTH-024, NFR-SEC-015 | `ChangePasswordFlowTests.Ac2…` (other authorization and token `revoked`, current `valid`, other user untouched); `ChangePasswordOidcTests.Ac2_ac3…` (B refresh → `invalid_grant` on a real seller-client code flow) | faithful; mutation check (revoked → valid) turned Ac2 and the null-keep test red | green |
| AC-3 | NFR-SEC-015 | `ChangePasswordOidcTests.Ac2_ac3…` (A refresh 200, a further refresh 200, pre-change token replay → `invalid_grant`) | faithful | green |
| AC-4 | FR-AUTH-025, UC-026 2a | `Ac4_ac5_a_failure_writes_nothing` (wrong / empty / null current: code, hash and `UpdatedAt`, authorization, outbox unchanged); contract wrong and empty scenarios | faithful | green |
| AC-5 | FR-AUTH-003, NFR-SEC-003, UC-026 2b | `Ac5_…together` (exact ordered list across both fields); `DomainTests.Ac5…` (`abc`, `abcdefgh`, 129 chars); contract outline (`abcdefgh`, `12345678`, 129 chars, combined) | faithful | green |
| AC-6 | FR-NOTIF-014, NFR-REL-005, FR-LOC-005 | `Ac6…` (one row, schema-valid, no secret); failure tests assert empty outbox; notification `test_a_password_change_sends_one_email…` (vi + en, `00:30 23/11/2026` for 17:30 UTC), duplicate-delivery test, schema-sync unit test | faithful | green |
| AC-7 | FR-AUTH-024, SCR-STF-018 | `ChangePasswordOidcTests.Ac7…` (old cookie → `/signin`, `hs.idp` cleared; reissued cookie → `code=`) | faithful | green |
| AC-8 | UC-026 1a deferred | `Ac8…` (409 `identity.password_not_set`, no event); contract scenario | faithful | green |
| AC-9 | FR-AUTH-015, FR-AUTH-016 | `Ac9_no_token_or_a_tampered_token…` (401, spoofed `X-User-Id` changes nothing); contract 401 outline; gateway 401/403 for `PUT /v1/me/password` | **gateway part was missing → corrected** | green |
| AC-10 | FR-LOC-004, NFR-SEC-002 | contract vi message scenario; `Ac10_neither_password_appears_in_the_logs` (Trace-level capture, 4 secrets); vi/en key parity (web + contracts) | faithful (see minor M-2) | green |
| AC-11 | UC-026, FR-AUTH-024 | `password.spec.ts` (204 → `refresh()`, clear, toast, field mapping, 401, 409, 500, double submit); browser demonstration below | faithful | green + observed |
| AC-12 | dev-standards §9.1, ADR-0025 | `contracts/features/identity/change-password.feature` on `tests/contract` | faithful | 47/47 scenarios green |

Standard audit: every FR in the trace has at least one criterion (FR-AUTH-003 is partial per the plan: the password-change part). UC-026 main flow, 2a and 2b are represented; 1a is correctly deferred to FEAT-005. NFR-SEC-015 and NFR-SEC-003 appear as concrete criteria. No misencodings found. The FR-AUTH-025 note ("counts toward lockout of FR-AUTH-011") is deferred to FEAT-005 in the plan, design §8 and the RTM, so it is not a design defect.

## Corrected tests
- **AC-9** — commit `64a2f3c` ("FEAT-003 acceptance: correct test for AC-9"). The gateway AC-9 tests covered only the profile paths, so nothing showed that `PUT /v1/me/password` is 401 without a session and 403 without `X-HS-CSRF`. Added the password path to the 401 theory and turned the CSRF test into a theory over both routes. The new cases pass against the code.

## Independent execution
All run from commit 64a2f3c with the harness commands in AGENTS.md; nothing reused from the implementation session.
- `dotnet build HiveSpace.slnx`: 0 warnings, 0 errors. `dotnet test --solution HiveSpace.slnx`: 227 passed, 0 failed, 0 skipped (includes Identity integration with Testcontainers, Gateway, and `tests/system`).
- Notification: `uv run pytest` 46 passed; `uv run mypy` clean; `uv run lint-imports` 2 contracts kept.
- Web: `pnpm turbo run lint typecheck test build --force` (cache bypassed): 13/13 tasks successful; storefront 60 tests, ui 37, core 7, seller-centre 2.
- `tests/contract`: 47 scenarios, 208 steps, all passed.
- Mutation check on `OpenIddictSessionRevoker` (token status left `valid`): `Ac2…` and the null-keep test went red; change restored (no diff).
- Migrations: none in this feature (design §4); the git range shows no migration file. Nothing to apply.
- E2E: none owed (tasks.md; UC-026 is not an architecture §8.6 critical flow). Not run.
- No flaky test observed.

## Direct verification
Cold-started with `dotnet run --project apphost/HiveSpace.AppHost`, torn down afterwards (the three processes this run started were stopped; the Docker containers Aspire created were left running).
- **AC-11 in the browser:** registered a test account, signed in, opened `/profile/password` (an unauthenticated visit redirected to sign-in). Changed the password through the form: success toast shown, all three fields cleared, then `GET /v1/me/profile` returned 200 and `/bff/user` reported authenticated, so the session stayed signed in.
- **FR-NOTIF-014:** Mailpit held exactly one "Mật khẩu HiveSpace của bạn đã được thay đổi" email for the account, with the change time shown as 06:01 23/11/2026 (giờ Việt Nam) for a 23:01 UTC event, and no link.
- **FR-AUTH-024 / NFR-SEC-015 across two sessions:** observed through the real OpenIddict code flows in `ChangePasswordOidcTests` (storefront and seller clients). A second physical browser was not driven by hand.
- **NFRs:** NFR-SEC-002 (no password in logs) verified by the log-capture test. No binding perf NFR for this feature. Pending environment: none.
- **Contracts:** request, response and error shapes match design §3 (204/400/401/409; `details` entries per field; event schema).

## Findings
**Rework:** none.

**Design defect:** none.

**Minor (non-blocking):**
- M-1: The gateway AC-9 tests carried no password-route case (corrected, see above).
- M-2: AC-10 says the three new codes come back in the request language. The API-level language assertion is vi only (contract scenario on `common.validation_failed`); en and the `details` messages rest on the vi/en key-parity checks and the web copy tests. Worth one more contract scenario in en when FEAT-005 touches this feature file.
- M-3: The "second browser is signed out and not silently signed back in" demonstration in T7 is proven at the HTTP level (AC-2, AC-3, AC-7 tests), not with two physical browsers.
- M-4: Lockout counting and a rate limit on this endpoint are deferred to FEAT-005 (design §8). Until then the endpoint has no attempt limit; recorded so the FR-AUTH-025 note is not read as delivered.
- M-5: Found after the audit: the Password card button on the profile page did not navigate (`@click.prevent` cancelled the click before `navigate`). No test clicked it and the browser check opened the page by URL. Fixed with a click test (`ProfilePage` spec) that fails without the fix.

## RTM
Test ref appended to `docs/rtm.md` (Test ref column only, append-only):
- FR-AUTH-024, FR-AUTH-025, FR-NOTIF-014, NFR-SEC-015 → `features/FEAT-003-change-password/acceptance-report.md`
- FR-AUTH-003 → existing FEAT-001 ref kept; `features/FEAT-003-change-password/acceptance-report.md (partial)` appended (the password-change part only)
