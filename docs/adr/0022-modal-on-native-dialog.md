# ADR-0022: Modals are built on the native `<dialog>` element

**Status:** Accepted (review when a trigger under "Revisit" fires)
**Date:** 2026-11-18
**Requirements addressed:** NFR-USE-003 (WCAG 2.1 AA on the hero flow), EIR-UI-003 (works at 360 px)

## Context
`docs/design.md` §4.3 requires every modal to trap focus and return it on close, close on Esc and scrim click (except while a submit is running), become a bottom sheet below `sm`, and be opened only through `useModal()`. The reference `@hivespace/shared` builds this on `vue-final-modal` plus a global modal store (`ModalManager`, `ModalWrapper`, `useModal`). Porting that adds a peer dependency and a second place where focus and layering rules live. See [shared-ui-review.md](../shared-ui-review.md) §2.

## Decision
- Build `Modal` and `ConfirmModal` in `web/packages/ui` on the native `<dialog>` element opened with `showModal()`.
- Keep the reference's calling style: a promise-based `useModal()` (`await openModal(Component, props)`; the component resolves it with `closeModal(result)`), written on top of `<dialog>`. Callers do not change if the internals change.
- Handled in our code: ignore Esc (the `cancel` event) and scrim clicks while a submit is in progress; restyle as a bottom sheet below `sm`; widths 400, 560 and 720 px per design.md.

## Options considered
- **Native `<dialog>` (chosen).** The browser provides the focus trap, an inert background, Esc, and the top layer (no z-index fights), at no dependency cost.
- **`vue-final-modal` as in the reference.** Less code written, but a dependency whose accessibility we must verify, and its own layering. Kept as the fallback.
- **A headless library (Reka UI Dialog).** Accessible and animated, but another dependency for something the platform now does.

## Consequences
- **Easier:** no dependency; accessibility behaviour comes from the platform; nested layering works.
- **Harder:** enter and exit animation needs `@starting-style` or a small transition helper; the bottom-sheet look is our own CSS; jsdom does not implement `showModal()`, so component tests stub it and the real behaviour is checked in Playwright.
- **Revisit** if: a supported browser lacks the features we rely on; animation or stacking needs outgrow our helper; or a nested modal (modal over modal) becomes common.

## What it teaches
The `<dialog>` element and the top layer, `inert`, and focus management; wrapping an imperative browser API in a promise-based composable; testing browser behaviour that jsdom does not implement.
