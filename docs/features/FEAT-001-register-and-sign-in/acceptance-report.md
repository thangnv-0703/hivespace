# Acceptance Report: FEAT-001 — Register, verify email, password sign-in and sign-out

> Verdict: **Accepted** · Date: 2026-11-20
> Standard: technical-design.md §6 @ 73e391d · Sources: srs.md (v0.16), use-cases.md
> Repo state audited: 9fb1776 (head 73e391d plus one acceptance test correction)

## Verdict summary
All 19 criteria are covered by tests that assert what the criterion says, and every suite was run fresh in this audit and passed. One test was weak (AC-15's replay clause) and was corrected toward the criterion; it passes and fails when the behavior is broken. No rework or design-defect findings; four minor findings are recorded. The feature is verified; RTM Test ref is appended.

## Audit table
| AC | Encodes | Test(s) | Audit | Observed |
| :- | :------ | :------ | :---- | :------- |
| AC-1 | FR-AUTH-001, FR-ROLE-001, NFR-SEC-002 | `RegistrationFlowTests.Ac1…` (DB: unverified, Buyer, `$argon2id$`, no plaintext); contract `register.feature` 201 + userId | faithful | green |
| AC-2 | FR-AUTH-002, UC-001 2a | `Ac2_duplicate_email…` (upper-case), `Ac2_concurrent…` (8 parallel → one user, one event); contract 409 | faithful | green |
| AC-3 | FR-AUTH-003, NFR-SEC-003 | `Ac3_ac4_validation…`, `DomainTests` policy cases, contract weak-password | faithful (uses 3-char `abc`, same rule as the 5-char example; compliant password accepted in AC-1 and every sign-in test) | green |
| AC-4 | FR-AUTH-004 | `DomainTests` (`null`/blank email → `email_required`, empty and 61-char name), integration and contract (`a@`, empty name) | faithful (missing email is unit-level, not through the API) | green |
| AC-5 | FR-LOC-004 | `ApiErrorTests` (every code has vi+en, status and language mapping, 500 kept); contract vi/en outline | faithful | green |
| AC-6 | UC-001 4a, ADR-0002 | `Ac6_…` (outbox row, exact field set, same transaction, unsent), system test validates data against `UserRegistered/v1.schema.json` and checks `traceparent`; registration 201 does not wait for email | faithful. The "Service Bus down" clause is covered by the outbox design plus the transactional test, not by a broker-down run (see minors) | green |
| AC-7 | FR-NOTIF-002, FR-LOC-005, NFR-REL-005 | `test_verification_email.py` (one email in `en`, link, duplicate delivery → one email and one record, resend is its own email), `test_emails.py` (vi/en templates), system test (exactly one email in Mailpit after a 5 s wait, working link) | faithful | green |
| AC-8 | R-6 | `Ac8_…` (SHA-256 at rest, 25 h-old sent outbox row deleted), `test_the_token_is_stored_nowhere`, system test (token absent from gateway, identity, notification logs) | faithful | green |
| AC-9 | FR-AUTH-005 | `Ac9_…`, `OidcFlowTests` (`email_verified` false → true on the next token), contract 204 | faithful | green |
| AC-10 | FR-AUTH-006 | `Ac10_…` (unknown → invalid, 25 h → expired, resend-invalidated → expired, reuse → already_verified), contract, e2e invalid-link page | faithful | green |
| AC-11 | FR-AUTH-007 | `Ac11_…` (new token, old one dead, unknown and verified send nothing), contract 202 ×3 | faithful | green |
| AC-12 | FR-AUTH-009, ADR-0008 | `OidcFlowTests` (tokens issued, claims), `/bff/user` test (no tokens in body), e2e (HttpOnly `hs.bff` cookies only; no `eyJ` in local/session storage), contract (unverified sign-in, `hs.idp` cookie) | faithful; URL not inspected (minor) | green |
| AC-13 | FR-AUTH-010 | `Ac13_…` (identical failure object), contract outline (wrong password, unknown email); dummy-hash verification present in `SignInHandler` | faithful; no timing measurement (see pending) | green |
| AC-14 | FR-AUTH-014, NFR-SEC-004 | `Ac14_…` ×2 (refresh at <60 s, cookie updated, failed refresh → 401 and cleared), `OidcFlowTests` (rotation, old token `invalid_grant`, 15 min and 7 day lifetimes) | faithful | green |
| AC-15 | FR-AUTH-013, UC-002 3b | `OidcFlowTests` (revoke, then refresh fails), `Ac15_sign_out…`, **corrected** `Ac15_replaying_the_pre_logout_cookie…`, `Logout_clears…` (next authorize asks for credentials), e2e (login route asks for credentials again) | was weak → corrected | green |
| AC-16 | FR-AUTH-015, FR-AUTH-016 | `Ac16_…` ×3 (no cookie, tampered, wrong signature, expired, non-JWT → 401; valid → bearer, `X-User-Id`, `X-User-Roles`, spoofed copies replaced), cross-user case | faithful | green |
| AC-17 | ADR-0008 | `Ac17_…` (403 without header, passes with it, `returnUrl` theory incl. `//`, `\`, absolute), `Ac18_…` (sign-in `returnUrl` only `/connect/authorize`), contract returnUrl outline | faithful; **mutation check**: CSRF check disabled → `Ac17_a_non_get…` red; restored | green |
| AC-18 | FR-AUTH-008 (p) | `Ac18_…` and contract (unverified user signs in), claims test (`email_verified=false`) | faithful | green |
| AC-19 | Plan §5 criterion 10 | contract: 20 cucumber scenarios; e2e: register → Mailpit → verify → sign in → reload → sign out (desktop and 360 px) | met in substance; the criterion still names Reqnroll (see minors) | green |

## Corrected tests
- **AC-15** (`gateway/tests/Gateway.Tests/AuthTests.cs`, `GatewayTestHost.cs`; commit `9fb1776`, `FEAT-001 acceptance: correct test for AC-15`). The criterion says replaying the old cookie after sign-out cannot refresh and returns 401. The existing test only showed that the client's cookie jar was emptied, and the fake Identity accepted `good-refresh` even after it was revoked, so a replay was never exercised. T13's "replay of the old cookie fails" was also never written (neither the original Playwright spec nor the current Gherkin has it). The fake Identity now rejects revoked refresh tokens, and the new test captures the cookie before sign-out, signs out, replays the captured cookie with a near-expiry access token and expects `401 common.unauthenticated`. Mutation: with revocation ignored the test fails. Two test refresh tokens were renamed (`good-refresh-signout`, `good-refresh-replay`) so shared-fixture revocations do not leak between tests.

## Independent execution
All commands are from AGENTS.md; nothing was reused from another session, and the AppHost was cold-started by the harness.
| Suite | Command | Result |
| :-- | :-- | :-- |
| .NET build and all tests (incl. Identity unit/integration/architecture, Gateway, Errors, system test with cold AppHost) | `dotnet build HiveSpace.slnx && dotnet test --solution HiveSpace.slnx` | 105/105 pass; after the correction Gateway is 21/21 |
| Notification | `uv run pytest && uv run mypy && uv run lint-imports` | 17 pass; mypy clean (15 files); 2 import contracts kept |
| Web | `pnpm turbo run lint typecheck test build --force` (cache bypassed) | 13/13 tasks; storefront 29 tests, includes Sheriff and locale parity |
| API contract (Gherkin) | `cd tests/contract && pnpm test` | 20 scenarios / 77 steps pass |
| E2E | `cd tests/e2e && pnpm test` (Playwright started the AppHost) | 15 passed, 1 skipped (seller-centre on 360 px, an existing skeleton skip, not FEAT-001); no retries or flakiness observed |
| Migrations | `Identity.IntegrationTests.MigrationTests` (Testcontainers Postgres, up and down) and the Alembic migration inside the Notification integration tests | pass |
Not re-run in this audit: Spectral, buf and `dotnet format --verify-no-changes` (not available through `npx` here; they are CI gates and were reported green by implementation, unverified by me). Containers left behind by the E2E AppHost were removed by name.

## Direct verification
- **Side effects:** the email was observed in Mailpit (subject, link, one message after a 5 s wait) and the outbox row, SHA-256 token hash and `$argon2id$` credential were read from the database by the tests above.
- **NFRs:** NFR-SEC-002 (argon2id), NFR-SEC-003 (policy), NFR-SEC-004 (15 min access, 7 day rotating refresh) were observed in tests. No latency NFR is binding for this feature.
- **Pending (non-blocking):** (a) AC-13 timing parity between unknown email and wrong password is implemented (dummy hash) but not measured; verify with a timing sample against a realistic container size. (b) AC-6 "Service Bus down" is not exercised against a stopped emulator; verify by stopping the `messaging` container during a registration.
- **Screens:** SCR-STF-012/013/014 rendered in E2E in default, error (wrong password alert, invalid link) and success states, desktop and 360 px with no horizontal overflow; vitest covers states. Locators in the manifest resolve (the E2E selectors use them).
- **Contracts:** request/response/error shapes seen in the contract suite match design §3.1 and §3.7.

## Findings
**Rework:** none.

**Design defect:** none.

**Minor (non-blocking):**
1. AC-15 wording: "replaying the old cookie… returns 401" is only true once the access token is near expiry (it stays valid for up to 15 min, per the FR-AUTH-015 note). The corrected gateway test sets that precondition; an E2E replay is not feasible without clock control. Suggest the design wording say "once the access token needs refreshing". T13's "done when" overstated what was built.
2. AC-19 still names Reqnroll; the contract suite moved to cucumber-js and journeys to playwright-bdd (ADR-0025, ADR-0026). The intent is met; update the criterion text on the next amendment.
3. AC-12: the E2E checks cookies and web storage, and a gateway test checks the `/bff/user` body; no check inspects URLs for tokens.
4. AC-6 broker-down and AC-13 timing are asserted by design, not by a run (see pending).

## RTM
Test ref appended with `features/FEAT-001-register-and-sign-in/acceptance-report.md` for FR-AUTH-001..010, FR-AUTH-013..016, FR-ROLE-001, FR-NOTIF-002, FR-LOC-004; with `(partial)` for FR-AUTH-008, FR-NOTIF-001 and FR-LOC-005 (the design implements them partially).
