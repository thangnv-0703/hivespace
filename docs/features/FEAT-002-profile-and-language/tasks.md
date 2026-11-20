# Tasks: FEAT-002 — Profile and preferred language

> Executes: docs/features/FEAT-002-profile-and-language/technical-design.md
> Status: pending | in-progress | done per task · Last updated: 2026-11-19
> Prerequisites: FEAT-001 T8 (gateway BFF) before T6; FEAT-001 T9 (Notification contacts) before T7.

The screen is owned by ui-design's output (SCR-STF-016, spec in [ui-design.md](ui-design.md), visual mockups in [mockups/](mockups/), registry in `docs/design-manifest.json`); T9 only wires it. T9 also adds the "Profile" item to the header account menu (ui-design.md "Extras").

- [x] T1 — Contracts first (the Spectral rule `hs-no-patch` was added with this design): `GET/PUT /v1/me/profile` and `PUT /v1/me/profile/language` with `Profile`, `ProfileUpdate`, `Gender` and bearer security in `openapi/identity.yaml`; `POST /bff/session/refresh` in `gateway.yaml`; the seven new codes with vi/en messages; `UserProfileChanged/v1` schema; `contracts/features/identity/profile.feature`; regenerate `web/packages/api-client` (design §3; D-1; AC-10, AC-12).
      Done when: Spectral shows 0 errors with `hs-no-patch` active, the vi/en key-parity check passes, the client regenerates with no manual edits, and the feature is discovered (red) by Reqnroll.
- [x] T2 — Identity domain: `PhoneNumber` (E.164 normalize), `Gender`, `DateOfBirth` (future and 120-year rules against a given today), `Language`; `DisplayName.MaxLength = 80`; `User.UpdateProfile` and `User.ChangeLanguage` returning changed fields (design §5.2; D-2, D-3; AC-2, AC-3, AC-11).
      Done when: unit tests for every rule in design §3.1 pass (valid and invalid phones in each written form, date boundaries at today and 120 years, 80/81-char names), FEAT-001's too-long tests are moved to 81 chars and pass, and the Architecture tests stay green.
- [x] T3 — Identity persistence: map `phone`, `gender` (with `ck_users_gender`), `date_of_birth`; widen `display_name`; migration `AddUserProfile` (design §4).
      Done when: the migration applies up and down clean on Testcontainers Postgres after `InitialIdentity`, and `dotnet ef migrations has-pending-model-changes` reports none.
      Note (cloud session, no Docker): up, down and up again were run with `dotnet ef database update` on a local Postgres 16 and `has-pending-model-changes` reported none; the Testcontainers tests `AddUserProfile_applies_down_and_up_after_the_initial_migration` and `Gender_is_limited_to_the_four_values` are written and compile, and run in CI or on a Docker machine (pending local run).
- [x] T4 — Identity application: `GetMyProfileHandler`, `UpdateMyProfileHandler` (all violations at once), `UpdateMyLanguageHandler`; `UserProfileChanged` enqueued only when display name or language changed, in the same transaction (design §5.2; D-6; AC-2, AC-3, AC-5, AC-8).
      Done when: integration tests (Testcontainers) show saved values, no save on any violation, exactly one outbox row for a name or language change, none for a phone-only or same-value change, and the row validates against the schema without phone, gender or date of birth.
      Note (cloud session, no Docker): the integration tests were run by pointing the fixture at a local Postgres 16 (temporary local patch, not committed): `ProfileFlowTests`, `RegistrationFlowTests` and `MigrationTests` all passed (29/29). They run unchanged on Testcontainers in CI.
- [ ] T5 — Identity API: `MeEndpoints`, unknown-key detection (`email_read_only`), OpenIddict validation `UseLocalServer()` with audience `hivespace-api`, `me` policy that rejects the `hs.idp` cookie (design §3.1, §5.2; D-5; AC-1..AC-5, AC-9, AC-10).
      Done when: contract tests for `profile.feature` pass in vi and en, and tests show 401 for no token, a tampered token and a wrong-audience token, and that a spoofed `X-User-Id` does not change the user.
- [ ] T6 — Gateway: route `me` (authenticated), `POST /bff/session/refresh` with CSRF check, `SessionTokens.ForceRefreshAsync` reusing single-flight refresh, shared `/bff/user` body builder (design §3.2, §5.3; D-4; AC-7, AC-9).
      Done when: gateway tests show 401 on `/v1/me/profile` without a session, 403 without the CSRF header, and a refresh call returning the new `name`/`lang` and rewriting the cookie; two parallel refresh calls spend the refresh token once.
- [ ] T7 — Notification: `contacts` handles `UserProfileChanged` (schema-validated, upsert email, display name, language) (design §5.4; AC-8).
      Done when: `uv run pytest && uv run mypy && uv run lint-imports` pass, including a duplicate-delivery test (one update) and a test that the email feature then picks the template in the contact's new language.
- [ ] T8 — Authorization suite: add `/v1/me/profile` cases (unauthenticated at the gateway, direct call without a valid token, spoofed identity header) to the shared suite (AC-9; dev-standards §9.2).
      Done when: the suite runs in CI with these cases green.
- [ ] T9 — UI integration (after ui-design): storefront `features/profile/` (`api.ts`, `ProfilePage.vue` at `/profile`, `requiresAuth` redirect to `/bff/login?returnUrl=/profile`), session store `refresh()`, account-menu "Profile" item, language switcher saving to the profile when signed in, profile language applied on session load (design §5.5; D-7; AC-1, AC-6).
      Done when: the page renders and conforms to its ui-design spec in default, loading, validation-error and save-error states; component tests cover signed-in versus visitor language switching; `pnpm turbo run lint typecheck test build` passes including Sheriff and vi/en parity.
- [ ] T10 — E2E for UC-027: Playwright signs in, edits the profile (including an invalid date shown on the field), switches to English, reloads (still English), signs in from a fresh context (still English), and checks the header name updated without waiting (AC-6, AC-7).
      Done when: the `tests/e2e` spec passes against the local AppHost stack.
- [ ] T11 — Verify: AC-1..AC-12 each map to a passing test or recorded demonstration; `dotnet build/test`, Notification and web checks, `dotnet format`, boundary rules, Spectral and buf green; `docs/rtm.md` Test ref cells are left to acceptance verification.
      Done when: the commands in AGENTS.md all pass.
