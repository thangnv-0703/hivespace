# UI Design: FEAT-005 — Account hardening: lockout, set a password, rate limits, audit

> Feature from: docs/implementation-plan.md · Pairs with: technical-design.md
> Screens: SCR-STF-012 (extended), SCR-STF-018 (extended: set mode), SCR-STF-016 (extended: Password card) · Mode: per-feature · Status: Verified · Date: 2026-11-23
> Strategy: **code-native**. All three screens already have manifest entries (FEAT-001, FEAT-003, FEAT-002); this feature adds states and a mode. No design tool is connected, and the manifest policy is `code-native`.
> **Visual mockups (reference for implementation; the spec below wins on any difference):** committed copies in [mockups/](mockups/) and the live canvas https://claude.ai/artifact/SrevC22oLd23LX3H1ofje8 (private to the owner).
> - `Main.dc.html`: SCR-STF-018 set mode, default.
> - `SetPasswordMobile.dc.html`: SCR-STF-018 set mode at 390 px with the re-authentication prompt.
> - `SignInLocked.dc.html`: SCR-STF-012 after the locking attempt.
> - `SignInRetry.dc.html`: SCR-STF-012 at 390 px, an error with no `returnUrl`.
> - `HardeningAlerts.dc.html`: the other new alerts (rate limited with countdown, rate-limited Google callback, re-authentication notice on Sign in, locked on change password).
> - `ProfileSetPasswordCard.dc.html`: SCR-STF-016 Password card for an account without a password.
> - `canvas.json`: layout index.
>
> Light theme and English only. States not drawn: Vietnamese copy, dark mode, the page loading spinner, profile load error, submitting, set-password success toast, 409 switch to change mode.

## SCR-STF-012 — Sign in

*Only the delta from FEAT-001's [ui-design.md](../FEAT-001-register-and-sign-in/ui-design.md#scr-stf-012--sign-in) and FEAT-004's [ui-design.md](../FEAT-004-google-sign-in/ui-design.md#scr-stf-012--sign-in) is specified here. Layout, fields, the Google button and divider are unchanged.*

- **Strategy & source:** code-native (this section is the design for the new states).
- **Content & data mapping:**
  - `POST /v1/users/sign-in` → `423 identity.account_locked` → **locked** state; `429 common.rate_limited` (gateway, with `Retry-After`) → **rate limited** state (technical-design §3.2, §3.7).
  - `/signin?error=common.rate_limited` (the gateway's answer to a rate-limited Google callback) joins FEAT-004's `?error=` table.
  - `returnUrl` containing `max_age=` (a re-authentication request from SCR-STF-018, technical-design §3.6) → **re-authentication notice**.
  - `?error=` present and `returnUrl` absent → **retry** state.
- **Conformance:** pass. Only `Alert`, `Button` and existing tokens are used. No escalation.
- **Mockup:** `SignInLocked.dc.html`, `SignInRetry.dc.html`, `HardeningAlerts.dc.html`.

### locked
- Shown after a `423 identity.account_locked`, both on the attempt that locks the account and on any attempt while it is locked.
- One `Alert` in the existing alert slot above the Google button, tone **warning** (`TriangleAlert`, `role="status"`). Title "Account temporarily locked"; message from the API: "Too many failed attempts. This account is locked for 15 minutes. Try again later, or sign in with Google."
- The password field is cleared, as for any failed sign-in, and focus moves to the **Continue with Google** button, the one path that works now (technical-design D-2).
- The form stays enabled. Another submit gets the same answer; the page does not count down, because the client does not know when the lock started.

### rate limited
- Shown after a `429 common.rate_limited` on submit.
- Warning `Alert`, title "Too many attempts", message from the API: "Too many requests. Please wait a minute and try again."
- The **Sign in** button is disabled with a countdown label "Sign in (42 s)", using `useCooldown` from FEAT-001 with the `Retry-After` header seconds (60 when the header is missing). The countdown is announced through `aria-live="polite"` at most every 10 s, as on the resend control. When it ends, the label returns to "Sign in" and the alert stays until the next submit. Typed values are kept.
- The Google button stays enabled (a different limit).

### Google rate limited (`?error=common.rate_limited`)
A new row in FEAT-004's alert table, same slot and behaviour (dropped from the URL after reading):

| `?error=` code | Tone | Title (en) | Message (en) |
| :-- | :-- | :-- | :-- |
| `common.rate_limited` | warning | Too many attempts | Too many sign-in attempts from this network. Wait a minute, then try again. |

### re-authentication notice
- When `returnUrl` carries `max_age`, an info `Alert` (`Info`, `role="status"`) is shown above the Google button before anything is submitted: title "Confirm it's you", message "For your security, sign in again to continue." A Google-only account can only use the Google button, which is already the first control.
- Any other alert replaces it.

### retry
- When the page has `?error=` but no `returnUrl` (the gateway's rate-limit redirect cannot know it), the page does **not** start `/bff/login` at once (FEAT-001's starting rule), because the alert would never be seen.
- It shows the `AuthLayout` title "Sign in", the alert for the code, and one primary `Button` "Try again" (full width, size `lg`) that calls `goTo('/bff/login')`. No form and no Google button: without `returnUrl` neither would end in a storefront session.
- An unknown `error` value with no `returnUrl` keeps the old behaviour (start `/bff/login` at once).

- **Responsive notes:** unchanged; alerts wrap under their icon at 360 px.
- **Decisions:**
  - **D-A. Locked is a warning, not an error.** Driver: FEAT-004 D-B's rule (the tone follows who caused it); the lock may come from someone else's attempts and is temporary. Rejected: error tone (blames the user).
  - **D-B. Focus goes to Continue with Google when locked.** Driver: Google still works while the account is locked (technical-design D-2); the password field would only invite another rejected attempt.
  - **D-C. No lock countdown.** Driver: the API returns no unlock time (technical-design §3.1), and a guessed countdown could be wrong. Rejected: adding `lockedUntil` to the error (it would tell an attacker exactly when to resume).

## SCR-STF-018 — Change password

*The FEAT-003 [design](../FEAT-003-change-password/ui-design.md#scr-stf-018--change-password) stays the **change mode**. This feature adds a **set mode** for accounts without a password, a loading state while the mode is decided, and two errors in change mode.*

- **Strategy & source:** code-native (this section is the design for set mode and the new states).
- **Route:** `/profile/password`, unchanged. The page now reads `GET /v1/me/profile` once on entry and picks the mode from `hasPassword` (technical-design §3.5). The route and the account menu item keep their name; only the page content changes.
- **Composition (set mode):** the same frame as change mode.
  1. Back link "Profile".
  2. `h1` "Set a password"; subtitle "Your account doesn't have a password yet. Set one to sign in with your email and password as well as Google. Other devices will be signed out."
  3. One `Card` with the form:
     - hidden `username` input with the email (FEAT-003 rule);
     - **New password** \*: `PasswordField`, `autocomplete="new-password"`, with `PasswordRules` below;
     - **Confirm new password** \*: `PasswordField`, `autocomplete="new-password"`, client-side only (FEAT-003 D-B);
     - footer: primary `Button` "Set password", right-aligned from `sm`, full width below.
- **Content & data mapping:**
  - Set mode submit → `PUT /v1/me/password/initial {newPassword}` (`setMyPassword`, technical-design §3.4) with `Accept-Language`.
  - Change mode submit → `PUT /v1/me/password`, unchanged.
  - Error code → UI in the table under Cross-screen decisions.
- **Conformance:** pass. Same components as FEAT-003, plus the Google button from FEAT-004 inside an `Alert` action. `PasswordRules` stays under FEAT-001's pending E-2. No new escalation.
- **Mockup:** `Main.dc.html` (set mode), `SetPasswordMobile.dc.html` (re-authentication prompt), `HardeningAlerts.dc.html` (locked in change mode).

### loading
While the profile loads, the back link and a `Spinner` (24 px) show in place of heading and card (design.md §4.3). A load failure shows the error `Alert` with **Retry** (design.md loading/error convention) and no form, because the mode is unknown.

### default
- `hasPassword` false → set mode as composed above; `hasPassword` true → change mode exactly as FEAT-003.
- Set mode fields are empty, the rules muted, the button enabled.

### submitting
As FEAT-003: the submit button is `loading`, repeat submits blocked.

### reauthentication required
- After `403 identity.reauthentication_required` in set mode.
- An info `Alert` above the card (`role="status"`), title "Confirm it's you", message from the API: "For your security, confirm it's you with Google first." Below the message, in the alert's action slot, a secondary `Button` with the Google "G" mark: "Continue with Google". One line of help in `text-xs`: "You'll come back here and enter your new password again."
- The button calls `goTo('/bff/login?reauth=true&returnUrl=%2Fprofile%2Fpassword')` (technical-design §3.6). Focus moves to it.
- The typed values stay in the fields until the browser leaves; they are lost on the round trip (technical-design D-5 consequence).
- After coming back, the page is in set mode default; a second 403 shows the same alert.

### error
- **Set mode:**
  - Client checks as FEAT-003 for the new and confirm fields.
  - `400 common.validation_failed` → `PasswordRules` error state with the API messages, focus on New password.
  - `409 identity.password_already_set` → reload the profile, switch to change mode, and show an info `Alert` above the form with the API message ("Your account already has a password. Use change password instead.").
- **Change mode, new:**
  - `423 identity.account_locked` → warning `Alert` above the form, title "Account temporarily locked", API message. The current-password field is cleared; the others keep their values.
  - (A wrong current password that causes the lock arrives as this 423, not as the field error.)
- **Both modes:**
  - `429 common.rate_limited` → warning `Alert` above the footer with the API message; values kept; the submit button shows the `useCooldown` countdown from `Retry-After` as on Sign in.
  - Save failed and `401` as FEAT-003.

### success
- Set mode `204`: `ProfileSession.refresh()`, success `Toast` "Password set. You can now also sign in with your email and password.", then the page switches to change mode with empty fields and focus on the `h1` (now "Change password").
- Change mode success unchanged.

- **Responsive notes:** as FEAT-003. The alert's Google button is full width below `sm`.
- **Decisions:**
  - **D-D. One page, two modes, not a second route.** Driver: the account menu item, the back link and the profile card all point at `/profile/password`; the contract decides the mode (`hasPassword`). Rejected: `/profile/password/set` (a second page and route guard for a form that differs by one field).
  - **D-E. Re-authenticate on the first 403, not before the form.** Driver: the client cannot read `auth_time`, and a user who just signed in with Google should not be sent round again. Rejected: always sending the user through Google on page entry (an extra round trip most of the time).
  - **D-F. Set mode has no current-password field.** Driver: there is none (UC-026 1a); Google re-authentication replaces it.

## SCR-STF-016 — Profile (extended)

The FEAT-002 design and FEAT-003's Password card are unchanged except for this content switch.

### password card set
- When `GET /v1/me/profile` returns `hasPassword: false`, the Password card shows:
  - title "Password";
  - body "Your account doesn't have a password yet. Set one to also sign in with your email.";
  - secondary `Button` as `RouterLink`: "Set a password" → `/profile/password`.
- With `hasPassword: true` the card is FEAT-003's ("Change password").
- **Mockup:** `ProfileSetPasswordCard.dc.html`.

## Cross-screen decisions
- **Error code → UI** (every new code, technical-design §3.1):

| Code | Screen | As |
| :-- | :-- | :-- |
| `identity.account_locked` | SCR-STF-012, SCR-STF-018 change mode | warning `Alert` "Account temporarily locked"; focus to Google (012) or the current field cleared (018) |
| `identity.reauthentication_required` | SCR-STF-018 set mode | info `Alert` with "Continue with Google" |
| `identity.password_already_set` | SCR-STF-018 set mode | switch to change mode, info `Alert` |
| `common.rate_limited` | SCR-STF-012 (response and `?error=`), SCR-STF-018 | warning `Alert`, submit countdown from `Retry-After` |

- **Copy keys** (vi and en, identical key sets; API messages from `contracts/errors/messages.*.yaml` win, these are fallbacks and titles):

| Key | en | vi |
| :-- | :-- | :-- |
| `auth.errors.identity.account_locked.title` | Account temporarily locked | Tài khoản tạm thời bị khóa |
| `auth.errors.common.rate_limited.title` | Too many attempts | Quá nhiều lần thử |
| `auth.errors.common.rate_limited` (`?error=` message) | Too many sign-in attempts from this network. Wait a minute, then try again. | Có quá nhiều lần đăng nhập từ mạng này. Hãy chờ một phút rồi thử lại. |
| `auth.signIn.submitCooldown` | Sign in ({seconds} s) | Đăng nhập ({seconds} giây) |
| `auth.signIn.reauth.title` | Confirm it's you | Xác nhận đó là bạn |
| `auth.signIn.reauth.message` | For your security, sign in again to continue. | Để bảo mật, hãy đăng nhập lại để tiếp tục. |
| `auth.signIn.retry` | Try again | Thử lại |
| `account.password.set.title` | Set a password | Đặt mật khẩu |
| `account.password.set.subtitle` | Your account doesn't have a password yet. Set one to sign in with your email and password as well as Google. Other devices will be signed out. | Tài khoản của bạn chưa có mật khẩu. Hãy đặt mật khẩu để đăng nhập bằng email và mật khẩu, ngoài Google. Các thiết bị khác sẽ bị đăng xuất. |
| `account.password.set.submit` | Set password | Đặt mật khẩu |
| `account.password.reauth.title` | Confirm it's you | Xác nhận đó là bạn |
| `account.password.reauth.action` | Continue with Google | Tiếp tục với Google |
| `account.password.reauth.help` | You'll come back here and enter your new password again. | Bạn sẽ quay lại đây và nhập lại mật khẩu mới. |
| `account.password.toasts.set` | Password set. You can now also sign in with your email and password. | Đã đặt mật khẩu. Giờ bạn cũng có thể đăng nhập bằng email và mật khẩu. |
| `profile.password.setBody` | Your account doesn't have a password yet. Set one to also sign in with your email. | Tài khoản của bạn chưa có mật khẩu. Hãy đặt mật khẩu để có thể đăng nhập bằng email. |
| `profile.password.setAction` | Set a password | Đặt mật khẩu |

- **Accessibility:** alerts follow design.md §4.3 roles; focus moves as stated per state; the countdown label is announced at most every 10 s; the alert's Google button has the visible label "Continue with Google" (the "G" mark is `aria-hidden`).

## Escalations & open items
- **No design-system escalation.** The Google "G" mark inside the set-password alert reuses FEAT-004's recorded brand-asset exception. `PasswordRules` stays under FEAT-001's pending E-2.
- **Component reuse for implementation:** `GoogleButton` (FEAT-004) is used on SCR-STF-018 too, so it lives in `web/packages/ui` or is exported through `features/auth/index.ts` (Sheriff: features import each other only through `index.ts`). `useCooldown` moves from `features/auth` to `web/packages/core` if `features/profile` uses it.
- **Plan correction already filed** in technical-design §8: SCR-STF-016 belongs in the FEAT-005 row.
