# Tasks: FEAT-004 — Google sign-in with auto-link

> Executes: docs/features/FEAT-004-google-sign-in/technical-design.md
> Status: pending | in-progress | done per task · Last updated: 2026-11-23
> Prerequisites: FEAT-003 merged to `main` (the `pwd_at` stamp, `ISessionRevoker`, `password_not_set`). No gateway change. Decision D-5 (technical-design §7) is confirmed by the user (2026-11-23): delete the password of an unverified account at link time and say so in the notice email; setting a new password is FEAT-005.

The screen change (Google button, divider, error alerts on SCR-STF-012) is owned by ui-design's output (spec in [ui-design.md](ui-design.md), visual mockups in [mockups/](mockups/), registry in `docs/design-manifest.json`); T8 only wires it. UC-025 is a critical flow named in architecture §8.6 ("one Google sign-in run against a fake IdP"), so T9 is the E2E task.

- [x] T1 — Contracts first:
      - error codes `identity.external_login_denied`, `identity.external_email_unverified`, `identity.external_login_failed`, `identity.external_login_unavailable` with vi/en messages;
      - `contracts/events/ExternalLoginLinked/v1.schema.json`;
      - `contracts/features/identity/google-sign-in.feature` (start redirect shape, unavailable redirect, `returnUrl` handling, unknown provider 404, no-password sign-in);
      - a note in `contracts/openapi/identity.yaml` `info.description` that `/connect/external/*` are browser endpoints like `/connect/*`.
      (design §3.1, §3.3, §3.4; AC-15)
      Done when: Spectral shows 0 errors, the vi/en key-parity check passes, the generated client is unchanged, and the new feature file is discovered (red) by `tests/contract`.
- [x] T2 — Schema and domain:
      - `ExternalLogin` entity and `IdentityDbContext` mapping (table `external_logins`, unique `(provider, subject)`, unique `(user_id, provider)`);
      - migration `AddExternalLogins`;
      - `User.RegisterExternal`.
      (design §4, §5.2; FR-AUTH-020)
      Done when: the migration applies clean up and down on a database at `20261120171033_AddUserProfile` (`MigrationTests`). Unit tests show `RegisterExternal` gives a verified Buyer and that `Register` is unchanged.
- [x] T3 — Identity application: `ExternalSignInHandler` and the new repository members (`FindExternalLoginAsync`, `FindExternalLoginByUserAsync`, `Add(User)`, `AddExternalLogin`, `RemoveCredential`), `ExternalLoginConflictException` mapping in `EfUserRepository`, one retry on conflict.
      (design §5.2; D-3, D-4, D-5; FR-AUTH-020..022; AC-1..AC-6 handler part, AC-9 sign-in part, AC-11)
      Done when: Testcontainers integration tests pass for each of these:
      - a new verified identity creates a verified Buyer with no credential and no token, one link and one `ExternalLoginLinked` (`accountCreated: true`) that validates against the schema and holds no secret (AC-1);
      - a verified-email account links to the same user, keeps its password, event flags false (AC-2);
      - an unverified-email account is verified, loses its credential, has every authorization and token revoked and its pending tokens invalidated, and the event says `passwordRemoved: true` (AC-3);
      - a known `(provider, subject)` signs in with no write and no event, also after a Google email change (AC-4);
      - unverified or missing email, empty subject, a user already linked to another subject each fail with the right code and write nothing (AC-5, AC-6);
      - two concurrent first sign-ins give one user and one link (AC-11);
      - the sign-in handler still returns `identity.invalid_credentials` for a credential-less user, same result as an unknown email (AC-9).
- [ ] T4 — Fake IdP (`tools/fake-idp`, added to `HiveSpace.slnx`): discovery, JWKS, `/authorize`, `/token`, and the `/__fake/next` control endpoint with the outcomes in design §5.6; PKCE, client secret and `redirect_uri` checks.
      (design §5.6; D-7; dev-standards §10)
      Done when: its own tests drive one full code flow for `ok`, and show that each failure outcome produces the response the integration tests expect (a bad signature really fails signature validation, and so on). It builds with no warnings.
- [ ] T5 — Identity API: `hs.ext` cookie, the `google` OpenID Connect scheme and registry (`ExternalLoginProviders`), `AddExternalLogins` configuration, `start` and `complete` endpoints, `ExternalFailure.Classify`, `OnRedirectToIdentityProvider` / `OnMessageReceived` / `OnRemoteFailure`, the exact `pwd_at` match in `ConnectEndpoints.PasswordStampMatchesAsync`, no secret in logs.
      (design §3.1, §3.2, §5.2; D-1, D-2, D-5, D-6, D-8; NFR-SEC-014, EIR-SW-006; AC-3 cookie part, AC-5..AC-10, AC-16)
      Done when: integration tests with `IdentityApiFactory` and the fake IdP pass for:
      - the full happy path for a new and a linking account, ending in a successful `/connect/authorize` (AC-1, AC-2);
      - the exact request parameters on both Google calls, including `redirect_uri` equality and no stored token or leftover `hs.ext` (AC-7);
      - every failure outcome of AC-5 and AC-6 ending at `/signin?error=<code>` with `returnUrl` kept and no cookie;
      - Google down at `start`, at discovery and at the token call, with password sign-in still working and recovery within 30 s (AC-8);
      - the old `hs.idp` of an account whose credential was removed is sent to `/signin` (AC-3);
      - `returnUrl` and unknown provider cases (AC-10);
      - a log-capture test with no code, token, client secret or email in the output (AC-7, AC-16).
      `ExternalFailure.Classify` has unit tests for each branch. The Identity architecture tests stay green. FEAT-003's `hs.idp` tests pass or are updated for the exact-match rule.
- [ ] T6 — Notification:
      - `ExternalLoginLinked` entry in `KINDS` with the `applies` check, and the contacts projection event list;
      - pydantic `ExternalLoginLinked` model validated by the dispatcher before any write;
      - `external_login_linked.{vi,en}.{txt,html}`.
      (design §5.3; D-3; FR-NOTIF-015; AC-12)
      Done when: `uv run pytest && uv run mypy && uv run lint-imports` pass, including:
      - one event with `accountCreated: false` gives one `email_records` row and one message, in vi and en, with the Vietnam time and the extra paragraph only when `passwordRemoved`;
      - `accountCreated: true` sends nothing but upserts the contact;
      - a duplicate delivery sends once;
      - a schema-sync unit test;
      - the verification and password-changed email tests stay green.
- [ ] T7 — Local wiring: AppHost starts `tools/fake-idp` on a fixed local port and sets `Identity__Google__ClientId`, `ClientSecret` (generated into `.local/dev-secrets` like the OIDC client secrets) and `Authority`, unless `.local/dev-secrets/google.json` provides a real client. Update `docs/repo-structure.md` for `tools/fake-idp/` and the README note on running with real Google (OQ-32).
      (design §3.6, §5.6; D-7)
      Done when: `dotnet run --project apphost/HiveSpace.AppHost` starts the fake IdP and Identity with Google configured, and `GET /connect/external/google/start` through the gateway redirects to the fake IdP. `tests/system` still passes.
- [ ] T8 — UI integration (after ui-design):
      - one shared `GoogleButton` (with the `GoogleIcon`) and the "or" divider in `SignInPage.vue` and `RegisterPage.vue` (navigation to `start` with `returnUrl`);
      - `?error=` alerts on the sign-in page for the four codes (unknown values ignored), the unavailable message pointing to email and password;
      - vi/en keys.
      (design §5.5; FR-AUTH-022, FR-AUTH-023; AC-13)
      Done when: both pages render and conform to their ui-design specs (default, each Google alert and the unavailable state on Sign in; the Google button on Register). Component tests cover the navigation target on both pages, the code-to-message mapping and an unknown code. `pnpm turbo run lint typecheck test build` passes, including Sheriff and vi/en parity.
- [ ] T9 — E2E: extend the suite for UC-025 against the fake IdP: `tests/e2e/features/google-sign-in.feature` with a new visitor signing in and ending signed in (name in the header), and a visitor with a password account of the same email being linked. Steps set the outcome through `/__fake/next`.
      (design §6 AC-14; architecture §8.6)
      Done when: both scenarios pass with `pnpm test` in `tests/e2e` against the local AppHost stack, in the desktop and mobile-360 projects.
- [ ] T10 — Verify: AC-1..AC-16 each map to a passing test or a recorded demonstration (AC-8 and AC-14 also checked once by hand: stop the fake IdP, see the unavailable message and a working password sign-in). `dotnet build/test`, `dotnet format`, the Notification and web checks, boundary rules, Spectral, buf and `tests/contract` are green. `docs/rtm.md` Test ref cells are left to acceptance verification.
      Done when: the commands in AGENTS.md all pass.
