# Verification (Self-Check Before Delivery)

Phase 5's contract: mechanical conformance of this feature's design and tasks,
checked before delivery. Fix failures; flag the unfixable in the summary —
never ship silently defective design. (Per-skill layer of the pipeline's
verification approach; the future pipeline-wide checker composes with this.)

## 1. Requirement coverage

- feature-spec.md exists with `Status: Agreed` and a `Signed off` date (the
  Phase 1 gate held).
- Every `AC-*` in feature-spec.md has a row in the §6 mapping with at least
  one technical check, its wording untouched; no AC was added here (new business
  criteria go back to clarify-feature). Every `BR-*` is enforced somewhere in
  §3–§5.
- Every FR the plan assigned to this feature is covered by at least one
  acceptance criterion **and** at least one task. None missing from either.
- Every UC flow the feature realizes (main + consequential alternates/exceptions)
  maps to contract behavior (§3) and acceptance criteria (§6).
- Binding NFRs appear as concrete contract/design properties, cited by ID.
- **E2E obligation honored**: if this feature's UCs intersect the
  architecture's named critical flows, tasks.md contains the E2E-extension
  task (flow-aware, before final verification); if the architecture is silent
  on testing, the skip is legitimate.
- Nothing in the design lacks a requirement or code-reality driver behind it
  (gold-plating check).

## 2. ID integrity

- Every cited US/AC/BR ID exists in feature-spec.md.
- Every cited FR/UC/SCR/NFR/ADR ID exists in its source document; no
  tombstoned requirement or screen is served. Source-gating respected where
  documents are absent.
- The feature's folder name matches its plan-sequence position and name.

## 3. Schema conformance

- Every schema change maps to an entity the architecture's conceptual model
  owns. Any new entity / boundary / ownership change has a filed escalation in
  §8 — not a local workaround.
- Migrations are expressed against the *current* schema state (the migration
  history read in Phase 2), not against the skeleton or an assumed state.

## 4. Codebase consistency

- Contracts follow the surveyed conventions (envelope, naming, errors,
  pagination). Any deliberate divergence is a §7 decision with a driver.
- No collision: new endpoints/tables don't conflict with existing ones or
  with prior feature designs in `docs/features/`.
- Breaking changes to prior features' contracts/schema are recorded as decisions
  with a compat/migration story.

## 5. Tasks soundness

- Order respects dependencies (schema → domain → contract → wiring → UI
  integration → E2E when owed → verify); every task has a checkable done-when; every task
  points at its design section; the final task is acceptance verification.
- Executing tasks.md top-to-bottom would produce the design — no design
  element lacks a task, no task lacks a design basis.

## 6. Write-backs and hygiene

- RTM Design ref appended (when rtm.md exists) for every FR the feature
  implements — append-only, Design ref column only.
- Any Mermaid in technical-design.md parses (quoted labels, no bare `end`).
- §8 lists every escalation/open item raised during design — nothing raised in
  conversation is missing from the document.

## 7. Design-flow diagram (checked at the end of Phase 6)

- `design-flow.html` and `design-flow.model.json` sit in the feature folder and
  the build reports zero `WARN` (no `ERROR`).
- Each flow button matches a UC path the design covers in §3/§6; every arrow
  that crosses a boundary has a `data` contract consistent with §3.
- §7 decisions and §8 escalations/open items appear as notes; nothing in the
  diagram contradicts technical-design.md.
- technical-design.md's header links the diagram.
- If the `html-flow-diagram` skill was unavailable, the summary says the
  diagram was skipped.

## Reporting

One line in the delivery summary: "verification clean" or the flagged list
("escalation pending: new entity Invoice — architecture amendment proposed").
The user never discovers a gap the skill knew about.
