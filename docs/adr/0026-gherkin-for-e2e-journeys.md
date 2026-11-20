# ADR-0026: Gherkin for the end-to-end user journeys (playwright-bdd)

**Status:** Accepted
**Date:** 2026-11-20
**Requirements addressed:** NFR-MNT-005, EIR-UI-003; traces UC-001, UC-002 and their FR tags

## Context
API contracts run as Gherkin on cucumber-js (ADR-0025). The end-to-end suite in `tests/e2e` was plain Playwright, so the hero flow (register, email, verify, sign in, sign out) was readable only as code, and its requirement links lived in comments. The same suite also holds technical checks (design tokens, 360 px overflow, 404 page) where a scenario adds no value.

## Decision
- **User journeys are Gherkin** in `tests/e2e/features/`, tagged with the use case and requirement IDs (`@UC-001 @FR-AUTH-005`), so the RTM Test ref column can be fed the same way as for the API features. The steps are in `tests/e2e/steps/`.
- **Runner: playwright-bdd.** `bddgen` compiles the `.feature` files into Playwright tests (`.features-gen/`, ignored by git) and Playwright runs them, so projects (desktop and 360 px), traces, retries and the AppHost `webServer` stay as they were. The `pnpm test` script runs `bddgen && playwright test`.
- **Technical checks stay plain Playwright** in `tests/e2e/specs/` (`skeleton.spec.ts`, `layout.spec.ts`): a scenario for "the brand colour is #465fff" would only wrap code in prose.
- **Location:** e2e features live beside their runner, not in `contracts/features/`, which stays one feature per API operation.
- **State shared between steps** (the registered address, the verification link) lives in a Playwright fixture (`support/fixtures.ts`). The Mailpit polling helper is `support/mailpit.ts`.

## Options considered
- **playwright-bdd (chosen)** — keeps Playwright's runner features; Gherkin only changes how tests are written.
- **cucumber-js with Playwright as a library** — one runner for API and UI, but projects, traces and the AppHost start-up would be rebuilt by hand.
- **Plain Playwright only** — nothing new to learn, but the journeys stay unreadable to non-developers and untagged.

## Consequences
Easier: journeys read as the use cases they verify; tags give traceability. Harder: one extra generation step, and step wording must stay consistent across features. At 360 px the header collapses, so steps open the menu when it is collapsed.

**What it teaches:** Gherkin over a real browser, and where a scenario earns its keep (a user journey) and where it does not (a technical check).
