# Tasks: FEAT-003 — Change password

> Executes: docs/features/FEAT-003-change-password/technical-design.md
> Status: pending | in-progress | done per task · Last updated: 2026-11-22
> Prerequisites: FEAT-001 and FEAT-002 merged (both on `main`). No gateway change and no migration in this feature.

The screen is owned by ui-design's output (SCR-STF-018 and the SCR-STF-016 Password card: spec in [ui-design.md](ui-design.md), visual mockups in [mockups/](mockups/), registry in `docs/design-manifest.json`). T6 only wires it. No E2E task: UC-026 is not one of the critical flows in architecture §8.6.

- [x] T1 — Contracts first:
      - `PUT /v1/me/password` (`changeMyPassword`, bearer, `PasswordChange`, 204/400/401/409) in `openapi/identity.yaml`;
      - codes `identity.current_password_required`, `identity.current_password_incorrect`, `identity.password_not_set` with vi/en messages;
      - `events/PasswordChanged/v1.schema.json`;
      - `contracts/features/identity/change-password.feature` (success, 2a, 2b, empty current, 409, 401, vi/en message);
      - regenerate `web/packages/api-client`.
      (design §3.1, §3.3, §3.4; D-1; AC-10, AC-12)
      Done when: Spectral shows 0 errors (`hs-no-patch` included), the vi/en key-parity check passes, the client regenerates with no manual edits, and the new feature is discovered (red) by `tests/contract`.
- [x] T2 — Identity domain:
      - `Credential.ChangePassword(newHash, now)` and `Credential.Stamp` (Unix ms);
      - `PasswordPolicy.Evaluate(password, field = "password")`.
      (design §5.2; AC-5)
      Done when: unit tests pass for the policy reporting `newPassword` (too short + needs digit together, letters only, 129 chars, valid). Unit tests also show that the stamp changes with `ChangePassword` and survives a microsecond round trip. Registration tests and Architecture tests stay green.
- [x] T3 — Identity application and infrastructure:
      - `ChangeMyPasswordHandler` (no credential → `password_not_set`; all violations at once; nothing written on failure);
      - port `ISessionRevoker` and `OpenIddictSessionRevoker` (tracked status changes on the shared `IdentityDbContext`, keep the current authorization);
      - `PasswordChanged` enqueued in the same `SaveChangesAsync`.
      (design §3.1, §5.2; D-2, D-5; FR-AUTH-024, FR-AUTH-025, FR-AUTH-003; AC-1, AC-4, AC-5, AC-6 Identity part, AC-8)
      Done when: Testcontainers integration tests pass for each of these:
      - a success writes the new argon2id hash, revokes the other authorizations and their tokens (current one untouched), and writes exactly one outbox row that validates against the schema with no secret in it;
      - a wrong or empty current password, a policy breach, or both together each write nothing (hash, authorizations and outbox unchanged);
      - a credential-less user gets 409 semantics;
      - a null keep-id revokes all.
- [x] T4 — Identity API and session cookie:
      - `PUT /v1/me/password` in `MeEndpoints` (typed body, `oi_au_id` as the kept authorization, `hs.idp` reissued for the same user, 204);
      - `pwd_at` in `ToPrincipal` and at sign-in;
      - the stamp check in `/connect/authorize`;
      - no password in logs.
      (design §3.1, §3.2, §5.2; D-3, D-4; FR-AUTH-024, NFR-SEC-015; AC-1..AC-5, AC-7, AC-9, AC-10)
      Done when: `change-password.feature` passes on `tests/contract` in vi and en. `OidcFlowTests` cases pass with two code flows (storefront and seller clients) for one user:
      - after A changes the password, B's refresh gets `invalid_grant`, A's refresh succeeds, and A's pre-change refresh token then gets `invalid_grant` (AC-2, AC-3);
      - an old `hs.idp` is sent to `/signin` and the reissued one signs in silently (AC-7);
      - no token, a tampered token and a spoofed `X-User-Id` behave as in AC-9.
      A log-capture test shows neither password in the output.
- [ ] T5 — Notification:
      - template table by event type in `features/emails/service.py`;
      - `password_changed.{vi,en}.{txt,html}`;
      - pydantic `PasswordChanged` model mirroring the schema, validated by the dispatcher before any write.
      (design §5.3; D-5; FR-NOTIF-014; AC-6 Notification part)
      Done when: `uv run pytest && uv run mypy && uv run lint-imports` pass, including:
      - an integration test where one `PasswordChanged` gives one `email_records` row with template `password_changed` and one message in Mailpit/fake sender, in vi and in en, with the time in Asia/Ho_Chi_Minh;
      - a duplicate delivery that sends once;
      - a schema-sync unit test;
      - the verification email tests unchanged and green.
- [ ] T6 — UI integration (after ui-design):
      - move `PasswordField` to `packages/ui` and the client password rules to `packages/core`, and update FEAT-001 imports and tests (D-6);
      - `features/profile` `changePassword` in `api.ts`;
      - `ChangePasswordPage.vue` at `/profile/password` (`requiresAuth`) and the entry points ui-design names;
      - after 204: `refresh()`, clear the form, success toast; 401 → `markSignedOut()`; 409 message; server `details` on the matching field; no double submit.
      (design §5.5; AC-11)
      Done when: the page renders and conforms to its ui-design spec in default, submitting, field-error, wrong-current-password and success states. Component tests cover the 204 → `refresh()` call, the field mapping, the 401 and the 409. `pnpm turbo run lint typecheck test build` passes, including Sheriff and vi/en parity.
- [ ] T7 — Verify: AC-1..AC-12 each map to a passing test or a recorded demonstration (AC-11 checked in the browser against the AppHost stack: change the password, still signed in, a second browser signed out within 15 minutes and not silently signed back in). `dotnet build/test`, the Notification and web checks, `dotnet format`, boundary rules, Spectral, buf and `tests/contract` are green. `docs/rtm.md` Test ref cells are left to acceptance verification.
      Done when: the commands in AGENTS.md all pass.
