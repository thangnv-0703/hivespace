## Summary
<!-- What the feature adds, in 2-4 bullets. -->

## Requirements / design
- Feature: FEAT-NNN
- FR / UC IDs:
- Design: `docs/features/FEAT-NNN-<slug>/technical-design.md`, `tasks.md`, `ui-design.md`

## Changes by area
<!-- contracts, services, web, docs, migrations -->

## How it was verified
<!-- Done-when commands run per task and their results; screenshots for UI. -->

## Checklist (dev-standards §4.4)
- [ ] The cited FR/NFR IDs are fully met, not partly.
- [ ] OpenAPI changed first, if the API changed (§6), and the breaking-change check passes.
- [ ] A Gherkin scenario exists or is updated for each changed endpoint behaviour (§9).
- [ ] Mandatory risk tests (§9.2) pass if the change touches their area.
- [ ] New user-facing strings exist in both `vi` and `en`.
- [ ] No secret, no bare money value, no local-time timestamp introduced.
- [ ] Docs updated: README run steps, ADR if a decision was made, RTM if requirement status changed.
