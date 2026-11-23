# UI Design: FEAT-004 — Google sign-in with auto-link

> Feature from: docs/implementation-plan.md · Pairs with: technical-design.md
> Screens: SCR-STF-012 (extended), SCR-STF-013 (extended) · Mode: per-feature · Status: Verified · Date: 2026-11-23
> Strategy: **code-native**. The screen was designed in FEAT-001 (manifest entry reused and updated); this feature adds the Google button, the divider and the `?error=` alerts. No design tool is connected, and the manifest policy is `code-native`.
> **Visual mockups (reference for implementation; the spec below wins on any difference):** committed copies in [mockups/](mockups/) (`Main` = sign in with the Google button, `GoogleUnavailable` = unavailable state, `GoogleAlerts` = the other three alerts, `SignInMobile` = 390 px with the cancelled notice, `RegisterGoogle` = Register with the Google button; `canvas.json` = layout index) and the live canvas https://claude.ai/artifact/A8C8z8n6T6nBC4sYouwXtB (private to the owner). Light theme and English only. States not drawn: Vietnamese copy, dark mode, button loading (follows design.md §4.1), password-form error with the Google button present.

## SCR-STF-012 — Sign in

*Only the delta from FEAT-001's [ui-design.md](../FEAT-001-register-and-sign-in/ui-design.md#scr-stf-012--sign-in) is specified here. Everything not mentioned (AuthLayout, fields, password errors, footer) is unchanged.*

- **Strategy & source:** code-native (this section is the design for the new parts; FEAT-001's section stays the design for the rest).
- **Route:** `/signin?returnUrl=…[&error=<code>]`. `error` is set only by Identity's redirects after a failed Google attempt (technical-design §3.1, §3.4).
- **Composition (top to bottom inside `AuthLayout`):**
  1. Optional `Alert` for a Google result (below).
  2. **Google button:** `Button variant="secondary" size="lg"`, full width, label "Continue with Google", with the Google "G" mark (20 px, left of the label, `gap-3`) as an inline SVG in `packages/ui` (`GoogleIcon`, `aria-hidden`).
  3. **Divider:** a row with a `border-t border-border-default` line on each side and the word "or" centred in `text-xs text-text-muted`, `my-6`.
  4. The existing password form, then the existing "No account yet? Create an account" line.
  The Google button comes first because it is a one-click path, and the form stays complete underneath it (FR-AUTH-023: password sign-in is always available).
- **Content & data mapping:**
  - The button is a **navigation**, not an API call: on click, `goTo('/connect/external/google/start?returnUrl=' + encodeURIComponent(returnUrl))` (technical-design §3.1, §5.5). `returnUrl` is the page's own query value, unchanged.
  - The `?error=` value maps to an alert by the table below. Codes come from technical-design §3.4. An unknown value shows nothing.
  - After reading it, the page drops `error` from the URL with `router.replace` (keeping `returnUrl`), so a reload or the browser Back button does not show the alert again.
- **Conformance:** pass. The `secondary` button, `Alert` tones, divider colours and type sizes are all existing tokens and components. One recorded exception: the Google "G" mark uses Google's own four brand colours, which Google's branding rules require and which are not design-system colours. It is confined to the icon and is not a fork of the system. No escalation.

### default
Title "Sign in" and subtitle unchanged. The Google button and divider appear above the form. The Google button and the form are both enabled. The hidden-until-later notes from FEAT-001 for the email-code link (FEAT-006) and the forgot link (FEAT-007) still hold.

### error (Google result alerts)
One `Alert` above the Google button, full width, `mb-6`. The Google button and the form stay enabled so the user can retry or use a password. The alert does not take focus; it is announced (`role="alert"` for error, `role="status"` for the others, design.md §4.3).

| `?error=` code | Tone | Title (en) | Message (en) |
| :-- | :-- | :-- | :-- |
| `identity.external_login_denied` | info | Google sign-in cancelled | You cancelled the Google sign-in. Try again, or sign in with your email and password. |
| `identity.external_email_unverified` | error | Google email not verified | Google could not confirm this email address, so we cannot sign you in with it. Use another Google account, or sign in with your email and password. |
| `identity.external_login_failed` | error | Google sign-in failed | We could not complete the Google sign-in. Please try again, or sign in with your email and password. |
| `identity.external_login_unavailable` | warning | Google sign-in unavailable | Google is not available right now. Sign in with your email and password, or try again later. |

Vietnamese (the fallback language, same keys under `auth.errors.<code>`, with `.title`):

| Code | Title (vi) | Message (vi) |
| :-- | :-- | :-- |
| `identity.external_login_denied` | Đã hủy đăng nhập bằng Google | Bạn đã hủy đăng nhập bằng Google. Hãy thử lại hoặc đăng nhập bằng email và mật khẩu. |
| `identity.external_email_unverified` | Email Google chưa được xác minh | Google không xác nhận được địa chỉ email này nên chúng tôi không thể đăng nhập bằng email đó. Hãy dùng tài khoản Google khác hoặc đăng nhập bằng email và mật khẩu. |
| `identity.external_login_failed` | Đăng nhập bằng Google không thành công | Chúng tôi không thể hoàn tất đăng nhập bằng Google. Hãy thử lại hoặc đăng nhập bằng email và mật khẩu. |
| `identity.external_login_unavailable` | Đăng nhập bằng Google đang không khả dụng | Google hiện không khả dụng. Hãy đăng nhập bằng email và mật khẩu, hoặc thử lại sau. |

Button label: "Continue with Google" / "Tiếp tục với Google". Divider word: "or" / "hoặc". Alert tone and icon per design.md §4.3 (info: `Info`, warning: `TriangleAlert`, error: `CircleAlert`).

A failed password attempt (`identity.invalid_credentials`, FEAT-001) shows its own error `Alert` in the same slot; only one alert shows at a time, and a new password submit clears a Google alert.

### loading
On click, the Google button goes `loading` (spinner replaces the label, width kept, `aria-busy`) and the email and password fields and the sign-in button are disabled, until the browser leaves the page (no flash of the form), as for the password submit. If the user returns with Back, the page loads fresh in the default state.

### unavailable
The `warning` row above is the unavailable state (UC-025 1a, FR-AUTH-023). It is drawn as its own board. The page is otherwise identical to default and the password form is fully usable.

- **Responsive notes:** below `lg` the single column is unchanged; the button and divider are full width at 48 px height, the alert wraps its text under the icon. Nothing else changes at 360 px.
- **Decisions:**
  - **D-A. Google button above the form.** Driver: it is the shortest path and the common pattern; the form remains visible, so password sign-in is never hidden (FR-AUTH-023). Rejected: below the form (a one-click option buried under fields).
  - **D-B. Alert tone follows who caused it.** Cancelled = info (the user's own choice, not an error), unavailable = warning (a dependency outage), unverified and failed = error. Rejected: all red (it blames the user for a cancel or an outage).
  - **D-C. Button label "Continue with Google", not "Sign in with Google".** Driver: the same button creates an account when none exists (FR-AUTH-020), and the architecture's own wording is "Continue with Google". Rejected: "Sign in with Google" (wrong for first-time users).
  - **D-D. Google button on Register too (user decision, 2026-11-23).** Driver: "Continue with Google" creates accounts, so a visitor on Create account expects to find it. See SCR-STF-013 below. The plan lists SCR-STF-012 only; the plan row is corrected in the technical design §8.

## SCR-STF-013 — Register

*Only the delta from FEAT-001's [ui-design.md](../FEAT-001-register-and-sign-in/ui-design.md#scr-stf-013--register) is specified here. The check-your-email notice view is unchanged.*

- **Strategy & source:** code-native (this section is the design for the new parts; FEAT-001's section stays the design for the rest).
- **Route:** `/register` (and its `?returnUrl=`, unchanged).
- **Composition (form view):** `AuthLayout` → **Google button** → **divider** → the existing form (display name, email, password with `PasswordRules`, confirm password, "Create account") → "Already have an account? Sign in". The button and divider are exactly those of SCR-STF-012 (same component, same label "Continue with Google", same "or" divider), placed under the subtitle and above the form. Nothing is added to the notice view.
- **Content & data mapping:** the button navigates to `/connect/external/google/start?returnUrl=…` with the page's own `returnUrl` query value, as on Sign in. No terms checkbox is added (FEAT-001 decision).
- **Conformance:** pass, with the same recorded Google "G" brand-mark exception as SCR-STF-012. No escalation.

### default
The Google button and divider appear above the form; the form is unchanged. Hidden-until-FEAT-004 note from FEAT-001 no longer applies.

### error
No new error state on this screen. **A failed Google attempt started here lands on `/signin?error=…`**, not back on Register, because Identity's redirects have one target (technical-design §3.1). The alert there already says to try again or use email and password. Rejected alternative: returning to the starting page (it would need a second query value and a second error mapping on Register for little gain).

### loading
Same as SCR-STF-012: the Google button goes `loading` and the form fields and "Create account" are disabled until the browser leaves.

- **Responsive notes:** unchanged; the button and divider are full width below `lg`.
- **Decisions:** D-E. Failures return to Sign in, not Register (above).

## Cross-screen decisions
None: one screen.

## Escalations & open items
- **No design-system escalation.** The Google "G" mark is a recorded brand-asset exception (see Conformance).
- **Component gaps in `ui` (implementation work, not design-system changes):** a `GoogleIcon` component; a small `Divider` with centred text (or inline markup in the page, since only one screen uses it).
- **Not designed:** the "password removed" explanation after linking an unverified account. It is delivered by the notice email only (technical-design D-5); no in-app message exists for it, and setting a new password belongs to FEAT-005.
