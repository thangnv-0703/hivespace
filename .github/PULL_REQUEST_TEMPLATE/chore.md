## Why
<!-- Refactor, technical debt, CI, docs, perf, dependency update: the reason for the change. -->

## What changed
<!-- Scope in 2-4 bullets. -->

## Behaviour change
<!-- "None" for a refactor, and how you know (tests green before and after). Otherwise describe it. -->

## Requirements / decisions
<!-- NFR IDs, ADR link, issue link if any. -->

## How it was verified
<!-- Commands run and results. -->

## Checklist (dev-standards §4.4)
- [ ] No behaviour change, or the change is described above and tested.
- [ ] Existing tests pass unchanged (none weakened or skipped).
- [ ] OpenAPI changed first, if the API changed (§6).
- [ ] No secret, no bare money value, no local-time timestamp introduced.
- [ ] Docs updated if needed (README, ADR, dev-standards, RTM).
