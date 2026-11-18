# ADR-0021: Design tokens (DTCG) and a Tailwind v4 theme with light and dark modes

**Status:** Accepted
**Date:** 2026-11-16
**Requirements addressed:** EIR-UI-001..004, NFR-USE-002, NFR-USE-003, NFR-L10N-001..003, FR-LOC-001

## Context
Two web apps, the storefront and the seller centre (repo-structure §8), must look like one product, work at 360 px, meet WCAG 2.1 AA on the hero flow, and support Vietnamese and English. The UI reference `references/hivespace.web` uses Tailwind v4 with raw palette utilities and three competing primaries: indigo `#465fff` in the seller and admin apps, `blue-600` in the storefront, and hardcoded Shopee-style reds in the product card. Its shared theme source (`packages/`) is missing from our copy, so its values had to be read from compiled CSS. UI code will be written largely by coding agents, which need concrete, machine-readable values and rules.

## Decision
- **Canonical tokens in `docs/tokens.json`, in W3C DTCG format.** Primitive scales (brand, gray, success, error, warning, info) plus **semantic role tokens** (`bg.surface`, `text.muted`, `action.primary.bg`, `feedback.error.fg`, `commerce.price`, `focus-ring`), defined once per theme (`theme.light`, `theme.dark`) as aliases of primitives.
- **Rendering:** a Tailwind v4 CSS-first theme (`@theme` for primitives and scales, `@theme inline` mapping roles to `--hs-*` variables, `.dark` class overrides). It lives in `web/packages/ui/src/theme.css` and is imported by both apps. The CSS is generated from tokens.json and embedded in `docs/design.md`, which is the render-time guide agents load.
- **Brand:** the indigo scale (`#465fff`) is the only brand colour. Red is reserved for prices, errors and destructive actions.
- **Themes:** light and dark from the start, class-based (`.dark` on `<html>`), defaulting to `prefers-color-scheme`.
- **Accessibility:** every role pairing is checked by script against WCAG 2.1 contrast (4.5:1 for text, 3:1 for UI). Control borders were darkened from the reference gray-300 to gray-500 to pass 3:1.
- **Font:** Outfit as the primary family, self-hosted, with Noto Sans as the fallback for Vietnamese diacritics (Outfit has no Vietnamese subset). Changed from Inter on 2026-11-18; see the review note below.

## Options considered
- **DTCG tokens with semantic roles and a Tailwind v4 theme (chosen).** One source of truth, theme switching by swapping the role layer, and a standard format that design tools and Style Dictionary can read.
- **Copy the reference CSS as-is.** This is the fastest, but it carries over three primaries, raw palette utilities (no theming layer) and inputs that fail 1.4.11.
- **A component library with its own theme (PrimeVue, Vuetify).** Less to build, but it fights the reference's Tailwind look and teaches less about design systems.

## Consequences
- **Easier:**
  - Dark mode is mostly free.
  - Agents get exact values and rules.
  - Contrast is verified rather than guessed.
  - Both apps stay consistent through `packages/ui`.
- **Harder:**
  - Role utilities read a little redundantly (`bg-bg-surface`, `text-text-muted`).
  - Two themes double visual QA, so visual checks are limited to hero-flow screens (R-1).
  - tokens.json, the CSS block and `theme.css` must be regenerated together. Do not edit the CSS by hand.
- **Revisit** if a design tool becomes the source of truth. Its variables can then feed tokens.json.
- **Review 2026-11-18:** the font was changed from Inter to the reference's Outfit, to match the reference look. Trade-off: Outfit has no Vietnamese subset, so letters with diacritics render in Noto Sans and the two faces mix within a word. Revisit if that looks poor in review (for example Be Vietnam Pro or Inter, which both cover Vietnamese). The expanded sidebar is 260 px (measured from the reference app) instead of the 290 px template default.

## What it teaches
Design tokens as a contract between design and code; the W3C DTCG format and alias resolution; primitive vs. semantic token layers; Tailwind v4 CSS-first theming (`@theme`, `@theme inline`, custom variants); class-based dark mode; and measuring WCAG contrast instead of eyeballing it.
