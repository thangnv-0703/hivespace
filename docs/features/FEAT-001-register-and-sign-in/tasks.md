# Tasks: FEAT-001 — Register, verify email, password sign-in and sign-out

> Executes: docs/features/FEAT-001-register-and-sign-in/technical-design.md
> Status: pending | in-progress | done per task · Last updated: 2026-11-18

Screens are owned by ui-design's output (SCR-STF-012/013/014, specs in [ui-design.md](ui-design.md), visual mockups in [mockups/](mockups/), registry in `docs/design-manifest.json`); T12 only wires them. T12 also builds the header account menu and the unverified banner from ui-design.md "Extras".

- [x] T1 — Contracts first: add `identity.*` codes with vi/en messages, `UserRegistered/v1` and `VerificationEmailRequested/v1` schemas, the four operations in `openapi/identity.yaml`, `gateway.yaml` for `/bff/*`, the shared `Accept-Language` parameter; regenerate `web/packages/api-client`; replace `features/identity/ping.feature` with register, verify, resend and sign-in features (design §3.5–§3.7).
      Done when: Spectral shows 0 errors, vi/en key-parity check passes, the client regenerates with no manual edits, and the features are discovered (red) by Reqnroll.
- [x] T2 — Shared errors: `ApiException`, `MessageCatalog` (embedded vi/en YAML, vi fallback), language resolver, `UseApiErrors` mapping to 4xx with localized messages (design §5.4; FR-LOC-004; AC-5).
      Done when: unit tests for AC-5 pass (every code in `error-codes.yaml` has both messages; status and language mapping correct; 500 catch-all kept).
- [x] T3 — Identity domain: `User`, `Credential`, `VerificationToken`, value objects, `PasswordPolicy` returning all unmet rules, roles (design §5.2; FR-AUTH-001..004, FR-ROLE-001).
      Done when: unit tests for AC-3, AC-4 (policy and field rules) and token expiry/use/invalidate rules pass; Architecture tests (Domain has no framework reference) stay green.
- [x] T4 — Identity persistence: `IdentityDbContext`, migration `0001_InitialIdentity` incl. OpenIddict and outbox/inbox tables, `Database:MigrateOnStartup`, AppHost `identitydb` + messaging references (design §4, §5.7).
      Done when: the migration applies clean up and down on Testcontainers Postgres and Identity starts under the AppHost with its database.
- [ ] T5 — Identity application and infrastructure: handlers `RegisterUser`, `VerifyEmail`, `ResendVerificationEmail`, `SignIn`; `Argon2idPasswordHasher`; token generator (SHA-256 at rest); outbox adapter; `OutboxCleanup` (design §5.2; D-3, D-6; AC-1, AC-2, AC-6, AC-8..AC-11, AC-13).
      Done when: unit and integration tests (Testcontainers) for those criteria pass, including the concurrent-registration race and the transactional outbox write.
- [ ] T6 — Identity API: `UsersEndpoints` (§3.1) replacing the ping stub, cookie scheme `hs.idp`, `returnUrl` validation (design §3.1; AC-1..AC-4, AC-13, AC-17).
      Done when: contract tests (Reqnroll register, verify, resend, sign-in features) pass for success and every designed error, in vi and en.
- [ ] T7 — OpenIddict server: ES256 and encryption keys from config, `/connect/authorize` (redirect to `/signin`), `/connect/token`, `/connect/revoke`, `/connect/logout`, discovery and JWKS, client seeding, claims (`sub`, `email`, `email_verified`, `name`, `role`, `lang`), 15 min / 7 day lifetimes with rotation (design §3.2; D-7; AC-9, AC-12, AC-14).
      Done when: an integration test runs code + PKCE → token → refresh (rotation: the old refresh token fails) → revoke, and asserts the claims and lifetimes.
- [ ] T8 — Gateway auth: cookie + OIDC client, Data Protection in Azurite, `/bff/login|callback|logout|user`, token refresh, JWKS validation of the access token, route policies, token relay replacing `dev-user`, CSRF middleware, `auth` rate-limit hook, new routes and SPA-fallback exclusions (design §3.3, §3.4, §5.3; D-4, D-5, D-9; AC-12, AC-14..AC-17).
      Done when: Gateway tests pass for AC-16 and AC-17 (missing, tampered, expired, wrong-signature token → 401; valid → headers relayed, inbound spoofed headers stripped; missing CSRF header → 403) and the `dev-user` branch no longer exists.
- [ ] T9 — Notification: Alembic `0002`, dispatcher by `eventType`, contacts feature, emails feature, `EmailSender` protocol with the SMTP provider (Mailpit), vi/en verification templates, `PUBLIC_BASE_URL` setting, no token in logs or tables (design §5.5, §4; FR-NOTIF-002, FR-LOC-005; AC-7, AC-8).
      Done when: `uv run pytest && uv run mypy && uv run lint-imports` pass, including a duplicate-delivery test (one email, one record) and a vi/en template test.
- [ ] T10 — Cross-service event proof: Identity outbox → Service Bus emulator → Notification → Mailpit, with the UserRegistered schema validated on both ends (AC-6, AC-7).
      Done when: a system or integration test registers a user and finds exactly one email in Mailpit with a working link, and one trace spans identity, broker and notification.
- [ ] T11 — Authorization suite seed: add the first cross-user and unauthenticated cases to the shared authorization test suite (gateway 401 on a protected test route).
      Done when: the suite runs in CI with the cases from AC-16.
- [ ] T12 — UI integration (after ui-design): storefront `features/auth/` (`api.ts`, pages, session store, header account menu, sign-out form POST), CSRF and `Accept-Language` headers in one client place, shared `AuthLayout` ported into `web/packages/ui` (with its grid shape) and used by the three pages (design §5.6; D-1).
      Done when: each screen renders and conforms to its ui-design spec in states default/error/loading; `pnpm turbo run lint typecheck test build` passes including Sheriff and the vi/en parity check.
- [ ] T13 — E2E for UC-001 (hero flow named in the architecture's Testing entry): extend the Playwright suite with register → Mailpit → verify → sign in → refresh → sign out, plus replay of the old cookie fails (design §6 AC-12, AC-15, AC-19).
      Done when: `tests/e2e` spec passes against the local AppHost stack (needs a healthy Docker Desktop).
- [ ] T14 — Verify: all acceptance criteria AC-1..AC-19 demonstrably pass; `dotnet build/test`, Notification and web checks, `dotnet format`, boundary rules, Spectral and buf green; scaffold-notes §8 stub table updated (dev-user and ping removed).
      Done when: the commands in AGENTS.md all pass and each AC maps to a passing test or recorded demonstration.
