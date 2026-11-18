# ADR-0024: Port a UI subset from the reference shared package instead of consuming it

**Status:** Accepted (review at the start of the next UI-heavy feature)
**Date:** 2026-11-18
**Requirements addressed:** NFR-USE-003, EIR-UI-001..004

## Context
The reference app's `@hivespace/shared` (Vue 3 and Tailwind v4) was copied to `references/hivespace.web/packages/shared`. It has many reusable components, but it is coupled to the old backend (OIDC, axios, SignalR, cookies, a global store), targets older versions (pinia 3, vue-router 4, TypeScript 5.7, Vite 6, Jest) and uses raw colours, the Outfit font and custom icons. Our rules: the API client is generated and imported only in `api.ts` (Sheriff), the design system uses semantic tokens, and icons are lucide only.

## Decision
- Port the presentational components into `web/packages/ui`, one at a time and re-styled: semantic tokens, labels and links passed in (no i18n, router, store or HTTP in the package), lucide icons, Vitest tests.
- Do not depend on the reference package.
- Record each component as ported, needing adaptation, or skipped in [shared-ui-review.md](../shared-ui-review.md).
- Use the reference's Outfit typeface (see ADR-0021), self-hosted with Noto Sans as the Vietnamese fallback.

## Options considered
- **Port a subset (chosen).** Each component fits our tokens, accessibility rules and toolchain, and the team understands all of it.
- **Use the package as-is.** Fastest, but pulls in the old backend code and clashes with our boundaries and versions.
- **Build from scratch.** Cleanest, but ignores working components and the look the team wants.

## Consequences
- **Easier:** consistent look and behaviour in both apps; no hidden coupling; checks enforce the boundaries.
- **Harder:** upstream changes in the reference do not flow in; every port costs adaptation and tests.
- **Revisit** if the reference package is extracted into a clean, versioned library without backend code, or if the number of ported components makes a library with an upstream (for example a headless one) cheaper to maintain.

## What it teaches
Reading and adapting an existing component library, separating presentation from data access, and keeping a package boundary clean with tests and lint.
