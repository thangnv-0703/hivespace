# Tasks: FEAT-005 — Account hardening: lockout, set a password, rate limits, audit

> Executes: docs/features/FEAT-005-account-hardening/technical-design.md
> Status: pending | in-progress | done per task · Last updated: 2026-11-23
> Prerequisites: FEAT-004 implemented and merged to `main` (Google sign-in, `external_logins`, `complete` endpoint, exact `pwd_at` rule, the `external_login_linked` templates). Decisions D-2, D-5 and D-7 are confirmed by the user (2026-11-23).

The screen states (SCR-STF-012 locked and rate-limited, SCR-STF-018 set mode and re-authentication prompt, SCR-STF-016 card label) are owned by ui-design's output ([ui-design.md](ui-design.md), [mockups/](mockups/)); T10 only wires them. UC-002 and UC-026 are not among the critical flows of architecture §8 (Testing row), so no E2E task is owed; T11 only keeps the existing E2E suite green under the new limits.

- [x] T1 — Contracts first:
      - error codes `identity.account_locked` (423), `identity.reauthentication_required` (403), `identity.password_already_set` (409), `common.rate_limited` (429) with vi/en messages;
      - `identity.yaml`: 423 on `signIn` and `changeMyPassword`, 429 on the three limited operations, new `setMyPassword` (`PUT /v1/me/password/initial`, `PasswordSet`), `Profile.hasPassword`;
      - `gateway.yaml`: `reauth` on `/bff/login`;
      - `contracts/features/identity/lockout.feature`, `set-password.feature`, and the locked plus en-language scenarios in `change-password.feature`.
      (design §3.1–§3.8; AC-17)
      Done when: Spectral shows 0 errors, the vi/en key-parity check passes, the regenerated web client compiles, and `tests/contract` discovers the new scenarios (red).
- [x] T2 — Schema and domain: `LockoutPolicy`, `SignInLockout`, `AuditEntry`/`AuditMethod`; `IdentityDbContext` mappings (`sign_in_lockouts` with `xmin`, `audit_entries` with checks and indexes); migration `AddLockoutAndAudit`.
      (design §4, §5.2 Domain; FR-AUTH-011; D-3, D-4)
      Done when: the migration applies clean up and down on a database at FEAT-004's `AddExternalLogins` (`MigrationTests`). Unit tests for `SignInLockout.RecordFailure`/`IsLocked` cover: lock on the fifth failure, window expiry restarting at 1, a finished lock restarting at 1, `IsLocked` at the exact boundary (AC-1, AC-3 domain part).
- [x] T3 — Lockout and audit in sign-in: ports `ILockoutStore`, `IAuditLog`, `ConcurrencyConflictException`; `EfUserRepository` implementation and exception mapping; `LockoutRecorder` with 3 tries; `SignInHandler` in the order of design §3.2; `sign-in` endpoint returns 423 and adds `auth_time`/`amr=pwd` to `hs.idp`; Google audit rows in FEAT-004's `ExternalSignInHandler` and `RecordSignInFailureHandler` for `OnRemoteFailure`.
      (design §3.2, §3.6 cookie part, §5.2; FR-AUTH-011, FR-AUTH-012; NFR-SEC-009; D-1, D-2, D-4, D-7)
      Done when: unit tests show the hasher is not called for a locked account (AC-2) and Testcontainers integration tests pass for AC-1, AC-2 (with a fake `TimeProvider`), AC-3, AC-4 (ten parallel failures), AC-6, AC-7 (Google with the fake IdP while locked) and AC-8 (audit rows and their content, same transaction).
      > Note (implementation): `LockoutRecorder` retries up to `Threshold + 1` = 6 times, not 3. Ten parallel failures need more than three rounds (AC-4 returned 500 with three); every conflict means another failure committed, and after five the account is locked and failures write nothing, so six always ends. Google audit rows also cover the `start` unavailable and `complete` pre-handler failures.
- [x] T4 — Lockout in change password: locked check first, wrong current password through `LockoutRecorder` (423 when it locks), a correct current password clears the lockout.
      (design §3.3; FR-AUTH-025 note, FR-AUTH-012; UC-026 2a)
      Done when: integration tests pass for AC-5, and FEAT-003's change-password tests stay green.
- [x] T5 — Audit retention: `AuditRetentionSweeper` (start and every 6 hours, 90 days) registered in Identity.
      (design §5.2 Infrastructure; NFR-SEC-009)
      Done when: an integration test of `RunOnceAsync` passes for AC-9.
- [x] T6 — Re-authentication plumbing: `auth_time` and `amr` in `UsersEndpoints.ToPrincipal`, FEAT-004 `complete` (`amr=google`) and both password reissues; `ConnectEndpoints` `max_age` check, `BuildPrincipal` claims, refresh copy; gateway `/bff/login?reauth=true` → `max_age=60`.
      (design §3.6, §5.2 Api, §5.3; D-5)
      Done when: Identity integration tests pass for AC-12 (old and fresh `auth_time` with `max_age`, claims in access and ID tokens before and after refresh), and a gateway test shows `max_age=60` only with `reauth=true`.
- [x] T7 — Set a password: `SetMyPasswordHandler`, `IUserRepository.AddCredential`, `CredentialExistsException` mapping, `PUT /v1/me/password/initial` with `hs.idp` reissue; `ProfileView.HasPassword`.
      (design §3.4, §3.5, §5.2; FR-AUTH-027, FR-NOTIF-014; UC-026 1a; D-5, D-8, D-10)
      Done when: integration tests pass for AC-10 (including sign-in with the new password and a silent `/connect/authorize` with the reissued cookie), AC-11 (each error, nothing written, the parallel 409) and AC-15.
- [x] T8 — Gateway rate limits: policies `auth` (per IP) and `auth-user` (per `sub`, else IP) from `RateLimits` options; `OnRejected` (429 envelope with `Retry-After`, or the `/signin?error=common.rate_limited` redirect for `/connect/external/*`); routes `me-password`, `me-password-rest`, `connect-external-callback`; `UseForwardedHeaders` behind `Gateway:TrustForwardedFor`; `Gateway__TrustForwardedFor=true` in the Azure deployment config.
      (design §3.7, §5.3; NFR-SEC-005; D-6, D-9)
      Done when: gateway tests pass for AC-13 (per IP, per user, other partitions unaffected, the callback redirect) and AC-14 (forwarded header trusted only when enabled); `RoutingTests` show the new routes reach Identity with the right authorization policy.
- [ ] T9 — Notification: the `passwordRemoved` paragraph of `external_login_linked.{vi,en}.{txt,html}` links to `{storefront}/profile/password`.
      (design §5.4; FR-NOTIF-015; FEAT-004 D-5)
      Done when: `uv run pytest && uv run mypy && uv run lint-imports` pass, with a template test for AC-18 in vi and en.
- [ ] T10 — UI integration (spec in [ui-design.md](ui-design.md), visual mockups in [mockups/](mockups/), registry in `docs/design-manifest.json`): `SignInPage.vue` (423, 429, `?error=common.rate_limited`, error without `returnUrl` → "Try again"); `ChangePasswordPage.vue` set mode and change-mode additions (`setMyPassword` in `api.ts`, the "Continue with Google" re-authentication action, 409 switch, 423, 429); `ProfilePage.vue` card label from `hasPassword`; vi/en keys.
      (design §5.5; FR-AUTH-012, FR-AUTH-027; AC-16)
      Done when: the screens render and conform to their ui-design specs for each state listed in AC-16; component tests cover each response mapping and the re-authentication navigation target; `pnpm turbo run lint typecheck test build` passes, including Sheriff and vi/en parity.
- [ ] T11 — Test stacks under real limits: the AppHost reads an optional `RateLimits__PermitLimit` parameter; `tests/e2e/playwright.config.ts` (`webServer.env`) and the `tests/system` fixture set it to 1000; document both settings next to the gateway configuration (scaffold-notes or gateway README) and update FEAT-003's `hs.idp` claim note.
      (design §5.3; D-9; §8 doc updates)
      Done when: `dotnet run --project apphost/HiveSpace.AppHost` with no parameter enforces 10 per minute (one manual check: the 11th sign-in through `http://localhost:5080` gets 429), and `tests/e2e` and `tests/system` pass against stacks started by their own runners.
- [ ] T12 — Verify: AC-1..AC-19 each map to a passing test or a recorded demonstration (AC-10 also once by hand: a Google-only account on the local stack is sent through Google by "Continue with Google" and then sets a password). `dotnet build/test`, `dotnet format`, the Notification and web checks, boundary rules (AC-19, including a log-capture test with no password, token or email), Spectral and `tests/contract` are green. `docs/rtm.md` Test ref cells are left to acceptance verification.
      Done when: the commands in AGENTS.md all pass.
