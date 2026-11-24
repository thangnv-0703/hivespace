# UI Design: FEAT-006 — Sign in with an email code

> Feature from: docs/implementation-plan.md · Pairs with: technical-design.md
> Screens: SCR-STF-021 (new), SCR-STF-022 (new), SCR-STF-012 (extended: the entry link) · Mode: per-feature · Status: Verified · Date: 2026-11-23
> Strategy: **code-native**. No design tool is connected and the manifest policy is `code-native`. SCR-STF-021 and SCR-STF-022 adapt `references/hivespace.web/apps/buyer/src/pages/Auth/OtpSignInPage.vue` and `OtpCodeEntryPage.vue`; SCR-STF-012 already has a manifest entry (FEAT-001) and gains one element.
> **Visual mockups (reference for implementation; the spec below wins on any difference):** committed copies in [mockups/](mockups/) and the live canvas https://claude.ai/artifact/GgmrggSSUKeMGxf1pdBzNK (private to the owner).
> - `Main.dc.html`: SCR-STF-022 default, desktop.
> - `CodeRequest.dc.html`: SCR-STF-021 default, desktop.
> - `CodeEntryMobile.dc.html`: SCR-STF-022 at 390 px, wrong code.
> - `CodeStates.dc.html`: the other states (SCR-STF-021 too soon; SCR-STF-022 expired, ended, locked, new code sent).
> - `SignInCodeLink.dc.html`: SCR-STF-012 with the new link.
> - `canvas.json`: layout index.
>
> Light theme and English only. States not drawn: Vietnamese copy, dark mode, the starting spinner, SCR-STF-021 sending and field errors, SCR-STF-022 verifying, rate limited, network error, no stored challenge (a redirect).

## SCR-STF-021 — Request sign-in code

- **Strategy & source:** code-native (this section is the design). Adapts `Auth/OtpSignInPage.vue`.
- **Route:** `/signin/code?returnUrl=…` (fullscreen, technical-design §5.5). The email typed on SCR-STF-012, if any, arrives in router **state**, not the query (D-A).
- **Composition:** `AuthLayout` (title "Sign in with an email code", subtitle "We'll email you a 6-digit code. No password needed.") → optional `Alert` → form: `Input` email (`type="email"`, `autocomplete="email"`, `inputmode="email"`, required) → primary `Button` `lg` full width "Send code" → footer line "Remember your password? **Sign in with password**" (link to `/signin?returnUrl=…`, keeps the typed email in router state). No Google button here: the sign-in page one step back has it (D-F).
- **Content & data mapping:**
  - Submit → `POST /v1/users/sign-in/code-challenges {email}` (`requestSignInCode`, technical-design §3.2) with `Accept-Language`.
  - `201 {challengeId, expiresAt, resendAvailableAt}` → saved with the email and `returnUrl` in `sessionStorage` (`codeChallenge.ts`), then `router.push('/signin/code/enter?returnUrl=…')`.
  - `400 common.validation_failed` → field error on email.
  - `429 identity.sign_in_code_too_soon` and `429 common.rate_limited` (with `Retry-After`) → **too soon** state.
- **Conformance:** corrected (reference gray/brand utilities → role tokens; the reference toast on success → navigation only; `text-brand-500` links → `text-text-link`). No escalation on this screen.
- **Mockup:** `CodeRequest.dc.html`, `CodeStates.dc.html` (too soon).

### default
Email field empty, or prefilled from router state; focus on the email field; button enabled.

### starting
Without `returnUrl` the page shows the `AuthLayout` with a centred `Spinner` and calls `goTo('/bff/login')`, exactly like SCR-STF-012's starting rule: a code sign-in without the authorize round trip would leave the user with no storefront session. The user then lands on SCR-STF-012 with a `returnUrl` and uses the link again.

### sending
Button `loading` (width kept, `aria-busy`), the email field disabled. After `201` the page stays in this state until the route changes.

### too soon
- Warning `Alert` above the form (`role="status"`), title "Please wait a moment", message from the API ("You just asked for a code. Wait a moment before asking again." or the gateway's rate-limit message).
- The button shows "Send code (42 s)" and is disabled, counting down from `Retry-After` (60 when missing) with `useCooldown`; announced through `aria-live="polite"` at most every 10 s. The typed email is kept.
- When the stored challenge is for the **same** email and has not expired, the alert carries an action link "Enter the code you already have" → `/signin/code/enter?returnUrl=…`.

### error
- **Client:** empty → `auth.errors.emailRequired`; malformed → `auth.errors.emailInvalid` (on blur and submit, design.md §4.2).
- **Network or 5xx:** error `Alert` "Something went wrong. Try again." with the muted support code line (design.md §6), as SCR-STF-012.

### email unavailable
Not a state of this screen: Identity cannot know at request time whether the email will be sent (technical-design D-6, divergence 1). The answer is always the same `201`; SCR-STF-022's help line covers a code that never arrives.

- **Responsive notes:** single column below `lg` (AuthLayout); the email input is 16 px text below `sm`.
- **Decisions:**
  - **D-A. The typed email travels in router state, not in the URL.** Driver: an email in a query string ends up in browser history and gateway access logs (NFR-LEG-001 minimisation). Rejected: `?email=` (the reference did this).

## SCR-STF-022 — Enter sign-in code

- **Strategy & source:** code-native (this section is the design). Adapts `Auth/OtpCodeEntryPage.vue`.
- **Route:** `/signin/code/enter?returnUrl=…` (fullscreen). Reads the stored challenge from `sessionStorage`; without one it replaces the route with `/signin/code?returnUrl=…`.
- **Composition:**
  1. `AuthLayout` title "Enter your code", subtitle "We sent a 6-digit code to **{email}**." followed by a link "Change email" → `/signin/code?returnUrl=…` with the email in router state.
  2. Optional `Alert` slot (warning or error states below).
  3. `CodeInput` with the visible legend "6-digit code" (provisional component, Escalation E-1).
  4. Help text under it, `text-xs text-text-muted`: "The code expires in 4:12." (live, from `expiresAt`, `m:ss`).
  5. Primary `Button` `lg` full width "Sign in".
  6. Resend row, `text-sm`: "Didn't get it?" followed by a `ghost` text button "Resend code", disabled with "Resend code in 0:42" until `resendAvailableAt`.
  7. Help line, `text-sm text-text-muted`: "Emails can take a minute. Check your spam folder, or **sign in with your password or Google** instead." The link goes to `/signin?returnUrl=…` (covers UC-029 2b, technical-design D-6).
- **Content & data mapping:**
  - Submit (button, Enter, or the sixth digit, D-B) → `POST /v1/users/sign-in/code {challengeId, code, returnUrl}` (`signInWithCode`, technical-design §3.3).
  - `200 {redirectUrl}` → clear the stored challenge, then `goTo(redirectUrl)` (full-page navigation to `/connect/authorize…`).
  - Resend → `POST /v1/users/sign-in/code-challenges {email}` with the stored email; `201` replaces the stored challenge.
  - Error code → state in the table under Cross-screen decisions.
- **Conformance:** corrected (reference red box → `Alert` error; reference amber box → `Alert` warning; raw `h-12 w-12` boxes and brand focus → `CodeInput` built on Input tokens; `text-brand-500` → `text-text-link`). **Escalation filed: E-1 (CodeInput).**
- **Mockup:** `Main.dc.html`, `CodeEntryMobile.dc.html`, `CodeStates.dc.html`.

### default
Six empty boxes, focus on the first, "The code expires in 4:59", resend disabled with its countdown, "Sign in" enabled.

### verifying
Button `loading`, boxes disabled, resend disabled. After `200` the page stays in this state until the browser leaves.

### wrong code
- After `401 identity.sign_in_code_invalid`: `CodeInput` error state (every box `border-feedback-error-icon`) and the error text under it with the API message "That code is not right. Check the email and try again." (`role="alert"`).
- Boxes are cleared and focus returns to the first box. Typing again clears the error.
- The page does not show remaining attempts (the API does not return them, technical-design §3.3).

### expired
- After `401 identity.sign_in_code_expired`, or when the expiry countdown reaches 0 (client side, no request).
- Warning `Alert`, title "Code expired", message "This code has expired. Request a new code." Boxes disabled, the expiry help line hidden.
- The primary button becomes "Send a new code" and calls the resend request; while `resendAvailableAt` has not passed it shows the countdown "Send a new code (0:12)" (D-C).

### ended
- After `401 identity.sign_in_challenge_ended` (used, replaced by a newer code, or five wrong codes).
- Same treatment as **expired**, title "Request a new code", message from the API "This code can no longer be used. Request a new code."

### locked
- After `423 identity.account_locked`.
- Warning `Alert`, title "Account temporarily locked", message from the API (FEAT-005: "Too many failed attempts. This account is locked for 15 minutes. Try again later, or sign in with Google."), with the action link "Sign in another way" → `/signin?returnUrl=…`. Focus moves to that link.
- Boxes and the button are disabled; resend stays available (a new code does not unlock, but the user may wait out the lock on this page).

### rate limited
After `429 common.rate_limited` on submit: warning `Alert` "Too many attempts" with the API message; the "Sign in" button counts down from `Retry-After` as on SCR-STF-012. Typed digits are kept.

### new code sent
After a successful resend: boxes cleared, focus on the first, the expiry countdown restarts, the resend countdown restarts at 60 s, and a status line under the resend row (`text-sm text-feedback-success-fg`, `CircleCheck`, `role="status"`): "New code sent. The earlier code no longer works." It disappears on the next submit. A `429` on resend restarts the resend countdown from `Retry-After` with no alert.

### error
Network or 5xx on submit or resend: error `Alert` "Something went wrong. Try again." with the support code line; digits kept.

- **Responsive notes:** below `sm` the boxes are 44 × 52 px with an 8 px gap (fits 312 px of content at 360 px); from `sm` 52 × 60 px with a 12 px gap. The resend row wraps under "Didn't get it?" at 360 px.
- **Decisions:**
  - **D-B. Submit when the sixth digit is entered, and keep the button.** Driver: the common pattern for one-time codes (paste or SMS-style autofill completes the code in one action); the button stays for keyboard users and for a retry after an error. A failed auto-submit never loops: after an error the next submit needs a new digit or the button.
  - **D-C. Expired and ended offer one action, "Send a new code", on the same page.** Driver: UC-029 4b ("asks for a new code"); going back to SCR-STF-021 would make the user retype the email. Rejected: a link back to SCR-STF-021 (the reference).
  - **D-D. The expiry countdown is shown and expires the page client side.** Driver: FR-NOTIF-016 tells the user how long the code is valid; showing it on the page saves a failed request. The server stays the authority (a `401 sign_in_code_expired` wins over the clock).

## SCR-STF-012 — Sign in

*Only the delta from FEAT-001's [ui-design.md](../FEAT-001-register-and-sign-in/ui-design.md#scr-stf-012--sign-in), FEAT-004's and FEAT-005's is specified here. Everything else is unchanged.*

- **Strategy & source:** code-native (this section is the design for the new element).
- **Content & data mapping:** the link goes to `/signin/code?returnUrl=…` with the current `returnUrl` and the typed email in router state (D-A). No new call.
- **Conformance:** pass (`Link` style, design.md §4.1). No escalation.
- **Mockup:** `SignInCodeLink.dc.html`.

### email code link
- Directly under the "Sign in" button, centred, `text-sm`: "Prefer not to type a password? **Sign in with an email code**" (the bold part is the underlined link, `text-text-link`). It is what FEAT-001 kept hidden.
- Shown in the default, error, locked and rate-limited states (the email code is a way out of a failed password attempt; while locked the code submit is refused too, but the request is not, technical-design D-4). Not shown in the starting and retry states (no form there).
- The "Forgot password?" slot on the password row stays reserved for FEAT-007.

- **Decisions:**
  - **D-E. A text link under the submit button, not a second full-width button.** Driver: one primary action per group (design.md §4.1); the Google button already sits above the divider as the second way in, and a third large button would compete with both. Rejected: a secondary `Button` "Email me a code" (the reference's layout).

## Cross-screen decisions

- **Error code → UI** (technical-design §3.1):

| Code | Screen | As |
| :-- | :-- | :-- |
| `identity.sign_in_code_too_soon` | SCR-STF-021; SCR-STF-022 resend | warning `Alert` with countdown (021); resend countdown only (022) |
| `identity.sign_in_code_invalid` | SCR-STF-022 | `CodeInput` error text, boxes cleared |
| `identity.sign_in_code_expired` | SCR-STF-022 | warning `Alert` "Code expired", "Send a new code" |
| `identity.sign_in_challenge_ended` | SCR-STF-022 | warning `Alert` "Request a new code", "Send a new code" |
| `identity.account_locked` | SCR-STF-022 | warning `Alert` "Account temporarily locked", "Sign in another way" |
| `common.rate_limited` | SCR-STF-021, SCR-STF-022 | warning `Alert`, submit countdown from `Retry-After` |
| `common.validation_failed` | SCR-STF-021 (email); SCR-STF-022 (`code`, only if the client check was bypassed) | field error; `CodeInput` error text |

- **Warnings, not errors,** for expired, ended, locked and too soon: none of them is a mistake the user just typed (FEAT-004 D-B tone rule). Only the wrong code is an error.
- **Stored challenge:** `{challengeId, email, expiresAt, resendAvailableAt, returnUrl}` in `sessionStorage` under one key; cleared on `200` and when SCR-STF-021 issues a new one; never the code.
- **Copy keys** (vi and en, identical key sets; API messages from `contracts/errors/messages.*.yaml` win, these are titles, labels and fallbacks):

| Key | en | vi |
| :-- | :-- | :-- |
| `auth.code.request.title` | Sign in with an email code | Đăng nhập bằng mã qua email |
| `auth.code.request.subtitle` | We'll email you a 6-digit code. No password needed. | Chúng tôi sẽ gửi cho bạn mã 6 chữ số qua email. Không cần mật khẩu. |
| `auth.code.request.submit` | Send code | Gửi mã |
| `auth.code.request.submitCooldown` | Send code ({seconds} s) | Gửi mã ({seconds} giây) |
| `auth.code.request.backPrompt` | Remember your password? | Bạn nhớ mật khẩu? |
| `auth.code.request.backLink` | Sign in with password | Đăng nhập bằng mật khẩu |
| `auth.code.request.tooSoonTitle` | Please wait a moment | Vui lòng chờ một chút |
| `auth.code.request.useExisting` | Enter the code you already have | Nhập mã bạn đã nhận |
| `auth.code.entry.title` | Enter your code | Nhập mã của bạn |
| `auth.code.entry.subtitle` | We sent a 6-digit code to {email}. | Chúng tôi đã gửi mã 6 chữ số đến {email}. |
| `auth.code.entry.changeEmail` | Change email | Đổi email |
| `auth.code.entry.legend` | 6-digit code | Mã 6 chữ số |
| `auth.code.entry.digit` | Digit {index} of 6 | Chữ số {index} trên 6 |
| `auth.code.entry.expiresIn` | The code expires in {time}. | Mã hết hạn sau {time}. |
| `auth.code.entry.submit` | Sign in | Đăng nhập |
| `auth.code.entry.notReceived` | Didn't get it? | Chưa nhận được mã? |
| `auth.code.entry.resend` | Resend code | Gửi lại mã |
| `auth.code.entry.resendIn` | Resend code in {time} | Gửi lại mã sau {time} |
| `auth.code.entry.resent` | New code sent. The earlier code no longer works. | Đã gửi mã mới. Mã trước không còn dùng được. |
| `auth.code.entry.help` | Emails can take a minute. Check your spam folder, or {link} instead. | Email có thể đến chậm một phút. Hãy kiểm tra thư rác, hoặc {link}. |
| `auth.code.entry.helpLink` | sign in with your password or Google | đăng nhập bằng mật khẩu hoặc Google |
| `auth.code.entry.newCode` | Send a new code | Gửi mã mới |
| `auth.code.entry.newCodeCooldown` | Send a new code ({time}) | Gửi mã mới ({time}) |
| `auth.code.entry.otherWay` | Sign in another way | Đăng nhập cách khác |
| `auth.errors.identity.sign_in_code_expired.title` | Code expired | Mã đã hết hạn |
| `auth.errors.identity.sign_in_challenge_ended.title` | Request a new code | Hãy yêu cầu mã mới |
| `auth.signIn.codePrompt` | Prefer not to type a password? | Không muốn nhập mật khẩu? |
| `auth.signIn.codeLink` | Sign in with an email code | Đăng nhập bằng mã qua email |

- **Accessibility:**
  - `CodeInput` is a `fieldset` with a visible `legend`; each box has `aria-label` "Digit n of 6"; the first box has `autocomplete="one-time-code"`; all boxes `inputmode="numeric"`, `pattern="[0-9]*"`; the error text is linked by `aria-describedby` to the fieldset.
  - Countdowns update their visible label every second but are announced at most every 10 s (`aria-live="polite"`), as on SCR-STF-012.
  - Focus moves as stated per state; alerts follow design.md §4.3 roles.

## Escalations & open items

- **E-1 — CodeInput (one-time code input) for design.md §4.2. Proposed toward ux-foundations; proceeding with the provisional treatment below (SCR-STF-022).**
  - *What the system lacks:* design.md §4.2 has no control for a short numeric code split into boxes. Composing six `Input`s is not enough: the control needs one value, paste distribution, auto-advance and a group label, which is a new kind of form control, not an arrangement.
  - *Proposed addition:* **CodeInput** — `length` boxes (6 here), each a single-character input using the Input tokens (`bg-bg-surface border border-border-control` radius lg `shadow-xs`, focus `border-action-primary-bg ring-2 ring-focus-ring/30 shadow-focus`, error `border-feedback-error-icon`, disabled `bg-bg-subtle text-text-disabled`), text `text-2xl font-semibold` tabular figures, centred. Sizes 44 × 52 below `sm`, 52 × 60 from `sm`; gap 8 / 12 px. Behaviour: digits only; typing advances; Backspace on an empty box moves back and clears it; arrow keys move; paste anywhere fills from the first box; emits `complete` with the full value; `v-model` is the joined string. Field anatomy (legend, help, error) as §4.2.
  - *Provisional treatment:* built in `web/packages/ui` exactly as proposed, so accepting the amendment changes no screen.
- **Inventory note (ux-foundations owns it):** SCR-STF-021's listed state "email unavailable" cannot be shown at request time (technical-design D-6); this design moves it to SCR-STF-022's help line. Proposed inventory wording for SCR-STF-021: "sending, resend wait, rate-limited"; for SCR-STF-022 add "new code sent" and "email may take a minute (help line)".
- **Routes:** `/signin/code` and `/signin/code/enter` replace the reference's `/auth/otp` and `/auth/otp/code` (technical-design divergence 3).
- **Component reuse for implementation:** `useCooldown` (already in `@hivespace/core`) drives every countdown; the `m:ss` format for the expiry and resend timers is a small formatter in the auth feature.
