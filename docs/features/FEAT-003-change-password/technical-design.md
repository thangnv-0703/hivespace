# Technical Design: FEAT-003 — Change password

> Feature from: docs/implementation-plan.md · Epic: E1 Identity & Access
> Implements: FR-AUTH-024, FR-AUTH-025, FR-AUTH-003 (password change part), FR-NOTIF-014, NFR-SEC-015
> Realizes: UC-026 (main flow, exceptions 2a and 2b; alternate 1a is FEAT-005) · Screens: SCR-STF-018 (designed by ui-design)
> Depends on: FEAT-001 (credential, argon2id hasher, OpenIddict tokens, gateway BFF, Notification emails), FEAT-002 (`/v1/me` route, bearer validation in Identity, `POST /bff/session/refresh`)
> Status: Verified · Date: 2026-11-22
> Diagram: [design-flow.html](design-flow.html) (interactive flow diagram, html-flow-diagram)

## 1. Intent
A signed-in user changes their password by giving the current one and a new one that meets the policy. Every other session of the account is signed out, the current session keeps working, and the user gets a security email. This is the first feature that revokes sessions on purpose. It proves that a password change really cuts off stolen refresh tokens (NFR-SEC-015), which matters before Google sign-in (FEAT-004) adds a second way in.

## 2. Codebase context
Surveyed on 2026-11-22, on `main` after FEAT-002 (`cf8548e`) and ADR-0027 (`eb42022`). The design conforms to the following.

- **Identity domain:** `Credential` (`Identity.Domain/Credential.cs`) is `{UserId, PasswordHash, UpdatedAt}` with only `Create`. `PasswordPolicy.Evaluate(password)` returns every unmet rule but always with field `password` (8–128 chars, a letter, a digit; codes `identity.password_too_short|too_long|needs_letter|needs_digit`).
- **Application:** handlers return `Result<T>`; ports `IUserRepository` (`FindCredentialAsync`, `SaveChangesAsync` commits everything, outbox included), `IPasswordHasher` (`Hash`, `Verify(hash?, password)`), `IEventOutbox`, `IClock`.
- **Api:** `MeEndpoints` maps `/v1/me/*` with policy `me` (OpenIddict validation scheme only, never the `hs.idp` cookie; FEAT-002 D-5). The user is `sub` of the bearer token. `UsersEndpoints.ToPrincipal` builds the `hs.idp` cookie (7 days) with `sub, email, email_verified, name, lang, role`; `ConnectEndpoints.AuthorizeAsync` signs the user in silently when that cookie is valid.
- **OpenIddict 7.9** with EF Core stores in `IdentityDbContext` (`UseOpenIddict()`): code flow with implicit consent, so each sign-in creates an ad-hoc **authorization**; access, ID and refresh tokens of that sign-in carry its id (`oi_au_id`). Refresh tokens rotate with zero reuse leeway and do not slide. The storefront and the seller centre are two clients (ADR-0027), so one browser signed in to both has two authorizations.
- **Gateway:** route `/v1/me/{**rest}` → identity (`authenticated`) already exists, CSRF header `X-HS-CSRF: 1` on non-GET calls. It strips only its own `hs.bff*` cookies, so `hs.idp` reaches Identity and Identity's `Set-Cookie` comes back. `SessionTokens` clears the session when a refresh fails; `POST /bff/session/refresh` forces one (FEAT-002 §3.2). **No gateway change is needed.**
- **Schema:** last migration `20261120171033_AddUserProfile`. `credentials(user_id pk, password_hash, updated_at)`; OpenIddict tables `OpenIddictAuthorizations` and `OpenIddictTokens` have a `Status` column.
- **Notification:** `features/emails/service.py` handles only the verification template (`EMAIL_EVENTS = {UserRegistered, VerificationEmailRequested}`, one `TEMPLATE`). `email_records` is unique on `(event_id, template)`. The dispatcher sends after commit.
- **Web:** `features/profile/` (profile page, `api.ts`, `ProfileSession` port with `refresh()` and `markSignedOut()`), `features/auth/PasswordField.vue` and `validation.ts` (client password rules). Features may not import each other (Sheriff).
- **Divergences recorded** (reality wins):
  1. The plan row says `POST /v1/me/password`; the project rule (dev-standards §6 rule 7) makes updates `PUT`, so it is `PUT /v1/me/password` (D-1).
  2. FR-AUTH-024 says revoke the user's *other* refresh tokens; NFR-SEC-015 says every refresh token issued *before* the change is rejected, which includes the current session's. Both hold with D-3.

## 3. API contracts

Conventions inherited from FEAT-001 and FEAT-002: JSON, camelCase, shared `Error` schema with `details[{field, code, message}]`, `Accept-Language` sets the message language, `X-HS-CSRF: 1` through the gateway, the user is always the token's `sub`.

### 3.1 Identity REST (existing gateway route `/v1/me/{**rest}`, policy `authenticated`)

| Endpoint | Auth | Request | Success | Errors |
| :-- | :-- | :-- | :-- | :-- |
| `PUT /v1/me/password` (`changeMyPassword`) | bearer | `PasswordChange` | `204`, and `Set-Cookie: hs.idp` when the request carried a valid `hs.idp` for the same user (D-4) | `400 common.validation_failed`, `409 identity.password_not_set`, `401 common.unauthenticated` |

`PasswordChange`: `{currentPassword: string, newPassword: string}`, both required, `additionalProperties: false`, both `format: password` (never logged, never echoed).

Validation (all unmet rules returned together, one `details` entry each; nothing changes when any rule fails):

| Rule | `details` entry | Traces to |
| :-- | :-- | :-- |
| `currentPassword` missing or empty | `{currentPassword, identity.current_password_required}` | UC-026 1 |
| `currentPassword` does not verify against the stored argon2id hash | `{currentPassword, identity.current_password_incorrect}` | FR-AUTH-025, UC-026 2a |
| `newPassword` breaks the policy | one entry per unmet rule, field `newPassword`, existing codes `identity.password_too_short`, `identity.password_too_long`, `identity.password_needs_letter`, `identity.password_needs_digit` | FR-AUTH-003, NFR-SEC-003, UC-026 2b |

Other outcomes:
- **No password on the account** (a Google-created account, which FEAT-004 introduces) → `409 identity.password_not_set`, checked before the rules above. Setting a first password is FR-AUTH-027 (FEAT-005, UC-026 1a).
- **Success** (FR-AUTH-024, NFR-SEC-002, NFR-SEC-015), in **one transaction**:
  1. the credential gets a new argon2id hash and `updated_at = now`;
  2. every OpenIddict authorization of the user **except the current one** (the `oi_au_id` of the bearer token) is set to `revoked`, with all its tokens;
  3. `PasswordChanged/v1` is written to the outbox.
  Then, outside the transaction, the current browser's `hs.idp` cookie is reissued with the new password stamp (D-4).
- After a 204 the SPA calls `POST /bff/session/refresh` (existing, FEAT-002 §3.2). That rotates the current session's refresh token, so the one issued before the change is spent and rejected from then on (D-3).
- **Retries:** the same request sent again after a success gets `identity.current_password_incorrect`, because the current password has changed. The SPA never retries this call by itself.
- A new password equal to the current one is accepted: no requirement forbids it.

### 3.2 Changes to the Identity session cookie and the authorize endpoint (no new endpoint)
- `hs.idp` gains claim `pwd_at`: the credential's `updated_at` as Unix milliseconds, set at sign-in (`POST /v1/users/sign-in`) and when it is reissued by §3.1.
  - FEAT-005 adds `auth_time` (Unix seconds of the interactive sign-in) and `amr` (`pwd` or `google`) to `hs.idp`; every reissue copies both unchanged (FEAT-005 §3.6).
- `/connect/authorize` treats an `hs.idp` cookie as **no session** (and clears it) when the user has a credential and the cookie's `pwd_at` is missing or differs. The browser then goes to the sign-in screen as it does today (D-4). The redirect and error behaviour are otherwise unchanged.
  - **Amended by FEAT-004 (D-5):** the rule is now an exact match. `pwd_at` must equal the credential's stamp, and a cookie that carries `pwd_at` for a user with **no** credential (its password was removed when an unverified account was linked to Google) is also treated as no session. A cookie without `pwd_at` signs in only for an account without a password.

### 3.3 Event `PasswordChanged/v1` (producer `identity`, consumer Notification)
`{userId: uuid, email: string ≤254, displayName: string ≤80, language: "vi"|"en", changedAt: date-time}`, all required, `additionalProperties: false`. No password, hash or token. The architecture event table (§5.4) already lists `PasswordChanged`.

### 3.4 OpenAPI, errors and client
- `contracts/openapi/identity.yaml`: `PUT /v1/me/password` with `security: bearer`, schema `PasswordChange`, responses 204, 400, 401, 409 (shared `Error`).
- `contracts/errors/error-codes.yaml` (+ vi and en messages): `identity.current_password_required` 400, `identity.current_password_incorrect` 400, `identity.password_not_set` 409. The first two appear only as `details` codes under `common.validation_failed`.
- `contracts/events/PasswordChanged/v1.schema.json`.
- Regenerate `web/packages/api-client`.

## 4. Schema changes

**None.**
- Identity: `credentials.updated_at` already exists and becomes the password stamp. Revocation uses OpenIddict's own `Status` columns. The last migration stays `20261120171033_AddUserProfile`.
- Notification: `email_records.template varchar(64)` already fits the new template name `password_changed`, and the `(event_id, template)` unique constraint already makes the email idempotent.

Entities touched: Credential and the refresh token store (Identity, architecture §5.2). No new entity, no escalation.

## 5. Component design

### 5.1 Flow

```mermaid
sequenceDiagram
    actor U as "Browser (session A)"
    participant GW as "Gateway/BFF"
    participant ID as "Identity"
    participant DB as "identitydb"
    participant SB as "Service Bus"
    participant NT as "Notification"

    U->>GW: "PUT /v1/me/password (X-HS-CSRF, hs.bff + hs.idp cookies)"
    GW->>ID: "relay with Bearer access token (oi_au_id = A)"
    ID->>ID: "verify current password, check policy"
    ID->>DB: "one tx: new hash, revoke authorizations not A, outbox PasswordChanged"
    ID-->>U: "204 + Set-Cookie hs.idp (new pwd_at)"
    U->>GW: "POST /bff/session/refresh"
    GW->>ID: "POST /connect/token (A's old refresh token)"
    ID-->>GW: "new tokens; old one now redeemed"
    GW-->>U: "200 /bff/user body"
    ID->>SB: "relay PasswordChanged"
    SB->>NT: "deliver"
    NT->>NT: "inbox, email record, send password-changed email"
```

Session B (another browser or the seller centre) keeps its access token until it expires (at most 15 minutes, FR-AUTH-015 note). Its next refresh gets `invalid_grant`, the gateway clears its cookie, and its `hs.idp` no longer signs it in silently.

### 5.2 Identity
- **Domain**
  - `Credential.ChangePassword(string newHash, DateTimeOffset now)` sets the hash and `UpdatedAt`.
  - `Credential.Stamp` is `UpdatedAt.ToUnixTimeMilliseconds()`. Milliseconds, because Postgres keeps microseconds and .NET keeps 100 ns ticks, so a full-precision value would differ after a round trip.
  - `PasswordPolicy.Evaluate(string? password, string field = "password")`, so the change reports `newPassword`. Registration is unchanged.
- **Application**
  - `ChangeMyPasswordCommand(Guid UserId, string? CurrentAuthorizationId, string? CurrentPassword, string? NewPassword, string CorrelationId)`.
  - `ChangeMyPasswordHandler`:
    1. Load the user and the credential. No credential → `identity.password_not_set`.
    2. Collect violations: current password required or incorrect (`hasher.Verify`), plus the policy rules for `newPassword`. Any violation → `common.validation_failed`, nothing written.
    3. Otherwise `ChangePassword(hasher.Hash(new), now)`, `ISessionRevoker.RevokeOtherSessions(userId, currentAuthorizationId)`, enqueue `PasswordChanged`, and one `SaveChangesAsync`.
    4. Return the new stamp so the Api can reissue `hs.idp`.
  - New port `ISessionRevoker` with `Task RevokeOtherSessionsAsync(Guid userId, string? keepAuthorizationId, CancellationToken ct)`. Its contract: it only stages changes in the same unit of work, and `IUserRepository.SaveChangesAsync` commits them.
- **Infrastructure:** `OpenIddictSessionRevoker` (in `Identity.Infrastructure`, which already references `OpenIddict.EntityFrameworkCore`).
  - It loads the user's `OpenIddictEntityFrameworkCoreAuthorization` rows (`Subject == userId`, `Status == valid`, `Id != keep`) through the shared `IdentityDbContext` and sets `Status = revoked`.
  - It also sets `Status = revoked` on their tokens whose status is `valid` or `inactive`.
  - These are tracked changes on the same context, so they commit atomically with the credential and the outbox row (D-2). A user has a handful of authorizations, so loading them is cheap.
  - When `keep` is null (a token without `oi_au_id`), every authorization is revoked. That is safe: the current session then signs out at its next refresh.
- **Api**
  - `MeEndpoints` adds `PUT /v1/me/password`. The body is a typed record `ChangePasswordRequest(string? CurrentPassword, string? NewPassword)`. The current authorization id is read from claim `oi_au_id` of the validated principal. On success: `http.AuthenticateAsync(hs.idp)`; when it succeeds and its `sub` equals the user, sign in `hs.idp` again with `ToPrincipal(user, stamp)`. Return 204.
  - `UsersEndpoints.ToPrincipal(User, long? passwordStamp)` adds `pwd_at` when a stamp is given. The sign-in endpoint passes the credential's stamp. `SignInOutcome` carries the credential stamp so the endpoint does not read it again.
  - `ConnectEndpoints.AuthorizeAsync`: after it finds the user, it loads the credential. When one exists and `pwd_at` ≠ `credential.Stamp`, it calls `SignOutAsync(hs.idp)` and redirects to `/signin?returnUrl=…`, the same path as having no session.
- **Logging:** the request body of this endpoint is never logged. Neither password appears in any log, trace attribute or event (NFR-SEC-002).

### 5.3 Notification
- `features/emails/service.py` changes from one template to a table keyed by event type: `UserRegistered` and `VerificationEmailRequested` → `verification` (with link), `PasswordChanged` → `password_changed` (no link). Each entry gives the subjects (vi, en), the template base name and a context builder. `prepare()` uses the entry's template for the `email_records` row, so the idempotency key stays `(event_id, template)`.
- The `PasswordChanged` payload is validated with a pydantic model `PasswordChanged` that mirrors the schema, plus a unit test that keeps the two in step (the FEAT-002 pattern).
- Templates `password_changed.{vi,en}.{txt,html}`: greeting with the display name; "your HiveSpace password was changed at {time}", with `changedAt` shown in Asia/Ho_Chi_Minh (NFR-L10N-002) as `HH:mm dd/MM/yyyy`; "other devices were signed out"; "if this was not you, contact HiveSpace support". No link and no token.
- The dispatcher validates `PasswordChanged` before any write, as it does for `UserProfileChanged`. The contacts projection does not consume this event: it carries no new contact data.

### 5.4 Gateway
No change. The existing `me` route, the CSRF check and `POST /bff/session/refresh` cover the flow.

### 5.5 Web (storefront)
- **`features/profile/`** owns account settings:
  - `api.ts` adds `changePassword(body)`, which calls the generated `changeMyPassword`.
  - `ChangePasswordPage.vue` at route `/profile/password` (`requiresAuth`), SCR-STF-018. Layout, states and entry points (profile page link, account menu) come from ui-design.
- **Behaviour**
  - Client checks show the policy hints before submit. The server stays the authority, and its `details` map to the two fields.
  - On 204: call `ProfileSession.refresh()`, clear both fields, and show a toast that the password was changed and other devices were signed out.
  - On 401: `markSignedOut()`.
  - On 409 `identity.password_not_set`: show the message. The set-password form arrives with FEAT-005.
  - The submit button is disabled while a request is in flight, so there are no double submits (§3.1 retries).
- **Shared input:** `PasswordField.vue` and the client password rules live in `features/auth`, and features cannot import each other. Implementation moves them to `web/packages/ui` (the field) and `web/packages/core` (the rules) and has both features import them from there. They stay one copy: no duplicated rules.

## 6. Acceptance criteria

| # | Criterion | Cites |
| :-- | :-- | :-- |
| AC-1 | Given a signed-in user with password `hello1234`, `PUT /v1/me/password {currentPassword: hello1234, newPassword: world5678}` returns 204. Afterwards, sign-in with `world5678` succeeds and with `hello1234` fails with `identity.invalid_credentials`. The stored hash is argon2id and is not the previous hash. | FR-AUTH-024, NFR-SEC-002, UC-026 1–3 |
| AC-2 | Given the same user also signed in as session B (another code flow, for example the seller-centre client), after A changes the password, B's refresh token gets `invalid_grant` from `/connect/token`, and B's authorization and tokens have status `revoked`. A's authorization stays valid. | FR-AUTH-024, NFR-SEC-015 |
| AC-3 | After the change, A's forced session refresh succeeds and returns new tokens. A then presenting its pre-change refresh token again gets `invalid_grant`. No refresh token issued before the change can still be redeemed. | NFR-SEC-015 |
| AC-4 | A wrong current password returns 400 `common.validation_failed` with `{currentPassword, identity.current_password_incorrect}`. An empty one returns `identity.current_password_required`. In both cases the hash, the authorizations and the outbox are unchanged. | FR-AUTH-025, UC-026 2a |
| AC-5 | A new password `abc` returns 400 with `newPassword` entries `identity.password_too_short` and `identity.password_needs_digit` in one response; `abcdefgh` returns `password_needs_digit`; 129 characters returns `password_too_long`. With both a wrong current password and a bad new one, all entries come back together. Nothing changes. | FR-AUTH-003, NFR-SEC-003, UC-026 2b |
| AC-6 | A successful change writes exactly one `PasswordChanged` outbox row in the same transaction. It validates against `PasswordChanged/v1` and holds no password or hash. A failed change writes none. Notification sends one password-changed email in the user's language (vi and en checked), with the time in Asia/Ho_Chi_Minh. Delivering the event twice sends one email. | FR-NOTIF-014, NFR-REL-005, FR-LOC-005 |
| AC-7 | After the change, `/connect/authorize` with an `hs.idp` cookie issued before the change, in another browser, redirects to `/signin` and clears the cookie. The same request with the cookie reissued in the 204 response signs in silently. | FR-AUTH-024, SCR-STF-018 ("signs out other sessions") |
| AC-8 | For an account without a credential, the call returns 409 `identity.password_not_set` and writes nothing. | FR-AUTH-026 context, UC-026 1a deferred to FEAT-005 |
| AC-9 | With no session the call returns 401 at the gateway, and 403 without `X-HS-CSRF`. Called directly with no token or a tampered token, Identity returns 401. A spoofed `X-User-Id` never changes whose password is changed. | FR-AUTH-015, FR-AUTH-016 |
| AC-10 | The three new codes and the reused password codes come back in the request language (vi, en). Every code has both messages. Neither password appears in logs or traces of a test run. | FR-LOC-004, NFR-SEC-002 |
| AC-11 | On SCR-STF-018, a successful change keeps the user signed in (the header name stays, a following `/v1/me/profile` call returns 200), clears the form and shows the success toast. The server's field errors appear on the matching field. The button cannot submit twice. | UC-026, FR-AUTH-024 |
| AC-12 | Gherkin `contracts/features/identity/change-password.feature` covers success and each designed error and passes on the cucumber-js contract runner. | dev-standards §9.1, ADR-0025 |

## 7. Decisions
- **D-1 `PUT /v1/me/password`, not the plan's `POST`.**
  - Driver: dev-standards §6 rule 7. The credential is a write-only sub-resource of `me`, replaced as a whole.
  - Consequence: repeating the call is not idempotent (the second call fails on the current password). The UI never retries it by itself.
  - Rejected: `POST` (would bring in a second style for updates).
- **D-2 Revoke through OpenIddict's own tables, in Identity's transaction.**
  - Driver: FR-AUTH-024 and NFR-SEC-015 must hold together with the hash change. OpenIddict already rejects tokens whose authorization or token entry is not `valid`.
  - Rejected: the manager API `IOpenIddictAuthorizationManager.TryRevokeAsync`, because each call saves on its own, so a crash could leave a new password with live old sessions. Also rejected: a deny-list table or a per-user token version checked on every refresh (a new structure for what the store already does).
- **D-3 The current session survives through rotation, not exemption.**
  - Driver: FR-AUTH-024 keeps the current session, and NFR-SEC-015 rejects every pre-change refresh token. Identity keeps the current authorization, and the forced `POST /bff/session/refresh` after the 204 spends the current pre-change refresh token right away. Zero reuse leeway, so a replay is rejected and OpenIddict then revokes that chain.
  - Residual: if that refresh request is lost, the current session's pre-change token stays redeemable until the gateway's next refresh. It is held only in this session's own encrypted `hs.bff` cookie, so no third party gains from it.
  - Rejected: revoking the current authorization as well and sending the browser through a silent re-login (a full-page redirect on every password change for the same guarantee).
- **D-4 `hs.idp` carries a password stamp (`pwd_at`).**
  - Driver: SCR-STF-018 "signs out other sessions". Without the stamp, another browser's `hs.idp` (7 days) would sign it back in silently at `/connect/authorize` right after its refresh token was revoked, which would defeat FR-AUTH-024.
  - Uses the existing `credentials.updated_at`, so no migration. The current browser's cookie is reissued in the 204, so the storefront and seller-centre sign-in there keeps working.
  - Consequence: `hs.idp` cookies issued before this feature have no `pwd_at` and stop signing in silently once, after deployment.
- **D-5 `PasswordChanged` carries contact data, and Notification renders from the event.** Driver: the same pattern as `UserRegistered` (FEAT-001), and NFR-LEG-001 (only what the email needs). Rejected: reading Notification's `contacts` projection, which might not have caught up.
- **D-6 Shared password field and rules move to `packages/`.** Driver: the Sheriff boundary (features do not import each other) and one copy of the client policy hints. A local refactor of FEAT-001 code with no contract change. FEAT-001's tests move with it.

## 8. Escalations & open items
- **Plan correction suggested (implementation-planning owns it):** the FEAT-003 row says `POST /v1/me/password`; it should read `PUT /v1/me/password` (D-1).
- **Deferred, named:**
  - Wrong current passwords counting toward the lockout of FR-AUTH-011 (FR-AUTH-025 note) and a rate limit on this endpoint are FEAT-005 (Should). Until then the endpoint has no attempt limit, and it needs a valid session.
  - Setting a first password for a Google-created account (UC-026 1a, FR-AUTH-027, re-authentication OQ-33) is FEAT-005. This feature returns `identity.password_not_set`.
  - Notification email retries are FEAT-032; a failed send is recorded as `failed`, as for verification.
- **For ui-design:** SCR-STF-018 needs its entry points (profile page and/or account menu), the success toast copy, and the 409 state.
- **Breaking-ish change to FEAT-001 behaviour:** existing `hs.idp` cookies without `pwd_at` no longer sign in silently (D-4); `PasswordField`/password rules move packages (D-6). FEAT-001 tests that assert `hs.idp` claims or import `PasswordField` from `features/auth` are updated with this feature.
- **No architecture escalation:** Credential and refresh tokens are Identity's (architecture §5.2), and `PasswordChanged` is already in the event table.
