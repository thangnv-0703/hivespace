# UI Design: FEAT-003 — Change password

> Feature from: docs/implementation-plan.md · Pairs with: technical-design.md
> Screens: SCR-STF-018 (new), SCR-STF-016 (extended: Password card) · Mode: per-feature · Status: Verified · Date: 2026-11-22
> Strategy: **code-native**. No design tool is connected. The manifest policy is `code-native`, and the reference app has no change-password page (ux-foundations: "none (new)").
> **Visual mockups (reference for implementation; the spec below wins on any difference):** committed copies in [mockups/](mockups/) and the live canvas https://claude.ai/artifact/6NYyGvX7QpEnwQ6ffhAyB4 (private to the owner).
> - `Main.dc.html`: change password, default.
> - `PasswordErrors`: wrong current password plus policy errors.
> - `PasswordMobile`: 390 px, success toast.
> - `ProfilePasswordCard`: the new card on the profile page.
> - `AccountMenu`: header menu with the new item.
> - `canvas.json`: layout index.
>
> Light theme and English only. States not drawn: submitting, `password_not_set` (409), save failed, dark mode.

## SCR-STF-018 — Change password

- **Strategy & source:** code-native (this section is the design). No reference page.
- **Route:** `/profile/password` in the storefront, inside `StorefrontLayout`, with route meta `requiresAuth`. With no session, the guard sends the browser to `/bff/login?returnUrl=/profile/password` (technical-design §5.5).
- **Entry points:**
  - the **Password** card on the profile page (SCR-STF-016, below);
  - **Change password** in the header account menu (Extras).
- **Composition:** the same frame as the profile page, so the two read as one account area. Container (design.md §3), `max-w-2xl` (672 px).
  1. **Back link:** a `Link` with a `ChevronLeft` 16 px icon and the text "Profile" → `/profile`. `text-sm`, 44 px tall touch target.
  2. **Heading row:**
     - `h1` "Change password" (`text-title-sm font-semibold`);
     - subtitle in `text-sm text-text-muted`: "After the change, you stay signed in here and every other device is signed out."
  3. **One `Card`** holding a `<form>` (`autocomplete="on"`). Fields have labels above them (design.md §4.2) and are 16 px apart:
     - **Current password** \*: `PasswordField` (show/hide toggle, FEAT-001 convention), `autocomplete="current-password"`.
     - **New password** \*: `PasswordField`, `autocomplete="new-password"`, with the `PasswordRules` hint below it: "At least 8 characters", "Contains a letter", "Contains a number". The live behaviour is the same as on Register (SCR-STF-013).
     - **Confirm new password** \*: `PasswordField`, `autocomplete="new-password"`. This check runs in the browser only and is never sent (D-B).
     - A hidden `<input type="text" autocomplete="username">` holds the user's email, so password managers attach the new password to the right account (`readonly`, visually hidden, `aria-hidden`). The email comes from the session (`/bff/user`).
     - **Footer:** primary `Button` "Change password" (size `md`). It is right-aligned from `sm` and full width below `sm`.
- **Content & data mapping:**
  - Submit → `PUT /v1/me/password {currentPassword, newPassword}`, with `Accept-Language` set to the UI locale.
  - On `204`:
    - call `ProfileSession.refresh()` (`POST /bff/session/refresh`), which keeps this session alive (technical-design D-3);
    - clear all three fields and reset `PasswordRules`;
    - show the success toast;
    - move focus to the `h1` (`tabindex="-1"`).
  - The page reads no data of its own. The email for the hidden username field comes from the session.
- **Conformance:** pass. Every component comes from design.md §4 and `web/packages/ui`: `Card`, `Input` through `PasswordField`, `Button`, `Alert`, `Toast`, `Link`. The one exception is `PasswordRules`, which is in `packages/ui` but still waiting on FEAT-001's escalation **E-2** (not yet in design.md §4.2). This screen reuses it on the same provisional terms: if E-2 is rejected, fall back to the plain help text "At least 8 characters, a letter and a number." No new escalation.
- **Mockup:** `Main.dc.html`, `PasswordErrors.dc.html`, `PasswordMobile.dc.html`.

### default
- All three fields are empty. The rules show in muted text with a `Circle` icon, and the button is enabled.
- Password values are never put in the URL, storage or logs, and they are cleared on success and when the user leaves the page.

### submitting
"Change password" is in the `loading` state (width kept, `aria-busy`), and repeat submits are blocked (design.md §4 Loading; technical-design §3.1: the call is never retried). Inputs stay enabled.

### error
- **Client checks** (on blur and on submit, never on each keystroke):
  - current empty → `account.password.errors.currentRequired` ("Enter your current password.");
  - new empty → `account.password.errors.newRequired`;
  - new breaks a rule → `PasswordRules` turns the unmet rules to the error style, and the field shows "Your password does not meet the rules below.";
  - confirm empty → `account.password.errors.confirmRequired`;
  - confirm ≠ new → `account.password.errors.mismatch` ("Passwords do not match.").

  Nothing is sent while a client check fails.
- **`400 common.validation_failed`:** each `details[]` entry goes to its field (table below), with the API's localized `message`, and all entries show at once.
  - `currentPassword` errors show on the current field.
  - `newPassword` policy codes show on `PasswordRules` in the error style, one line per code.
  - Focus moves to the first invalid field.
  - The **current password field is cleared** when its error is `identity.current_password_incorrect`. The new and confirm fields keep what the user typed.
  - With 3 or more errors, a summary `Alert` appears above the form with links to the fields (design.md §4.2).
- **`409 identity.password_not_set`:** an `Alert variant="info"` above the form, using the API message ("Your account doesn't have a password yet."). The form stays visible. Setting a first password is FEAT-005, so no action link is shown yet.
- **Save failed** (network, 5xx): an `Alert variant="error"` above the footer: "Your password wasn't changed. Try again." All values are kept.
- **`401`** (session ended): `ProfileSession.markSignedOut()`, and the guard sends the user to `/bff/login?returnUrl=/profile/password`. Typed values are lost, which is acceptable.

### success
- A success `Toast`: "Password changed. Other devices have been signed out." It dismisses after 5 s (design.md §4.3).
- The form is cleared, and the header still shows the user (the session refresh kept the session).
- If the refresh itself fails, the session store handles it as for any refresh, and the user is shown as signed out. The password change still stands (the toast already showed). This is rare and needs no special copy.

- **Responsive notes:**
  - Below `sm`: the card is full width with `p-4`, the button is full width, and input text is 16 px (no zoom on iOS).
  - From `md`: card padding is 24 px.
  - The mobile toast is top-centre (design.md §4.3).
- **Decisions:**
  - **D-A. A separate page, not a card inside the profile form.** Driver: different contract (`PUT /v1/me/password`), and it must never be saved together with profile fields. Password managers also handle a dedicated form with `current-password` / `new-password` better. Rejected: an inline expandable section on SCR-STF-016.
  - **D-B. Keep "Confirm new password", client-side only.** Driver: the same rule as Register (FEAT-001 D-B, user decision 2026-11-19). The contract has no confirm field. Rejected: sending it, or dropping it.
  - **D-C. Clear only the current password on a wrong-password error.** Driver: the user has to retype it anyway, and keeping the new password saves effort.

## SCR-STF-016 — Profile (extended)

The FEAT-002 design is unchanged except for one addition. Its manifest entry keeps `feature: FEAT-002`, and this section is listed as a supplement.

### password card
- A third `Card`, below the Language card, 24 px apart. It is a `<section aria-labelledby>`:
  - title "Password" (`text-lg font-semibold`);
  - body `text-sm text-text-muted`: "Change the password you use to sign in. Other devices will be signed out.";
  - a secondary `Button` rendered as a `RouterLink`: "Change password" → `/profile/password`. It is left-aligned and full width below `sm`.
- It is always shown. For an account without a password the next page shows the 409 notice. FEAT-005 will change this card into "Set a password".
- **Mockup:** `ProfilePasswordCard.dc.html`.

## Extras (storefront shell, not new SCR IDs)

### Header account menu (extends FEAT-002 Extras)
- `DropdownMenu` items, top to bottom:
  1. **Profile** (`User` icon) → `/profile`;
  2. **Change password** (`KeyRound` icon) → `/profile/password`;
  3. separator;
  4. **Sign out**.
- In the mobile menu panel the items appear in the same order.
- **Mockup:** `AccountMenu.dc.html`.

## Cross-screen decisions
- **D-D. Still no account sidebar.** This closes FEAT-002 D-A's open item. With two account pages, the menu item, the profile card and the back link give enough navigation. The account navigation (a sidebar from `lg`, a link row below) is designed when the third page arrives (FEAT-008 addresses, SCR-STF-017). Rejected: a two-item sidebar now.
- **Copy keys:** the `account.password.*` keys below, in vi and en with identical key sets, plus `common.account.changePassword` for the menu item and `profile.password.*` for the profile card:
  - `title`, `subtitle`, `back`;
  - `fields.current`, `fields.new`, `fields.confirm`;
  - `actions.submit`;
  - `errors.*`;
  - `toasts.changed`;
  - `notices.notSet`;
  - `rules.*` (reused from `auth.rules.*` through `packages/core`).

  Error messages from the API come from `contracts/errors/messages.*.yaml`; the client keys are fallbacks.
- **Error code → UI** (every code `PUT /v1/me/password` returns, technical-design §3.1):

| Code | Field | As |
| :-- | :-- | :-- |
| `identity.current_password_required`, `identity.current_password_incorrect` | Current password | field error (incorrect also clears the field) |
| `identity.password_too_short`, `identity.password_too_long`, `identity.password_needs_letter`, `identity.password_needs_digit` | New password | `PasswordRules` error state, all together |
| `identity.password_not_set` | form | info `Alert` above the form |
| `common.unauthenticated` | page | redirect to sign-in |
| other / network | form | error `Alert` "Your password wasn't changed. Try again." |

- **Accessibility:**
  - Each field error is linked by `aria-describedby`, and `PasswordRules` is linked to the new-password input.
  - The show/hide toggles have `aria-pressed`.
  - The success toast is announced through `ToastContainer` (`aria-live="polite"`), and focus moves to the `h1` after success.
  - The back link reads "Back to profile" to screen readers (`aria-label`).

## Escalations & open items
- **No new escalation.** `PasswordRules` stays under FEAT-001's pending E-2 toward ux-foundations. This screen is a second user of it, which strengthens the case for adding it to design.md §4.2.
- **Moves for implementation (technical-design D-6):** `PasswordField` moves to `web/packages/ui`, and `passwordChecks` / `passwordRules` move to `web/packages/core`, so both `features/auth` and `features/profile` import them without crossing feature boundaries.
- **FEAT-005 follow-up:** the profile Password card and the 409 notice become the "Set a password" flow (UC-026 1a).
