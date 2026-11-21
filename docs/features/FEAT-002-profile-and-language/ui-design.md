# UI Design: FEAT-002 — Profile and preferred language

> Feature from: docs/implementation-plan.md · Pairs with: technical-design.md
> Screens: SCR-STF-016 · Mode: per-feature · Status: Draft · Date: 2026-11-19
> Strategy: **code-native adapt spec**. No design tool is connected; the manifest policy is `code-native` (reuse from `references/hivespace.web`). This section lists the delta from the reference page.
> Reference page: `references/hivespace.web/apps/buyer/src/pages/Profile/ProfilePage.vue`.
> **Visual mockups (reference for implementation; the spec below wins on any difference):** committed copies in [mockups/](mockups/) (`Main.dc.html` = profile default, `ProfileErrors` = validation errors, `ProfileLoading` = loading skeleton, `ProfileMobile` = 390 px with save error, `AccountMenu` = header menu with the new "Profile" item; `canvas.json` = layout index) and the live canvas https://claude.ai/artifact/NCMYCzcnfWLv2DnHMxJHG3 (private to the owner). Logo: the real `web/packages/ui/src/assets/logo/logo-light.svg` (as `BrandLogo` renders it), uploaded to the canvas, so it shows only when viewed there. Light theme and English only. States not drawn: load error, saving, saved toast, language-save failure, dark mode.

## SCR-STF-016 — Profile

- **Strategy & source:** code-native (this section is the design). Adapts `Profile/ProfilePage.vue`.
- **Route:** `/profile` in the storefront, inside `StorefrontLayout`. Route meta `requiresAuth`: with no session the guard sends the browser to `/bff/login?returnUrl=/profile` (technical-design §5.5). Reached from the header account menu (Extras).
- **Composition:** container (design.md §3) → page heading row: `h1` "My profile" (`text-title-sm font-semibold`), subtitle `text-sm text-text-muted` "Manage your personal details and language." → two `Card`s stacked, `max-w-2xl` (672 px), 24 px apart:
  1. **Details card** (a `<form>`):
     - Identity row: `Avatar` 64 px (initials fallback) + display name `text-lg font-semibold` + email `text-sm text-text-muted`.
     - Fields, label above control (design.md §4.2 field anatomy), 16 px apart:
       - `Input` **Email**, read-only style (no border, `bg-bg-subtle`, `readonly`, `aria-readonly`), help "Your sign-in email can't be changed."
       - `Input` **Display name** \* (`autocomplete="name"`, `maxlength` not set so the server message shows; help "Shown on your orders and reviews. Up to 80 characters.").
       - `Input` **Phone** (`type="tel"`, `inputmode="tel"`, `autocomplete="tel"`, placeholder `0912 345 678`, help "Vietnamese number, for example 0912 345 678 or +84 912 345 678." per design.md Phone input).
       - `RadioGroup` **Gender** (horizontal, wraps): Male · Female · Other · Prefer not to say. A "Clear" text link appears when one is selected (sends `null`).
       - **Date of birth**: three `Select`s in one row (Day, Month, Year), each `w-28`, wrapped in a `<fieldset>` with `<legend>`. Month options show localized month names (`Intl.DateTimeFormat(locale, {month: 'long'})`). Year options run from the current year back 120 years. A "Clear" text link when any part is set.
     - Footer: primary `Button` "Save changes" (size `md`), right-aligned from `sm`, full width below `sm`.
  2. **Language card:** title "Language" (`text-lg font-semibold`), help "Used for the site and for emails we send you." → `RadioGroup` vertical: "Tiếng Việt", "English" (each in its own language, as in `LanguageSwitcher`). It is **not** part of the form and has no Save button: picking an option switches the UI at once and saves (FR-LOC-002, D-B).
- **Content & data mapping:**
  - On mount: `GET /v1/me/profile` → email, displayName, phone, gender, dateOfBirth, avatarUrl (always `null` now → initials), language (selects the radio).
  - Phone is shown as returned (`+84912345678`) formatted for reading as `+84 912 345 678`; the user may type any accepted form.
  - Date of birth splits into day/month/year from `YYYY-MM-DD`; on save the three join back. All three empty → `null`.
  - Save → `PUT /v1/me/profile {displayName, phone|null, gender|null, dateOfBirth|null}` (empty phone field → `null`). On 200: replace the form with the response, then `POST /bff/session/refresh` (session store `refresh()`), so the header name updates.
  - Language radio → `setLocale` at once, then `PUT /v1/me/profile/language {language}`, then `POST /bff/session/refresh`. Same handler as the header switcher (one composable, D-C).
- **Conformance:** corrected vs reference:
  - Left-aligned label column (`w-36 text-right`) → labels above controls (design.md §4.2), which also fixes 360 px and Vietnamese label lengths.
  - Masked email and phone with "Change" buttons → email read-only with no action (FR-PROF-004, email change out of scope); phone is a normal editable field (FR-PROF-002).
  - "Username" row dropped (no such field in the contract).
  - Gender: 3 options → 4, adding "Prefer not to say" (FR-PROF-002 notes); clearable.
  - Avatar `FileInput` (5 MB) and `ProfileSidebar` dropped for now: upload is FEAT-035; the sidebar returns when the account area has a second page (D-A).
  - `bg-[#f5f5f5]`, `bg-white`, `text-gray-*`, `border-gray-100` → `bg-bg-canvas`, `Card`, `text-text-*`, `border-border-default`.
  - Language added as its own card (not in the reference page; FR-PROF-001, FR-PROF-006).
  - No escalation: every component used exists in design.md §4 and `web/packages/ui`.
- **Mockup:** `Main.dc.html`, `ProfileErrors.dc.html`, `ProfileLoading.dc.html`, `ProfileMobile.dc.html`.

### default
Form filled from the profile. "Save changes" is always enabled (no dirty tracking); saving unchanged values is harmless (technical-design §3.1). Language radio reflects the current UI language.

### loading
Skeleton in the shape of the two cards (design.md §4.4): a 64 px circle and two text bars for the identity row, five label + 44 px field bars, a button-sized bar; the language card with two 20 px rows. `aria-busy="true"` on the main region.

### error
- **Load failed** (network, 5xx): in place of the cards, `Alert variant="error"` "We couldn't load your profile." with a **Retry** button and the muted support code (design.md §4.4, §6).
- **Client checks** (on blur and submit): display name empty → `profile.errors.displayNameRequired`; date of birth partially chosen → `profile.errors.dateOfBirthIncomplete` on the fieldset ("Choose day, month and year, or clear the date."); an impossible date (31 February) → `profile.errors.dateOfBirthInvalid`. Everything else is left to the server, which is the authority.
- **`400 common.validation_failed`:** each `details[]` entry goes to its field with the API's localized message (table below). Three or more errors → summary `Alert` above the form with links to each field (design.md §4.2). Focus moves to the first invalid field. Nothing is saved, values stay as typed.
- **Save failed** (network, 5xx): `Alert variant="error"` above the form footer "Your changes weren't saved. Try again." Values kept; button returns to normal.
- **`401` during save** (session ended): the session store treats it as signed out and the guard sends the user to `/bff/login?returnUrl=/profile`. Typed values are lost; acceptable because sessions last 7 days.

### saving
"Save changes" in `loading` (width kept, `aria-busy`), inputs stay enabled but submit is blocked (design.md §4 Loading).

### saved
Success `Toast` "Profile saved" (design.md §4.3: toasts confirm completed actions). The header name changes after the session refresh. Focus stays on the button.

### language switching
- Selecting a language: UI text changes at once (`<html lang>` too), the radio stays on the new value. While the request runs, the radio group has `aria-busy` but is not disabled.
- **Save failed:** error `Toast` with the server message ("We couldn't save your language. It applies on this device for now."). The UI keeps the new language (it is also in the browser), the radio keeps the new value.
- `400 identity.language_unsupported` cannot happen from this UI (only two options); if it does, the same toast.

- **Responsive notes:** below `sm` the cards are full width with `p-4`; the gender radios wrap two per row; the date selects share the row equally (`flex-1` instead of `w-28`); "Save changes" is full width. From `md` the card padding is 24 px.
- **Decisions:**
  - **D-A. No account sidebar yet.** Driver: Profile is the only account page until FEAT-003 (change password) and FEAT-008 (addresses); a one-item nav is noise. FEAT-003's ui-design adds the account navigation. Rejected: the reference's `ProfileSidebar` now.
  - **D-B. Language saves on selection, outside the form.** Driver: FR-LOC-002 ("switch the UI at once") and the separate `PUT /v1/me/profile/language`. Putting it in the form would make the UI switch before a Save the user may never press. Rejected: language as a form field.
  - **D-D. Date of birth as three selects.** Driver: reference pattern, works the same in both locales and on touch, needs no new `DatePicker` component; the year list is bounded by the 120-year rule. Rejected: native `<input type="date">` (format follows the browser, not the app locale, and is inconsistent on mobile).

## Extras (storefront shell, not new SCR IDs)

### Header account menu (extends FEAT-001 ui-design Extras)
- `DropdownMenu` items, top to bottom: **Profile** (`User` icon, link to `/profile`), separator, **Sign out** (unchanged form POST).
- Header language switcher: same behavior as the language card when signed in (switch at once, then save and refresh). Signed out: browser only, no request (UC-027 3a).
- On session load, when the profile language differs from the active UI language, the UI switches to the profile language without a toast (D-7 of technical-design).
- **Mockup:** `AccountMenu.dc.html`.

## Cross-screen decisions
- **D-C. One language composable** (`useLanguagePreference` in the storefront `features/profile`, exported through `index.ts`) used by both the header switcher and the language card, so the two always behave the same. The `StorefrontLayout` imports it through the feature's `index.ts` (Sheriff).
- **Copy keys:** `profile.*` (`profile.title`, `profile.fields.*`, `profile.help.*`, `profile.gender.*`, `profile.language.*`, `profile.actions.*`, `profile.errors.*`, `profile.toasts.*`) in vi and en with identical key sets; `common.account.profile` for the menu item.
- **Error code → UI** (all codes the profile endpoints return, technical-design §3.5):

| Code | Field | As |
| :-- | :-- | :-- |
| `identity.display_name_required`, `identity.display_name_too_long` | Display name | field error |
| `identity.phone_invalid` | Phone | field error |
| `identity.gender_invalid` | Gender | field error on the radio group |
| `identity.date_of_birth_invalid`, `identity.date_of_birth_in_future`, `identity.date_of_birth_too_old` | Date of birth fieldset | field error |
| `identity.email_read_only` | Email | not reachable from the UI (the client never sends email); handled as a generic save error if it happens |
| `identity.language_unsupported` | Language card | error toast |
| `common.unauthenticated` | page | redirect to sign-in |

- **Registration copy follow-up (FEAT-001 screen):** SCR-STF-013's client check "> 60" becomes "> 80" (technical-design D-2). One-line change in its spec's error section during implementation; no new design.
- **Accessibility:** each card is a `<section>` with `aria-labelledby` its title; date fieldset with legend "Date of birth" and each select labelled "Day", "Month", "Year"; the read-only email is still focusable and announced as read-only; toasts via `ToastContainer` (`aria-live`).

## Escalations & open items
- None toward ux-foundations: the screen composes from existing design.md components.
- **Inventory note:** SCR-STF-016 lists "avatar upload" and "upload error"; both arrive with FEAT-035 (UC-028). The avatar shows initials until then. Recorded as a states gap in the manifest.
- **Open:** where the account navigation lives (sidebar at `lg`, tabs below) is decided by FEAT-003's ui-design (D-A).
