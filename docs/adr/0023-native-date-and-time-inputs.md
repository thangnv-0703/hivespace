# ADR-0023: Date and time fields use native inputs first

**Status:** Accepted (review when a trigger under "Revisit" fires)
**Date:** 2026-11-18
**Requirements addressed:** NFR-USE-003, NFR-L10N-001..003 (vi and en), EIR-UI-003

## Context
The reference has hand-built `DatePicker`, `DateTimePicker` and `TimePicker` components (250 to 420 lines each) with no keyboard model and raw colours. The first real need in our scope is coupon start and end dates. See [shared-ui-review.md](../shared-ui-review.md) §2.

## Decision
- Use `<input type="date">`, `type="time"` and `type="datetime-local"` styled with our `Input` (same height, border, focus and error rules as design.md §4.2). Add a small `DateField` wrapper in `web/packages/ui` when the first screen needs it.
- Display dates elsewhere with `Intl.DateTimeFormat` through `@hivespace/core` (time helpers). Values travel as ISO 8601 strings.
- Do not port the reference pickers and do not add flatpickr.

## Options considered
- **Native inputs (chosen).** Accessible and localised by the browser, good native pickers on mobile, nearly no code.
- **Headless calendar (Reka UI date components).** Accessible and fully styled by us; the choice if a custom calendar is needed.
- **flatpickr, as the reference peer dependency.** Familiar, but weaker accessibility and a styling layer to maintain.
- **Port the reference pickers.** Most code to own, with the least accessibility.

## Consequences
- **Easier:** no dependency; screen readers and mobile work out of the box; locale follows the browser.
- **Harder:** the picker popup looks different per browser; no range selection or disabled-day rules; browser language, not the app language, decides the picker's month names.
- **Revisit** if: design needs a date range, disabled days or a calendar view; the cross-browser look of the picker is not acceptable; or the picker must follow the app locale rather than the browser. Then move to the headless calendar option behind the same `DateField` wrapper.

## What it teaches
Native form controls versus custom widgets, ISO 8601 date handling and time zones, `Intl.DateTimeFormat`, and how a thin wrapper lets the implementation change later.
