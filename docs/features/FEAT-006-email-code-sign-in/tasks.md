# Tasks: FEAT-006 — Sign in with an email code

> Executes: docs/features/FEAT-006-email-code-sign-in/technical-design.md
> Status: pending | in-progress | done per task · Last updated: 2026-11-23
> Prerequisites: FEAT-005 (with FEAT-003 and FEAT-004) merged to `main` — `LockoutRecorder`, `audit_entries`, the gateway `auth` limiter and `Retry-After` on 429 exist only on `feat/005-account-hardening` today.

The screen states of SCR-STF-021, SCR-STF-022 and the SCR-STF-012 link are owned by ui-design's output ([ui-design.md](ui-design.md), [mockups/](mockups/)); T11 only wires them. `CodeInput` is provisional under ui-design escalation E-1. UC-029 is not among the critical flows of architecture §8.6, so no E2E task is owed; T10 adds a system test for the real Identity → broker → Notification → Mailpit path instead.

- [ ] T1 — Contracts first:
      - error codes `identity.sign_in_code_invalid` (401), `identity.sign_in_code_expired` (401), `identity.sign_in_challenge_ended` (401), `identity.sign_in_code_too_soon` (429) with vi/en messages;
      - `identity.yaml`: `requestSignInCode` (`POST /v1/users/sign-in/code-challenges`, `SignInCodeRequest`, `201 SignInChallenge`, 400, 429 with `Retry-After`), `signInWithCode` (`POST /v1/users/sign-in/code`, `SignInWithCodeRequest`, `200 {redirectUrl}` with `Set-Cookie`, 400, 401, 423, 429 from the gateway);
      - `contracts/events/SignInCodeRequested/v1.schema.json`;
      - `contracts/features/identity/code-sign-in.feature`.
      (design §3.1–§3.4, §3.6; AC-18)
      Done when: Spectral shows 0 errors, the vi/en key-parity check passes, the regenerated web client compiles, and `tests/contract` discovers the new scenarios (red).
- [ ] T2 — `Retry-After` on `ApiException`: optional `RetryAfterSeconds`; `ApiErrorResponder` writes the header when set.
      (design §3.5)
      Done when: `HiveSpace.BuildingBlocks.Errors.Tests` pass with a case for AC-16 (header with the value, no header without it).
- [ ] T3 — Schema and domain: `SignInCodePolicy`, `SignInCodeChallenge` and `ChallengeEndReason`, `AuditMethod.Code`; `IdentityDbContext` mapping (`sign_in_code_challenges` with checks, the three indexes incl. the partial unique one, `xmin`; `code` in `ck_audit_entries_method`); migration `AddSignInCodeChallenges`.
      (design §4, §5.2 Domain; FR-AUTH-028, FR-AUTH-032, FR-AUTH-033, FR-AUTH-034; D-3)
      Done when: the migration applies clean up and down on a database at `AddLockoutAndAudit` (`MigrationTests`), and unit tests for the challenge cover: expiry at the exact boundary, `RetryAfter` before and at 60 s, the fifth `RecordWrongCode` ending it with `attempts` and a sixth changing nothing, `Supersede`/`MarkUsed` on an ended challenge changing nothing.
- [ ] T4 — Code generator and store: `ISignInCodeGenerator` + `Security/SignInCodeGenerator`; `ISignInChallengeStore` in `EfUserRepository`; `SignInChallengeConflictException` and the `ConcurrencyConflictException` mapping for challenges.
      (design §5.2 Application ports, Infrastructure; NFR-SEC-016; D-7)
      Done when: unit tests show `NewCode` is six digits and uses the CSPRNG path, `HashCode` differs for the same code under two ids, `Matches` uses `FixedTimeEquals` and rejects a wrong code; an integration test shows a second open challenge for one `email_hash` raises `SignInChallengeConflictException`.
- [ ] T5 — Request a code: `RequestSignInCodeHandler` (§3.2 order) and the `requestSignInCode` endpoint (`TooSoon` → `ApiException` with `RetryAfterSeconds`).
      (design §3.2, §5.2; FR-AUTH-028, FR-AUTH-029, FR-AUTH-033, FR-AUTH-034; UC-029 main 1–2, 2a, 3a, 1a; D-1, D-4)
      Done when: Testcontainers integration tests pass for AC-1, AC-2 (request part), AC-3 (fake `TimeProvider`, parallel requests) and AC-4 (superseded challenge part), and the malformed-email case of AC-11.
- [ ] T6 — Lockout staging: `LockoutRecorder.StageFailureAsync`/`StageSuccessAsync`, with `RecordFailureAsync`/`RecordSuccessAsync` rebuilt on them.
      (design §5.2 Application; D-5)
      Done when: every FEAT-005 lockout, sign-in and change-password test (unit and integration) passes unchanged.
- [ ] T7 — Sign in with a code: `SignInWithCodeHandler` (§3.3 order, one save per attempt, bounded retry), `SignInOutcome.PasswordStamp` nullable, `signInWithCode` endpoint signing into `hs.idp` with `amr=otp`; audit rows with method `code`.
      (design §3.3, §5.2; FR-AUTH-030, FR-AUTH-031, FR-AUTH-032, FR-AUTH-035, FR-AUTH-036; NFR-SEC-009, NFR-SEC-016; UC-029 main 3–5, 4a, 4b, 4c; D-5, D-8)
      Done when: a unit test shows `Matches` is not called for a locked account (AC-8 spy part), and integration tests pass for AC-2 (submit part), AC-4 (old code rejected), AC-5 (incl. `/connect/authorize` for an account with and without a password, the parallel correct submits), AC-6, AC-7, AC-8 (shared lockout, ten parallel wrong codes), AC-9, AC-10 and the rest of AC-11.
- [ ] T8 — Challenge purge: `SignInChallengeSweeper` (start and hourly, 1 day after expiry) registered in Identity.
      (design §5.2 Infrastructure; D-3)
      Done when: an integration test of `RunOnceAsync` passes for AC-12.
- [ ] T9 — Notification: pydantic `SignInCodeRequested`, validation in `dispatch.py`, `CONTACT_EVENTS`, `KINDS["SignInCodeRequested"]`, templates `sign_in_code.{vi,en}.{txt,html}`.
      (design §5.4; FR-NOTIF-016, FR-LOC-005)
      Done when: `uv run pytest && uv run mypy && uv run lint-imports` pass, with tests for AC-13 (vi and en content, dedupe on redelivery, rejected payloads write nothing, contact upsert) and a test that keeps the pydantic model and the JSON schema in step.
- [ ] T10 — Wire the real path: gateway test for the code endpoints under `auth`; system test in `tests/system` for request → Mailpit email → submit.
      (design §5.3, §6; NFR-SEC-005; FR-AUTH-028, FR-AUTH-030, FR-NOTIF-016)
      Done when: the gateway test passes for AC-15 and `dotnet test --project tests/system/HiveSpace.SystemTests` passes for AC-14.
- [ ] T11 — Web wiring: `api.ts` calls, `codeChallenge.ts`, `CodeRequestPage.vue` (SCR-STF-021, `/signin/code`), `CodeEntryPage.vue` (SCR-STF-022, `/signin/code/enter`), the SCR-STF-012 link, routes, exports, vi/en keys — realizing ui-design's specs for these screens.
      (design §5.5; FR-AUTH-028..033, FR-AUTH-035; UC-029; screens per [ui-design.md](ui-design.md), visual reference in [mockups/](mockups/))
      Done when: Vitest component tests pass for AC-17, the screens render and match their ui-design specs, and `pnpm turbo run lint typecheck test build` passes (Sheriff, key parity).
- [ ] T12 — Contract steps: step definitions for `code-sign-in.feature` (read the code from the outbox row, fake clock or direct `expires_at` update for expiry, lock via the FEAT-005 steps).
      (design §3.6; AC-18)
      Done when: `cd tests/contract && pnpm test` passes for every scenario, old and new.
- [ ] T13 — Verify: every acceptance criterion AC-1..AC-19 demonstrably passes; boundary rules green (Identity architecture tests, Spectral, depguard not affected, import-linter, Sheriff); the full .NET, Python and web suites green; no code, code hash or email in the log output of the T7 and T10 runs; record results in this file.
      (design §6; AC-19)
      Done when: all of the above is shown in one run and written down here, ready for acceptance verification.
