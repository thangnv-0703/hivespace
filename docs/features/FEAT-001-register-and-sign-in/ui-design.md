# UI Design: FEAT-001 — Register, verify email, password sign-in and sign-out

> Feature from: docs/implementation-plan.md · Pairs with: technical-design.md
> Screens: SCR-STF-012, SCR-STF-013, SCR-STF-014 · Mode: per-feature · Status: Draft (E-1, E-2 accepted) · Date: 2026-11-19
> Strategy: all three are **code-native adapt specs**: the reference page is the starting point and this document lists the delta. No design tool is connected; the routing policy is `code-native` (matches the 2026-11-17 decision: reuse from `references/hivespace.web`, full mockup only if the layout changes).
> Reference pages: `references/hivespace.web/apps/buyer/src/pages/Auth/SignInPage.vue`, `Auth/SignUpPage.vue`, `VerifyEmailPage.vue`, `VerifyEmailCallbackPage.vue`; frame: `packages/shared/src/components/layout/AuthLayout.vue`.

> **Visual mockups (reference for implementation, the spec below wins on any difference):** committed copies in [mockups/](mockups/) (`Main.dc.html` = sign in, `Register`, `RegisterNotice`, `VerifyExpired`, `VerifyVerified`, `SignInMobile`, `UnverifiedBanner`; `canvas.json` = layout index) and the live canvas https://claude.ai/artifact/KtqAnGkhmfRHoBGsyhoUaP (private to the owner). Logo: the real `web/packages/ui/src/assets/logo/logo-light.svg` (as `BrandLogo` renders it), uploaded to the canvas, so it shows only when viewed there. Light theme and English only; states not drawn: verify invalid / already verified / missing token / network error, sign-in loading, dark mode.

User decisions applied (2026-11-19): Register keeps **Confirm password** (client-side check only, not sent to the API) and has no terms checkbox; the **unverified notice** is a banner under the storefront header (§ Extras).

## Shared frame — `AuthLayout` (used by all three screens)

Not an SCR; specified once here. Ported into `web/packages/ui` (technical-design §5.6, D-1).

- **Layout (≥ `lg`):** full screen, two columns of equal width. Left: form column on `bg-bg-surface`, content vertically centred, `max-w-md`, page gutter `px-6`, `py-10`. Right: brand panel `bg-brand-950` with the decorative grid shape, centred image (`buyer-auth.svg`), heading `text-title-sm font-semibold text-text-on-brand`, body `text-md` in `text-text-on-brand` at 70 % opacity.
- **Below `lg`:** brand panel hidden; single column, `px-4` (360 px minimum, no horizontal scroll). Title steps down one size (design.md §2.2).
- **Corner:** `LanguageSwitcher` (existing `ui` component) top-right of the form column, 44 px target. `ThemeToggle` next to it (both themes first-class).
- **Header block:** `h1` (one per page, `text-title-sm font-semibold text-text-primary`), subtitle `text-sm text-text-muted`, 32 px below. A small `BrandLogo` link to `/` above the title so the user can leave the flow (the reference has none; the layout has no storefront header).
- **Props only** (no i18n, router, store inside): `title`, `subtitle`, `imageSrc`, `imageAlt`, `imageHeading`, `imageBody`, default slot.
- **Conformance corrections vs reference:** `bg-white dark:bg-gray-900` → `bg-bg-surface`; `text-gray-*` → `text-text-primary/secondary/muted`; `bg-brand-950 dark:bg-white/5` → `bg-brand-950` in both themes (the dark form column is `gray-900`, the panel stays the deepest brand tone, so the two columns remain distinct). Brand image: `buyer-auth.svg` has 19 hard-coded hex fills; recolour to brand/gray tokens when it is copied (design.md §9 already flags the logo's off-palette purple).
- **Accessibility:** form column is `<main>`; panel is `aria-hidden` except the image alt; skip-link to the form.

### Form conventions for all three screens
- Validate on blur and on submit (design.md §4.2). On submit with errors: focus the first invalid field; `role="alert"` on errors.
- A server error with no field (e.g. `invalid_credentials`) is an `Alert variant="error"` above the form, not a toast.
- Submit button: `Button variant="primary" size="lg"`, full width, `loading` keeps the width and blocks repeats.
- Password fields use a show/hide toggle: `Eye` / `EyeOff` (lucide) as an `iconOnly` ghost button inside the input, `aria-label` "Show password" / "Hide password", `aria-pressed`. Replaces the reference's custom icons.
- **Component gaps in `ui` (implementation work, not design-system changes):** `Input` needs an `append` slot (reveal toggle) and a `hint` prop (design.md §4.2 already defines help text); `Alert` needs an optional `action` slot (design.md §4.3 already allows an action link).
- Email fields: `type="email"`, `autocomplete="email"`, `inputmode="email"`; password: `autocomplete="current-password"` on sign-in and `new-password` on register; display name `autocomplete="name"`.
- Links use `text-text-link` underlined (design.md §4.1), not the reference's `text-brand-500`.

## SCR-STF-012 — Sign in

- **Strategy & source:** code-native (this section is the design). Adapts `Auth/SignInPage.vue`.
- **Route:** `/signin?returnUrl=` (reached from Identity's authorize redirect, technical-design §3.2).
- **Composition:** `AuthLayout` → optional `Alert` (error) → form: `Input` email, `Input` password (with reveal), primary `Button` "Sign in" → footer line "No account? **Create account**" (link to `/register`, keeps `returnUrl`).
- **Content & data mapping:** submit → `POST /v1/users/sign-in {email, password, returnUrl?}`; on `200 {redirectUrl}` do a full-page navigation to `redirectUrl` (it is a `/connect/authorize` URL or `/`, not an SPA route). `returnUrl` is passed through unchanged from the query; the client never builds it.
- **Conformance:** corrected (reference red alert → `Alert` error; gray/brand utilities → role tokens; custom password icons → lucide). Escalation filed: see Escalations E-1.

### default
Title "Sign in" / subtitle "Welcome back to HiveSpace". Fields empty, button enabled. Hidden until later features (not rendered, not disabled): Google button and "or" divider (FEAT-004), "Sign in with an email code" link (FEAT-006), "Forgot password?" link (FEAT-007). Layout leaves the password-row right side free for the future forgot link.

### error
- **Field (client):** email empty → `auth.errors.emailRequired`; malformed → `auth.errors.emailInvalid`; password empty → `auth.errors.passwordRequired`. Shown on blur and submit.
- **`401 identity.invalid_credentials`:** `Alert variant="error"` title "Sign-in failed" with the API's localized message (same body for unknown email, wrong password, no password; never says which). Password field is cleared and focused; email stays.
- **`400 common.validation_failed` (malformed email caught server-side):** map `details[].field` to the field error.
- **Network / 5xx (`common.internal_error`):** `Alert` error "Something went wrong. Try again." with the muted support code line from design.md §6.
- **Locked out (FEAT-005):** not designed here.

### loading
Button `loading` (spinner replaces text, width kept, `aria-busy`); inputs disabled. After success the page stays in this state until the redirect completes (no flash of the form).

### unverified (not a state of this screen)
FR-AUTH-009 lets unverified users sign in, so there is no unverified result on this page. The inventory's "unverified notice" is delivered as the post-sign-in banner (Extras). Inventory update proposed in Escalations E-3.

- **Responsive notes:** single column below `lg`; the footer "Create account" line wraps under the button.
- **Decisions:** D-A. Sign-in never redirects to `/verify-email` (the reference did when verification was required). Driver: FR-AUTH-009. Rejected: reference behaviour.

## SCR-STF-013 — Register

- **Strategy & source:** code-native. Adapts `Auth/SignUpPage.vue`; the check-your-email view adapts `VerifyEmailPage.vue`'s "sent" state, now living on this page.
- **Route:** `/register` (one route, two views switched by local state; browser back from the notice returns to the form with the typed values kept except passwords).
- **Composition (form view):** `AuthLayout` → `Alert` (error, summary) when ≥ 3 errors → form: `Input` display name, `Input` email, `Input` password (reveal) with `PasswordRules` hint, `Input` confirm password (reveal), primary `Button` "Create account" → footer "Already have an account? **Sign in**" (keeps `returnUrl`).
- **Composition (notice view):** `AuthLayout` (title "Check your email") → `Alert variant="info"`-style card: `Mail` icon 48 px, "We sent a verification link to **a@b.com**", "The link works for 24 hours" → secondary `Button` "Resend email" with cooldown (below) → link "Wrong address? Go back" → link "Sign in".
- **Content & data mapping:** submit → `POST /v1/users {email, displayName, password}` (`confirmPassword` stays client-side); header `Accept-Language` from the UI locale. `201 {userId}` → switch to the notice view with the submitted email. Resend → `POST /v1/users/verification-email {email}` (always `202`).
- **Conformance:** corrected (Checkbox/terms and Google removed; gray/red utilities → roles). Escalation filed: E-1 (AuthLayout), E-2 (`PasswordRules`).

### default
Title "Create your account" / subtitle "Shop and sell on HiveSpace". `PasswordRules` shows three rules in muted text with a `Circle` icon: "At least 8 characters", "Contains a letter", "Contains a number". Hidden: Google button (FEAT-004).

`PasswordRules` live behaviour: icons turn to `Check` + `text-feedback-success-fg` as each rule is met while typing; unmet rules stay muted until submit, then turn `text-feedback-error-fg` with `CircleAlert` (colour is never the only signal; icon and text change too). It is purely a hint; the API is the authority (FR-AUTH-003).

### error
- **Client:** display name empty → `auth.errors.displayNameRequired`; > 60 → `displayNameTooLong`; email empty/malformed; password empty; confirm empty; confirm ≠ password → `passwordMismatch` (on the confirm field).
- **`400 common.validation_failed`:** each `details[]` entry goes to its field: `email_required`/`email_invalid` → email; `display_name_required`/`display_name_too_long` → display name; `password_too_short`/`password_needs_letter`/`password_needs_digit`/`password_too_long` → password, **all listed together** as one message per code, in `PasswordRules` error styling. Text comes from the API's localized `message`; client copy is the fallback.
- **`409 identity.email_in_use`:** error under the email field "This email is already registered" with an inline link "Sign in instead" (a plain link to `/signin`, no email carried over). The message does not say whether the account is verified.
- **Network / 5xx:** `Alert` error above the form, form values kept.
- Summary `Alert` above the form when ≥ 3 errors (design.md §4.2), each item a link to its field.

### loading
Submit button `loading`; all inputs disabled. Never blocks on email delivery (UC-001 4a): the notice appears as soon as `201` returns.

### notice (check your email)
- **Resend cooldown:** 60 s client-side (reference `useCooldown`); button label "Resend email (42 s)" disabled during cooldown, `aria-live="polite"` countdown announced at most every 10 s. Server rate limits arrive with FEAT-005, so the cooldown is the only guard now.
- **After resend:** inline `role="status"` text "Sent again to a@b.com" (not a toast: design.md §4.3 toasts are for completed actions, and this stays visible next to the button). Always shown on `202`, which is true even for unknown addresses.
- **Failure of resend (network):** `Alert` error, cooldown not started.
- **Email not arriving** (known gap, technical-design §8): help text "Check spam. If it still does not arrive, resend." No more.
- "Wrong address? Go back" returns to the form view with display name and email filled.

- **Responsive notes:** buttons full width below `sm`; notice card padding `p-4` → `p-6` from `md`.
- **Decisions:** D-B. Confirm password is kept per user decision, client-side only. Driver: user choice 2026-11-19. Rejected: send `confirmPassword` to the API (contract unchanged). D-C. The notice is a view of `/register`, not a separate route, so a refresh of a notice URL cannot show a "sent" claim without a submit. Rejected: reference's `/verify-email?maskedEmail=` route.

## SCR-STF-014 — Verify email

- **Strategy & source:** code-native. Adapts `VerifyEmailCallbackPage.vue` (result) and `VerifyEmailPage.vue` (request a new link).
- **Route:** `/verify-email?token=` (the link from the email, technical-design §5.5). With no `token` → the "missing" state.
- **Composition:** `AuthLayout`; centred result card in the form column: icon 48 px, `text-lg font-semibold` title, `text-sm text-text-muted` body, actions stacked (primary then secondary, full width). The request-a-new-link block (email `Input` + `Button`) appears inside the expired/invalid/missing cards.
- **Content & data mapping:** on mount, `POST /v1/users/verify-email {token}` (a POST triggered by page load, once; guarded against double invocation in dev strict mode). Results: `204` verified; `400 identity.verification_token_invalid`; `400 identity.verification_token_expired`; `409 identity.email_already_verified`. Request-new-link → `POST /v1/users/verification-email {email}`.
- **Conformance:** corrected (green/red boxes → `Alert`/result card tokens; `userId` query param removed because the API takes only `token`). Escalation: E-1.

### loading
`Spinner` size lg centred, text "Verifying your email…", `role="status"`. Title "Verifying". (Skeleton not used: the region is under 120 px of content, design.md §4.4.)

### verified (`204`)
`CircleCheck` in `text-feedback-success-icon`, title "Email verified", body "Your account is ready." Actions: primary "Sign in" (→ `/signin`), no secondary. If the user is already signed in (session probe), the primary is "Continue shopping" → `/`.

### expired (`400 identity.verification_token_expired`)
Also covers links invalidated by a newer resend (the API reports them as expired). `Clock` icon, `Alert` warning style: "This link has expired", body "Request a new one below. Only the newest link works." Block: email `Input` (prefilled from `sessionStorage` if the same tab just registered, else empty), primary "Send a new link" with the 60 s cooldown, then the same "Sent again" confirmation as the Register notice. Secondary "Sign in".

### invalid (`400 identity.verification_token_invalid`)
`CircleAlert`, "This link is not valid", body "It may be incomplete. Copy the full link from the email, or request a new one." Same request block as expired.

### already verified (`409 identity.email_already_verified`)
`CircleCheck` (info tone), "Your email is already verified", primary "Sign in". Covers a reused link.

### missing token and network error
- **Missing/empty `token`:** same as invalid with title "No verification link" (no API call).
- **Network / 5xx:** `Alert` error "We could not verify your email. Try again." with a **Retry** button (design.md §4.4 error pattern) and the muted support code.

- **Responsive notes:** card is the full form-column width; stacked buttons are already full width.
- **Decisions:** D-D. The request-new-link form lives inside this screen's error states rather than on a separate route. Driver: the API needs the email and the token carries no user (privacy: no enumeration), so we must ask; a separate page (as in the reference) adds a hop. Rejected: reference two-page split.

## Extras (inside this feature, not new SCR IDs)

These belong to the storefront shell (`StorefrontLayout`), specified here because FEAT-001 introduces them. They use only existing components.

### Header account area (design.md §3, top bar, right side)
- **Signed out:** "Sign in" and "Register" as `text-sm` text links in the 40 px top bar (design.md §3); both keep the current path as `returnUrl`. Below `md` they move into the menu drawer.
- **Signed in:** `Avatar` (initials fallback) + display name (truncate at 20 chars) → `DropdownMenu` with, for now, only "Sign out" (more items arrive with FEAT-002+). Sign out is a `<form method="post" action="/bff/logout">` button (technical-design D-4), same origin, no `fetch`.
- **Loading the session probe (`GET /bff/user`):** reserve the area (a 32 px skeleton circle) so the header does not jump; probe failure is treated as signed out.

### Unverified banner (user decision)
- **Where:** a full-width strip directly under the sticky header, `Alert variant="warning"` styled as a strip (no radius, same colours), shown only for signed-in users with `emailVerified === false`.
- **Content:** `MailWarning` icon, "Verify your email to place orders. We sent a link to a@b.com." with an `action` link "Resend email" (60 s cooldown, "Sent again" in place of the link for 5 s) and a dismiss `X` icon button (`aria-label` "Dismiss"; dismissal lasts for the browser session only, per-tab `sessionStorage`).
- **Not shown** on the full-screen auth routes (they have no storefront layout) and not blocking: browsing and search work (FR-AUTH-008). The order-placement block itself is FEAT-022.
- **Accessibility:** `role="status"`, announced once on load; dismiss button reachable before main content in tab order.

## Cross-screen decisions
- **Copy keys** are `auth.*` for pages (`auth.signIn.*`, `auth.register.*`, `auth.verify.*`, `auth.banner.*`, `auth.actions.*`, `auth.errors.*`), in vi and en with identical key sets (NFR-L10N-004). API-provided messages are shown verbatim; the client only falls back to its own text for network failures and for client-side checks.
- **Error code → UI** (server-sourced, all 13 codes of technical-design §3.7 accounted for):

| Code | Shown on | As |
| :-- | :-- | :-- |
| `email_required`, `email_invalid` | SCR-STF-013 email field; SCR-STF-012 email field | field error |
| `email_in_use` | SCR-STF-013 email field | field error with "Sign in instead" link |
| `display_name_required`, `display_name_too_long` | SCR-STF-013 display name | field error |
| `password_too_short`, `password_needs_letter`, `password_needs_digit`, `password_too_long` | SCR-STF-013 password | `PasswordRules` error state, all together |
| `verification_token_invalid` | SCR-STF-014 | "invalid" state |
| `verification_token_expired` | SCR-STF-014 | "expired" state |
| `email_already_verified` | SCR-STF-014 | "already verified" state |
| `invalid_credentials` | SCR-STF-012 | form-level `Alert` |

- **No enumeration in copy:** neither the sign-in error nor the resend confirmation says whether an address exists.
- **Language:** the language switcher is present on every auth screen; switching re-renders client copy at once and sends the new `Accept-Language` on the next request (FR-LOC-002 first use). The language chosen at registration is the one stored for emails (D-2).
- **Dark mode and Vietnamese length** checked in design: labels wrap, no fixed-width buttons, fields keep 44 px height.
- **Focus management:** on view switches (form → notice, loading → result) move focus to the new `h1` (`tabindex="-1"`).

## Escalations & open items
- **Status 2026-11-19:** E-1 and E-2 accepted by the user. The screens proceed on the specified treatment; `docs/design.md` still needs the matching §3/§4 entries through ux-foundations' amendment path. E-3 is still a proposal.
- **E-1 — Auth full-screen shell (filed toward ux-foundations).** design.md §3 defines the STF shell, cart/checkout reduced header and the SEL shell, but no full-screen auth layout with a brand panel. Needs: a §3 entry and a §4.5 component "AuthLayout" (props above), plus a rule that the brand panel is `bg-brand-950` with `text-on-brand` in both themes. Screens: SCR-STF-012/013/014. Provisional treatment: as specified here; the layout is already decided by technical-design D-1.
- **E-2 — `PasswordRules` hint (filed toward ux-foundations).** A live rule checklist is a new kind of component (icon + text + state, three tones). Needs a §4.2 entry. Screen: SCR-STF-013 (reused by FEAT-003 change password). Provisional treatment: as specified; if rejected, fall back to plain help text "At least 8 characters, a letter and a number."
- **E-3 — Inventory wording (proposed).** SCR-STF-012's "unverified notice" and "locked out" states: unverified is the banner (not a state of the page), lockout is FEAT-005. Suggest updating the ux-foundations inventory row to "invalid credentials, server error" for this feature.
- **Open:** logo recolour for `BrandLogo` (design.md §9, existing item) and the brand image recolour; terms-of-service copy is out of scope since terms checkbox was dropped (no requirement).

## Mockup alignment (post-implementation)
The committed mockups in [mockups/](mockups/) are authoritative for copy and the brand panel, where they differ from the text above: panel art is the linked-hexagons illustration (`web/packages/ui/src/assets/auth/auth-hex.svg`), panel heading and body are "Shop from many sellers, in one cart" / "Find what you need, pay once, and follow every order in one place.", and the sign-in, register, verified and notice subtitles and card layouts follow the mockups. `buyer-auth.svg` is no longer used by `AuthLayout`.
