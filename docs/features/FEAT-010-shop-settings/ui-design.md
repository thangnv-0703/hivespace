# UI Design: FEAT-010 — Shop settings: profile, pickup address, payout

> Feature from: docs/implementation-plan.md · Pairs with: technical-design.md
> Screens: SCR-SEL-007 (new), SCR-SEL-008 (new); SCR-SEL-002 reused (checklist links and "all done" state) · Mode: per-feature · Status: Draft · Date: 2026-11-24
> Strategy: **code-native**. No design tool is connected and the manifest policy is `code-native`. The reference (`references/hivespace.web/apps/seller`) has no shop-settings page; `RegisterStorePage.vue` has description and address fields, which were read for field order only. Both screens compose from existing `@hivespace/ui` components inside the SEL shell; no new component and no design-system amendment.
> **Visual mockups (reference for implementation; the spec below wins on any difference):** committed copies in [mockups/](mockups/) and the live canvas https://claude.ai/artifact/9W61KRjkR4T5a3BjXGP9RN (private to the owner).
> - `Main.dc.html`: SCR-SEL-007 default, desktop, inside the seller shell with the Settings group open.
> - `PickupPayout.dc.html`: SCR-SEL-008 saved (address form pre-filled, payout summary masked).
> - `PickupPayoutFirst.dc.html`: SCR-SEL-008 first visit, both forms empty.
> - `PickupPayoutErrors.dc.html`: SCR-SEL-008 address errors with summary alert; payout change form with an invalid account number.
> - `PickupPayoutMobile.dc.html`: SCR-SEL-008 at 390 px, first visit.
> - `States.dc.html`: loading skeleton, load error, the "ready" and "saved" toasts, the sidebar Settings group (U-11).
> - `canvas.json`: layout index.
>
> Light theme and English only. States not drawn: Vietnamese copy, dark mode, saving (button loading), SCR-SEL-007 load error, SCR-SEL-008 below `sm` for the payout summary.

## SCR-SEL-007 — Shop profile

- **Strategy & source:** code-native (this section is the design).
- **Route:** `/settings/profile` inside `SellerLayout` (sidebar shell). `/settings` redirects here (FEAT-009 links and bookmarks keep working).
- **Composition:**
  1. `PageHeader`: breadcrumb "Home / Settings / Shop profile", title "Shop profile". No header action (save lives in the card, U-11).
  2. One `Card`, `max-w-3xl` (768 px), title "Shop details":
     - `Input` "Shop name" (required, `maxlength` 60, `autocomplete="off"`), help text "3–60 characters. Buyers see this name, exactly as you write it."
     - `TextArea` "Description" (optional, `rows` 5, `maxlength` 1000), help text "Shown on your shop page. Up to 1,000 characters." with a live count "{n} / 1,000" right-aligned in the same `text-xs text-text-muted` line.
     - Read-only row "Seller type": the label and the value ("Individual" or "Business") as `text-sm text-text-primary`, with `text-xs text-text-muted` "Set when you opened the shop." It is not a field: the contract cannot change it.
     - Card footer (`border-t border-border-default`, `pt-4`, right-aligned): primary `Button` `md` "Save changes".
- **Content & data mapping:**
  - Load: `GET /v1/shops/me` (`getMyShop`, technical-design §3.1) → `name`, `description` (empty when `null`), `sellerType`.
  - Save: `PUT /v1/shops/me/profile {name, description}` (`updateMyShopProfile`); an empty or whitespace description is sent as `null`. `200 MyShop` replaces the page's copy of the shop.
  - `400 common.validation_failed` → `details` on fields: `name` (`identity.shop_name_*`), `description` (`identity.shop_description_too_long`, `_invalid`).
  - `409 identity.shop_name_taken` → field error on the name.
  - Success → toast "Shop profile saved". If the response's `setup` turned complete because of another tab's save, nothing extra shows here.
- **Conformance:** pass. `Card`, `Input`, `TextArea`, `PageHeader`, `Button` per design.md §4.2/§4.5. One small component gap, not a system change: `TextArea` has no help-text slot while design.md §4.2's field anatomy includes help text; the implementation adds a `hint` prop to `TextArea` matching `Input`'s (recorded on the manifest entry).
- **Mockup:** `Main.dc.html` (default, desktop, in the shell).

### default
Fields pre-filled from the shop. Focus is not moved on load (the page is reached by navigation; the `h1` receives focus per the router's focus rule).

### loading
`PageHeader` shows at once; the card becomes a `Skeleton` with a title line, a 44 px block, a 120 px block and a 20 px line (design.md §4.4).

### error
- **Load error:** error `Alert` in place of the card, message from the error code, **Retry** re-requests `getMyShop`. `401` → `/bff/login?returnUrl=/settings/profile`; `403` → `session.refresh()` and the guard reroutes (as SCR-SEL-002).
- **Client checks** (blur and submit, mirroring the server): name empty → "Enter a shop name."; under 3 characters → "Use at least 3 characters."; length caps are enforced by `maxlength`.
- **Server field errors:** on their fields; first invalid field focused.
- **Name taken:** "This shop name is already taken. Try another." on the name field.
- **Network or 5xx:** error `Alert` above the card "We couldn't save your shop profile. Try again." with the muted support-code line; typed values kept.

### saving
Button `loading` (width kept, `aria-busy`), fields disabled until the response.

- **Responsive notes:** the card is full width below `md`; the footer button becomes full width below `sm` (44 px).
- **Decisions:**
  - **U-7. Logo and cover are not shown yet.** Driver: FR-SHOP-010's images belong to FEAT-035/036 (technical-design D-8). Showing an empty uploader that cannot work would be a dead control; FEAT-036 adds an "Images" card above "Shop details". Rejected: a "coming soon" placeholder (same reason as FEAT-009 U-3).

## SCR-SEL-008 — Pickup address and payout

- **Strategy & source:** code-native (this section is the design).
- **Route:** `/settings/pickup-payout` inside `SellerLayout`.
- **Composition:**
  1. `PageHeader`: breadcrumb "Home / Settings / Pickup and payout", title "Pickup address and payout", subtitle `text-sm text-text-muted` "Both are needed before you can publish products."
  2. Two `Card`s stacked with a 24 px gap, each `max-w-3xl`, each saved on its own (technical-design D-3):
  - **Card "Pickup address"** — header row: title, and a `Badge` success "Saved" when `pickupAddress` exists, or `Badge` warning "Missing" when it is `null`. Intro `text-sm text-text-secondary`: "Where carriers collect your orders. Only you can see it."
    - Row 1 (2 columns from `md`): `Input` "Contact name" (required, `maxlength` 80, `autocomplete="name"`); `Input` "Phone" (required, `type="tel"`, `inputmode="tel"`, `autocomplete="tel"`), help text "Vietnamese number, e.g. 0912 345 678" (design.md §4.2 phone input).
    - Row 2 (3 columns from `lg`, 1 below): `Input` "Province or city" (required, `maxlength` 60), `Input` "District" (required, `maxlength` 60), `Input` "Ward or commune" (required, `maxlength` 60). Free text (technical-design D-2).
    - Row 3: `Input` "Street address" (required, `maxlength` 200, `autocomplete="street-address"`), help text "House number and street."
    - Footer: primary `Button` "Save address".
  - **Card "Payout details"** — header row: title and the same Saved/Missing badge for `payout`. Intro: "The bank account for your earnings. Nothing is paid out yet; HiveSpace is a demo." (NFR-LEG-001 states no real transactions occur.)
    - **When `payout` is `null`, or after "Change":** the payout form:
      - `Input` "Bank name" (required, `maxlength` 80, placeholder "e.g. Vietcombank").
      - Row (2 columns from `md`): `Input` "Account number" (required, `inputmode="numeric"`, `autocomplete="off"`, `spellcheck="false"`, `maxlength` 23 to allow spaces), help text "6–19 digits. Spaces and dashes are fine."; `Input` "Account holder" (required, `maxlength` 80, `autocomplete="off"`), help text "As shown by your bank, usually in capitals without accents."
      - Footer: primary `Button` "Save payout details"; when opened by "Change", a secondary `Button` "Cancel" before it.
    - **When `payout` exists:** a read-only summary, a `dl` of three rows (label `text-sm text-text-muted`, value `text-sm text-text-primary`): "Bank" → `bankName`; "Account number" → `•••• 4567` (`tabular-nums`; visually hidden text "ending in 4 5 6 7" for screen readers); "Account holder" → `accountHolder`. Footer: secondary `Button` "Change".
- **Content & data mapping:**
  - Load: `GET /v1/shops/me` → `pickupAddress` (all six fields pre-fill the address form; phone shown as returned, E.164), `payout` (`bankName`, `accountNumberLast4`, `accountHolder`) and the badges.
  - Save address: `PUT /v1/shops/me/pickup-address {contactName, phone, province, district, ward, street}` (`setMyShopPickupAddress`).
  - Save payout: `PUT /v1/shops/me/payout {bankName, accountNumber, accountHolder}` (`setMyShopPayout`). "Change" pre-fills bank name and holder from `payout` and leaves account number empty (technical-design D-4).
  - `200 MyShop` → both cards re-render from the response; the payout form closes to the summary and the account number value is cleared from memory.
  - `400 common.validation_failed` → `details` on fields: `contactName`, `phone`, `province`, `district`, `ward`, `street` (`identity.address_field_*`, `identity.phone_invalid`); `bankName`, `accountNumber`, `accountHolder` (`identity.bank_*`).
  - Success → toast "Pickup address saved" / "Payout details saved". When the response turns `setup.pickupAddress && setup.payout` from incomplete to complete, the toast is instead "Your shop is ready to publish products." (no action link: the shared `Toast` has no action slot and the dashboard checklist already shows the ready state; decided 2026-11-25).
- **Conformance:** pass. `Card`, `Input`, `Badge`, `Button`, `Alert`, a plain `dl`; badge tones per design.md §4.3 (success for done, warning for a missing required item, colour never the only signal: both carry text).
- **Mockup:** `PickupPayout.dc.html` (both saved), `PickupPayoutFirst.dc.html` (first visit, both forms), `PickupPayoutErrors.dc.html` (validation errors and the change form), `PickupPayoutMobile.dc.html` (390 px, first visit).

### first visit
Both badges "Missing"; both forms empty; the payout card shows the form directly. Each card saves on its own, in either order.

### saved
Address form pre-filled (it stays an editable form: every field can be shown in full); payout as the summary with "Change".

### loading
`PageHeader` shows at once; each card becomes a `Skeleton` in its shape (title line plus three 44 px rows).

### error
- **Load error:** one error `Alert` with **Retry** in place of both cards; `401`/`403` as SCR-SEL-007.
- **Client checks** (blur and submit): required fields empty → "Enter {field label, lower case}." (e.g. "Enter the district."); phone not matching the Vietnamese rule → "Enter a Vietnamese phone number, e.g. 0912 345 678."; account number not 6–19 digits after removing spaces and dashes → "Enter 6 to 19 digits."
- **Server field errors:** on their fields; first invalid field in that card focused; with 3 or more errors in one card, a summary error `Alert` at the top of that card lists them (design.md §4.2).
- **Network or 5xx:** error `Alert` at the top of the card that failed, "We couldn't save your pickup address. Try again." or "…payout details…", with the support-code line; values kept, except that a failed payout save keeps the account number too (the user has not left the form).

### saving
The saving card's button is `loading` and its fields disabled; the other card stays usable.

- **Responsive notes:** below `md` every row is one column; buttons full width below `sm`; the summary `dl` stacks label over value below `sm`.
- **Decisions:**
  - **U-8. The address stays a form; the payout becomes a summary once saved.** Driver: every address field can be shown and edited in place, but the account number can only be shown masked, so an always-open payout form would show an empty required field that looks unsaved. Rejected: summaries for both (one more click for the address for no gain), an edit modal (design.md modals for forms are 560 px; an inline form keeps the page context).
  - **U-9. Account number shown while typing, not as a password field.** Driver: it is not a secret to the person entering it, and seeing the digits avoids typos in a value that only shows as last four afterwards. `autocomplete="off"` keeps it out of the browser's form history.
  - **U-10. No "Missing" nag elsewhere.** The dashboard checklist (SCR-SEL-002) already carries readiness; this page's badges are enough.

## SCR-SEL-002 — Dashboard (reused)
Designed by FEAT-009 ([ui-design.md#scr-sel-002--dashboard](../FEAT-009-register-shop/ui-design.md#scr-sel-002--dashboard)). Changes in this feature:
- Both checklist buttons go to `/settings/pickup-payout` (they pointed at `/settings`).
- `setup.pickupAddress` and `setup.payout` now come from the server, so the done rows and the **all done** state (FEAT-009 spec) are reachable; no new state.
- Contract binding unchanged (`GET /v1/shops/me`); the extra `MyShop` fields are not shown here.

## Cross-screen decisions
- **U-11. Settings is a sidebar group with two pages, not tabs on one page.** Driver: ux-foundations' IA lists "Shop profile" and "Pickup address and payout" as children of "Shop settings", and the sidebar already supports `subItems` (as Products). `SellerLayout` changes the "Settings" item to `subItems`: "Shop profile" → `/settings/profile`, "Pickup and payout" → `/settings/pickup-payout`. Rejected: one page with `Tabs` (the shared `Tabs` is a `tablist` for in-page panels, not route navigation).
- **U-12. Save lives at the foot of each card, not in the page header.** Driver: SCR-SEL-008 has two independently saved cards; putting the profile's save in the same place keeps one pattern. design.md §3's "primary actions on the right of the title row" is for page-level actions such as "Add product".
- **U-13. No unsaved-changes prompt in this slice.** Driver: forms are short and saved per card; a leave guard needs a ConfirmModal flow nobody asked for. Revisit with ProductForm (FEAT-012), which is long.
- **U-14. Seller-entered values are shown as stored** (design.md §6): trimmed and collapsed by the server; the phone in E.164 as returned.

## Escalations & open items
- **None toward ux-foundations.** Both screens compose from existing components.
- **Component gap (implementation, no system change):** `TextArea` gains a `hint` prop to show help text, as design.md §4.2's field anatomy already specifies.
- **Needs confirmation (carried from technical-design D-7):** the length limits drive the `maxlength` values, help text and client checks here.
- **Wording to confirm with the SRS note on districts (technical-design §8):** if the district becomes optional, the "District" field drops its `*` and client check; nothing else changes.
