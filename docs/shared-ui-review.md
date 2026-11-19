# Shared UI review: reference `@hivespace/shared` vs `web/packages/ui`

> The approach is recorded in [ADR-0024](adr/0024-port-ui-subset-from-reference.md).
> Review of `references/hivespace.web/packages/shared/src` against our design system ([design.md](design.md)). Written 2026-11-18.
> Every ported component uses the semantic tokens (dark mode comes from the tokens), takes labels as props (the ui package has no i18n), has no pinia, vue-router, vue-i18n or axios import, and uses lucide icons. Tests: `web/packages/ui/src/__tests__/`.

## 1. Ported (in `web/packages/ui/src`)

| Component | From (reference) | Adaptations |
| :-- | :-- | :-- |
| Button, Input, Select, Spinner, Badge, Alert, Tabs, Pagination | `common/*` | Tokens; Button variants per design.md §4.1 (no success/warning); Pagination labels via props and no page-size select |
| Avatar | `common/Avatar` | Sizes renamed `xs..2xl`; status dot uses feedback tokens; lucide user icon |
| Checkbox, Radio, RadioGroup, ToggleSwitch | `common/*` | Real `useId`, 44 px rows, keyboard focus ring; Radio is a native radio (the reference let a chosen option be unchecked); RadioGroup has `role="radiogroup"` and a required `label`; ToggleSwitch has `role="switch"` |
| TextArea | `common/TextArea` | Error linked with `aria-describedby`; min height 96 px; reference `labelPosition` options dropped |
| Card | `common/ComponentCard` | Renamed; optional header with title and description; radius xl, border from tokens |
| FilterChips | `common/FilterChips` | `aria-pressed`, group label |
| FullscreenLoader | `common/FullscreenLoader` | `role="status"`, overlay token, message required |
| QuantityControl | `common/QuantityControl` | Pill per design.md §4.2; clamps to min/max; accessible names via `labels` prop |
| OrderTimeline | `common/OrderTimeline` | Reference used a `primary` colour that does not exist here: now the brand action tokens; list semantics, `aria-current="step"` |
| DropdownMenu | `common/DropdownMenu` | No `v-click-outside` directive and no teleport/position maths (menu opens under the trigger); roles and keyboard; links through a `linkAs` prop |
| Toast, ToastContainer | `common/Toast*` | Surface card with a 4 px feedback bar, hover/focus pause, errors stay until closed, max 3 stacked, `aria-live` |
| AppShell, AppSidebar, AppHeader, useSidebar | `layout/*` | No router, auth or notification code (see scaffold-notes §9) |
| LanguageSwitcher, ThemeToggle, PageHeader (breadcrumb), BrandLogo | `layout/header`, `common/ThemeToggler`, `PageBreadcrumb`, `assets/logo` | Done earlier this session |

## 2. Needs adapting (flagged, not ported yet)

| Component | Why it is not a straight port | Suggested next step |
| :-- | :-- | :-- |
| Modal, ModalWrapper, ModalManager, ConfirmModal, `useModal`, `useConfirmModal` | Built on `vue-final-modal` (a peer dependency) and a global modal store. design.md §4.3 requires a focus trap, Esc and scrim close, bottom sheet on mobile, and opening only through `useModal()` | Decide: build on the native `<dialog>` element (focus trap and Esc for free) or adopt `vue-final-modal`; then port `useModal` and ConfirmModal with the decision |
| MultipleSelect | 311 lines, custom listbox without proper roles, `filter` and `default` variants, raw colours | Rebuild as a combobox/listbox with chips; port when the first filter or category screen needs it |
| DatePicker, DateTimePicker, TimePicker | 250 to 420 lines each, hand-built pickers, raw colours, no keyboard model | Port after deciding on a date library and the vi/en week and month names; needed for coupons and scheduling |
| FileInput, Dropzone | `FileInput` (494 lines) mixes UI with upload logic and `vue-i18n`; `Dropzone` wraps the `dropzone` library | Build one uploader to design.md §4.2 states (idle, drag-over, uploading, processed, error); the upload call must go through our API client in the app, not in the component |
| CountDown | Reference timer with per-digit boxes, uses fixed brand classes | Port if flash-sale or OTP countdown is needed; `useOtpTimer`/`useCooldown` cover the logic |
| Table (`tables/basic-tables/BasicTablesOne`) | A demo table with hard-coded rows and raw colours; no sorting, empty or loading states | Write a real `Table` with the data-view states (design.md §4.4) with the first list screen (product list) |
| Pages `Maintenance`, `NotFound`, `ServerError` | Use `vue-i18n` and a fixed illustration set | Our `ErrorPage` already covers 404, 500 and maintenance and is now built on `Button` (done). Optional later: reuse the recoloured error SVGs from `assets/images/error` |
| `AuthLayout`, `FullScreenLayout` | Sign-in screens are not in this phase (FEAT-001); they use the reference's decorative shapes | Port with the first auth screen (FEAT-001 T12: shared `AuthLayout`, used by sign-in, register and verify) |
| Composables `useDebounce`, `useCooldown`, `useOtpTimer`, `useFieldValidation`, `useValidationRules`, `useAsyncAction`, `useFormatDate`, `useMoneyInput`, `useMoneyFormatter`, `useNumberInputFormatter` | Useful, but money and date formatting overlap `@hivespace/core` (money, time) and the amount-plus-currency rule in dev-standards §5 | Compare each with `web/packages/core` before porting; keep one implementation |

## 3. Not suitable (skipped)

| Item | Reason |
| :-- | :-- |
| `useAuth`, `utils/api.ts`, `features/auth`, `features/user-profile`, `features/user-settings`, `stores/app.store`, `utils/cookie`, `pending-verification-email` | Old backend (OIDC, axios, cookies). Our API client is generated and imported only in `api.ts`; sign-in arrives with its own slice |
| `NotificationPreviewToast`, `useNotificationHub`, `features/notifications` (SignalR) | The SRS has no in-app notification centre; toasts cover action feedback |
| `features/payments`, `GoogleAuthButton` | Payment and Google sign-in come from our own services and flows; the button's look can be built with the Button component when sign-in is built |
| `features/media-upload` | Our media service and upload flow differ (FR-MEDIA); see FileInput above |
| `charts/BarChart`, `LineChart` (apexcharts) | design.md says the seller dashboard has no charts for now; adds a heavy dependency |
| `CommonGridShape`, `SidebarWidget`, `ThreeColumnImageGrid`, `TwoColumnImageGrid`, `ResponsiveImage`, `YouTubeEmbed`, `Link` (preventDefault anchor), `v-click-outside`, `ComponentCard` demo styling | Template decoration or one-line wrappers (`<img class="w-full">`); `Link` blocks navigation, we use a `linkAs` prop instead |
| `icons/` (87 custom SVG components) | design.md allows lucide icons only; map the few needed to lucide names |
| Fonts | The reference's Outfit is now used (self-hosted via `@fontsource-variable`), see ADR-0021 |

## 4. Decisions and recommendations

| # | Question | Status |
| :-- | :-- | :-- |
| 1 | Modal | **Decided: native `<dialog>`, recorded in [ADR-0022](adr/0022-modal-on-native-dialog.md) with the triggers to review it.** Opened with `showModal()`, it gives the focus trap, inert background, Esc and a top layer (no z-index fights) for free, with no dependency. Port the reference's promise-style `useModal()` on top of it so callers keep `await openModal(...)`. Handle the two design.md rules by hand: block Esc (the `cancel` event) and scrim click while a submit is running, and restyle as a bottom sheet below `sm`. ConfirmModal follows (title, body naming the object, Keep plus danger buttons) |
| 2 | Date and time pickers | **Decided: native inputs first, recorded in [ADR-0023](adr/0023-native-date-and-time-inputs.md).** Details: start with native `<input type="date" / "time" / "datetime-local">` inside our Input styling.** They are accessible, localised by the browser and good on mobile, and cost no code; the first need is coupon start and end dates. Format dates for display with `Intl.DateTimeFormat` through `@hivespace/core`. If design later needs a custom calendar, use a headless library (Reka UI date components) rather than the reference's hand-built pickers or flatpickr |
| 3 | Font | **Decided: Outfit** with Noto Sans as the Vietnamese fallback (changed from Inter on 2026-11-18, see [ADR-0021](adr/0021-design-tokens-and-theming.md)) |
| 4 | `ErrorPage` on `Button` | **Done**: `Button` accepts `href` and renders a link with the button look; `ErrorPage` uses it |
