# UI Design: FEAT-009 — Register a shop and become a seller

> Feature from: docs/implementation-plan.md · Pairs with: technical-design.md
> Screens: SCR-SEL-001 (new), SCR-SEL-002 (new, partial: setup checklist only), plus one shell extra (the seller-centre account menu) · Mode: per-feature · Status: Draft · Date: 2026-11-24
> Strategy: **code-native**. No design tool is connected and the manifest policy is `code-native`. SCR-SEL-001 adapts `references/hivespace.web/apps/seller/src/pages/RegisterStorePage.vue`; the reference has no seller dashboard page, so SCR-SEL-002 is new. This is the first seller-centre screen, so it was checked with anchor-level care: the SEL shell, Card, RadioGroup and Alert compose without a new component.
> **Visual mockups (reference for implementation; the spec below wins on any difference):** committed copies in [mockups/](mockups/) and the live canvas https://claude.ai/artifact/6wUtB4tPe162T63YL5GLsR (private to the owner).
> - `Main.dc.html`: SCR-SEL-001 default, desktop.
> - `RegisterMobile.dc.html`: SCR-SEL-001 at 390 px, filled in.
> - `RegisterErrors.dc.html`: SCR-SEL-001 validation errors, name taken, server error.
> - `RegisterUnverified.dc.html`: SCR-SEL-001 unverified email.
> - `Dashboard.dc.html`: SCR-SEL-002 new shop, desktop, inside the seller shell.
> - `DashboardStates.dc.html`: SCR-SEL-002 loading, error, all done.
> - `AccountMenus.dc.html`: seller-centre account menu, and the unchanged storefront header link (U-6).
> - `canvas.json`: layout index.
>
> Light theme and English only. States not drawn: Vietnamese copy, dark mode, SCR-SEL-001 starting and submitting, resend cooldown and "still unverified", SCR-SEL-002 below `sm`.

## SCR-SEL-001 — Register shop

- **Strategy & source:** code-native (this section is the design). Adapts `RegisterStorePage.vue`.
- **Route:** `/register-shop` on the seller host, outside the sidebar shell (U-1). The guard (technical-design §5.5) sends signed-in users without the Seller role here and sends sellers away from it.
- **Composition:** `AuthLayout`
  - **Props:** title "Open your shop on HiveSpace", subtitle "Two details and you're a seller. You keep your buyer account."
  - **Brand panel:** heading "Sell to buyers across Vietnam", body "List products, take orders and ship from one place."
  - **Corner slot:** `LanguageSwitcher`, `ThemeToggle`, and the seller-centre account menu (§Shell extras), so the user can sign out or switch accounts here.
  - **Form column, top to bottom:**
    1. An optional `Alert` (the error and unverified states).
    2. The form:
       - `Input` "Shop name" (required, `maxlength` 60, `autocomplete="off"`), help text "3–60 characters. Buyers see this name, exactly as you write it."
       - `RadioGroup` "Seller type" (required, vertical) with options "Individual" and "Business", and help text below it: "We record this as you choose it and don't verify it."
       - Primary `Button` `md`, full width, "Open my shop".
       - A muted note in `text-xs text-text-muted`: "Next you can add your pickup address and payout details."
- **Content & data mapping:**
  - Submit → `POST /v1/shops {name, sellerType}` (`registerShop`, technical-design §3.1) with `Accept-Language` and `X-HS-CSRF`. Seller type values: `individual`, `business`.
  - `201 MyShop` → `session.refresh()` (`POST /bff/session/refresh`) → `router.replace('/')`, then a success toast "Your shop is open". The refresh comes first, so the dashboard's seller-only call carries the new role (technical-design D-2).
  - `400 common.validation_failed` → each `details` entry on its field: `name` gets `identity.shop_name_required`, `_too_short`, `_too_long` or `_invalid`; `sellerType` gets `identity.seller_type_invalid`. Messages come from the response, in the request language.
  - `409 identity.shop_name_taken` → field error on the name.
  - `409 identity.shop_already_owned` → treated as success: refresh, go to `/` (technical-design D-5).
  - `403 identity.email_not_verified` → the **unverified** state.
  - Session fields used: `email` (unverified copy and resend), `emailVerified`, `displayName` (account menu).
  - Resend → `POST /v1/users/verification-email {email}` (`resendVerificationEmail`, FEAT-001).
- **Conformance:** corrected.
  - Reference fields cut: description, address and logo, because they belong to FEAT-010 and FEAT-035 and are not in `ShopRegistration`.
  - Reference left label column changed to labels above controls (design.md §4.2).
  - Reference `FullscreenLoader` during submit changed to a button loading state (§4.1), since it is one short request.
  - Reference raw gray/red utilities changed to role tokens and `Alert`.
  - Reference grid-shape background and footer copyright replaced by `AuthLayout`'s brand panel.
  - Reference error toast for validation changed to field errors (§4.3: toasts are never used for validation).
  - Seller-type radio group added (FR-SHOP-001).
  - No escalation.
- **Mockup:** `Main.dc.html` (default), `RegisterErrors.dc.html` (validation and name taken), `RegisterUnverified.dc.html`, `RegisterMobile.dc.html`.

### default
- **Fields:** empty name, and no seller type selected. Selecting nothing is deliberate: the choice is the user's, so there is no preselected guess (U-2).
- **Focus:** on the name field.
- **Button:** enabled.

### starting
While the session probe and the one-time refresh run (an unverified claim triggers `session.refresh()` on load, technical-design D-3), the form column shows a centred `Spinner` 24. The guard has already redirected anonymous users and sellers.

### submitting
- **Button:** `loading` (width kept, `aria-busy`).
- **Fields:** disabled.
- **After success:** the state stays until the route changes, including the session refresh.

### error
- **Client checks** (on blur and submit, design.md §4.2), mirroring the server:
  - Name empty → "Enter a shop name."
  - Name under 3 characters after trimming → "Use at least 3 characters."
  - Name over 60 characters is prevented by `maxlength`.
  - No seller type → "Choose a seller type."
  - On submit, the first invalid field gets focus.
- **Server field errors:** shown the same way.
- **Name taken:** field error "This shop name is already taken. Try another." with the name kept selected for editing.
- **Network or 5xx:** error `Alert` "We couldn't open your shop. Try again." with the muted support-code line (design.md §6). The typed values are kept.

### unverified
- **Warning `Alert`** above the form (`role="status"`):
  - Title: "Verify your email first".
  - Body: "We sent a link to **{email}**. Open it, then come back here."
  - Two actions:
    - Secondary `Button` "Resend email", with a 60 s cooldown after sending (`useCooldown`) and the result line "Email sent." or the 429 message.
    - Ghost `Button` "I've verified it", which calls `session.refresh()` and re-renders; if still unverified, the alert body adds "We still see your email as unverified."
- **Form:** visible but its fields and submit are disabled, so the user sees what comes next.
- **Triggers:** reached from the session claim (after the one-time refresh) or from a `403 identity.email_not_verified` response.

- **Responsive notes:** single column below `lg` (AuthLayout hides the brand panel). The radio rows are 44 px tall, and the name input uses 16 px text below `sm`.
- **Decisions:**
  - **U-1. Registration sits outside the sidebar shell, on `AuthLayout`.** Driver: a buyer without a shop cannot use any sidebar section; showing Products and Orders links that only bounce back would mislead. `AuthLayout` already exists, has the corner slot for language, theme and account, and fits a one-form onboarding step. Rejected: the reference's header-only page (it would need a new sidebar-less shell variant) and the full `AppShell` with a disabled sidebar.
  - **U-2. No preselected seller type.** Driver: FR-SHOP-004 records the user's own statement; a default would record a guess. Rejected: "Individual" preselected (fewer clicks, but silently wrong for businesses).

## SCR-SEL-002 — Dashboard

- **Strategy & source:** code-native (this section is the design). New; the reference has no seller dashboard page. This slice draws only the setup checklist; order counts (FEAT-025) and low-stock variants (FEAT-013) arrive later in the same page.
- **Route:** `/` inside `SellerLayout` (`AppShell`: sidebar, header, account menu).
- **Composition:**
  1. `PageHeader`:
     - Breadcrumb "Home / Dashboard".
     - Title "Dashboard".
     - Subtitle line `text-sm text-text-muted`: the shop name, as entered, then " · ", then the seller type label ("Individual" or "Business").
  2. Setup checklist `Card` (full width, max content width follows the shell):
     - **Header row:** title "Set up your shop" (`text-lg font-semibold`) and, on the right, the progress text "0 of 2 done" (`text-sm text-text-muted`).
     - **Intro line** (`text-sm text-text-secondary`): "Finish these before you publish your first product."
     - **Rows** (each a `border-b border-border-default` row, at least 56 px, 16 px padding): a 20 px status icon, a text block (title `text-sm font-medium`, one-line description `text-sm text-text-muted`), and a trailing action.
       - **Pickup address:** "Where carriers collect your orders." Action: secondary `Button` `sm` "Add address" → `/settings`.
       - **Payout details:** "The bank account for your earnings. Nothing is paid out yet." Action: secondary `Button` `sm` "Add bank details" → `/settings`.
     - **Status per row:**
       - Not done: lucide `circle` icon in `text-text-muted`, with the button.
       - Done: lucide `circle-check` in `text-feedback-success-icon`, the title followed by a `Badge` success "Done", and no button.
- **Content & data mapping:** everything from `GET /v1/shops/me` (`getMyShop`, technical-design §3.1):
  - `name` and `sellerType` → the subtitle.
  - `setup.pickupAddress` and `setup.payout` → the two rows.
  - The count of `true` values → the progress text.
  - Both are `false` in this slice (technical-design D-7); `/settings` is built by FEAT-010, so until then its links reach the NotFound page (SCR-SEL-009). This is accepted: the rows show what is next, and the 404 page offers the way back.
- **Conformance:** pass. The checklist composes from `Card`, `Button`, `Badge` and lucide icons; no new token or component. design.md §4.8 has no written spec for the "setup checklist" that ux-foundations lists among SEL components, so this section becomes its spec (see open items).
- **Mockup:** `Dashboard.dc.html` (new shop), `DashboardStates.dc.html` (loading, error, all done).

### new shop
Both rows not done, "0 of 2 done". This is the only populated state this slice can reach.

### loading
`PageHeader` title shown at once; the subtitle and the checklist card become `Skeleton` blocks in their shape (a 200 px line, then a card with a header line and two 56 px rows), per design.md §4.4.

### error
- **Inside the card area:** an error `Alert` with the message from the error code, plus a **Retry** button that re-requests `getMyShop` (design.md §4.4).
- **`403 common.forbidden`:** the session lost the Seller role, which cannot happen in the MVP. It is handled like a guard miss: `session.refresh()`, then the guard reroutes.
- **`401`:** the session ended → `/bff/login?returnUrl=/`.

### all done
Spec only, reachable from FEAT-010: both rows done, progress "2 of 2 done", and the intro line becomes "Your shop is ready to publish products." (`text-feedback-success-fg`). The card stays (it is the only content until FEAT-025).

- **Responsive notes:**
  - Below `sm` each row stacks: the icon and text on top, the button full width under them (44 px on touch).
  - The progress text moves under the title.
- **Decisions:**
  - **U-3. Checklist rows link to `/settings` before it exists.** Driver: FEAT-010 is the next slice and owns the target; hiding the actions would make the checklist read as information only. Rejected: disabled buttons with "coming soon" (copy that would ship and need removing).

## Shell extras

### Seller-centre account menu
- **Replaces** the stub in `SellerLayout.vue`'s `actions` slot, and appears in SCR-SEL-001's corner slot too.
- **Trigger:** an `Avatar` 32 px with initials (as the storefront), plus the display name (`text-sm font-medium`, truncated at 20 ch) from `md`.
- **`DropdownMenu` items:**
  - "Go to storefront": an external link to `VITE_STOREFRONT_ORIGIN`, through `onClick` with `location.assign`.
  - "Sign out" (`divided`): submits a hidden same-origin form `POST /bff/logout`.
- **Header link:** the existing "Go to storefront" link stays visible from `md`.
- **While the session probe runs:** a 32 px `Skeleton` circle, as on the storefront.

### Seller-centre language switcher
Same control. When signed in, a change also saves to the profile and refreshes the session (technical-design §5.5); a failed save keeps the new language for this visit and shows an error toast with the server message, as on the storefront (FEAT-002).

### Storefront entry
No storefront change. The existing header link "Seller centre" (design.md §3: in the header from `lg`, in the mobile menu panel below `md`) opens `{VITE_SELLER_ORIGIN}/` for everyone, and the seller-centre guard decides where the user lands: sign-in for a visitor, SCR-SEL-001 for a buyer without a shop, the dashboard for a seller (U-6). The account menu keeps Profile, Change password and Sign out.
- **Mockup:** `AccountMenus.dc.html` (right half).

## Cross-screen decisions
- **U-4. Copy tone.** SEL is direct and task-focused (design.md §6); the registration page is the buyer's first step into the seller centre, so its title and subtitle stay friendly ("Two details and you're a seller") while the dashboard is plain.
- **U-5. The shop name is shown exactly as entered** (design.md §6, seller-entered content is never translated), trimmed and collapsed as the server stored it.
- **U-6. One storefront entry: the header link, no account-menu item** (user decision 2026-11-24). Driver: design.md §3 already gives the storefront header a "Seller centre" link that visitors see too, and the seller-centre guard routes by the seller host's own fresh session, so a role-aware label adds nothing and can lag (technical-design D-10). Rejected: an account-menu item "Register a shop" / "Seller centre" (ux-foundations Part C), which duplicated the header link. Known gap, accepted: between `md` and `lg` the header link is hidden, so that width has no storefront entry.

## Escalations & open items
- **Open, toward ux-foundations (non-blocking, no amendment needed to proceed):** ux-foundations lists a "setup checklist" among the SEL surface components and points to design.md §4.8, but §4.8 has no entry for it. The composition in SCR-SEL-002 above uses only existing components; proposed: add a one-line §4.8 entry describing it (Card, rows with status icon, title, description and action, a progress count). Recorded on the manifest entry.
- **Open, toward ux-foundations (wording only):** Part C "Buyer to seller" says the storefront account menu shows "Register a shop" or "Seller centre". U-6 keeps the header link instead; proposed: reword that line to "the storefront header's Seller centre link opens the seller centre, which sends a buyer without a shop to registration".
- **Needs confirmation (carried from technical-design §8):** the 3–60 character shop-name limit drives the help text and client checks here.
