## Bug
<!-- What goes wrong, who or what is affected, how to reproduce. -->

## Root cause
<!-- Why it happens. -->

## Fix
<!-- What changed, in 1-3 bullets. Nothing unrelated. -->

## Requirements
<!-- FR/NFR IDs the behaviour belongs to; issue link if any. -->

## Regression test
<!-- The test that failed before the fix and passes now (name or path). -->

## How it was verified
<!-- Commands run and results. -->

## Checklist (dev-standards §4.4)
- [ ] A test reproduces the bug and now passes.
- [ ] The cited FR/NFR IDs are fully met, not partly.
- [ ] OpenAPI changed first, if the API changed (§6), and the breaking-change check passes.
- [ ] Gherkin scenario updated if endpoint behaviour changed (§9).
- [ ] Mandatory risk tests (§9.2) pass if the change touches their area.
- [ ] No secret, no bare money value, no local-time timestamp introduced.
- [ ] Docs updated if needed (README, ADR, RTM).
