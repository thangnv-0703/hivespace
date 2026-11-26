---
name: detailed-design
description: >-
  Produces the per-feature technical design — the second skill in the
  construction loop, run once for each vertical slice after clarify-feature has
  produced a signed-off feature-spec.md (Status: Agreed); it refuses to start
  without one. Reads the agreed feature-spec.md (user stories, business rules,
  business acceptance criteria), the feature's plan entry, the FR/UC
  IDs it implements, the architecture's conceptual domain model and ADRs, and
  the live codebase (contracts and schema earlier features already established),
  then designs the feature concretely: API contracts (endpoints,
  request/response shapes, error codes), the physical schema changes
  (migrations — architecture owns the conceptual model; new entities escalate
  to an architecture amendment, never local invention), component-level
  design, a mapping of each business acceptance criterion to its technical
  checks, an ordered tasks.md breaking the feature
  into implementable tasks, and an interactive HTML flow diagram of the
  feature's design solution (via the html-flow-diagram skill). Use per
  feature after clarify-feature's sign-off, before ui-design and
  implementation. Trigger on "design this feature", "detailed
  design for", "design the API for this feature", "create the technical
  design", or "design the next feature". Hands contracts to ui-design.
---

# Detailed Design (per feature)

The second **loop skill**: it runs once per vertical slice, for the life of the
project, after `clarify-feature` has produced a signed-off `feature-spec.md`,
turning the feature's agreed business spec into a buildable technical design —
the backend/system half of the feature's low-level design (`ui-design` is the
presentation half, and consumes this skill's contracts).

Three principles govern it:

1. **The feature's *what* is fixed upstream; this skill designs the *how*.** The
   FR/UC IDs the feature implements were assigned and verified in requirements;
   the plan chose and scoped the feature; `clarify-feature` settled its user
   stories, business rules and business acceptance criteria with the user. This
   skill never invents or reinterprets requirements — that inheritance-by-ID is
   exactly what keeps per-feature design consistent at scale. A business
   question found while designing goes back to `clarify-feature`; it is not
   answered here.
2. **The live codebase is an input, not an obstacle.** Feature N is designed
   months after feature 1, in a fresh session, against code that has evolved past
   the skeleton. Design against *reality*: the contracts, schema migrations,
   and conventions earlier features established. A design that conflicts with
   existing code is wrong even if it matches the documents. **One exception:
   written conventions.** Where a layout or coding rule (repo-structure,
   dev-standards, ADRs) and older code disagree, the rule wins for new code.
   Record the mismatch in §2, then add a task that fixes the files the feature
   touches, or ask the user. Never design new code into the old pattern.
3. **Depth here, and only here.** This is where the pipeline's
   depth-on-demand promise is kept: full concrete detail for *this* feature —
   contracts, columns, components, tasks — and nothing for features that haven't
   reached the front.

## Inputs

Defaults below; user-provided paths win; source-gated citation throughout
(cite FR/UC/SCR/ADR IDs only when their documents exist; never fabricate).

- **Feature spec** — `docs/features/FEAT-NNN-<slug>/feature-spec.md`, required
  and signed off (`Status: Agreed`). The primary source for scope, user
  stories (`US-*`), business rules (`BR-*`), business acceptance criteria
  (`AC-*`), out of scope, and the "For detailed design" questions this skill
  must decide.
- **Implementation plan** — `docs/implementation-plan.md`. Which feature is next
  (or the user names one), its traced touchpoints: FRs implemented, UCs
  realized, SCR screens, building blocks/endpoints, data.
- **SRS** — `docs/srs.md`. The verbatim FR statements (they become acceptance
  criteria) and relevant NFRs (they constrain the design — cite them).
- **Use cases** — `docs/use-cases.md`. The UC flows the feature realizes —
  main/alternate/exception paths drive endpoint behavior and error design.
- **Architecture** — `docs/architecture.md`. The conceptual domain model
  (entity ownership, consistency boundaries), the ADRs constraining this
  feature, cross-cutting concepts (auth, error handling, observability
  conventions the design must follow).
- **Written conventions** — mandatory when the project has them:
  `docs/repo-structure.md` (layout of the touched stack and service shape) and
  `docs/dev-standards.md` (rules and what enforces each). Read both before
  naming any new file, folder or component.
- **The live codebase** — mandatory read, not optional: existing route/contract
  patterns, the current schema and migration history, module boundaries,
  shared utilities, conventions in `docs/scaffold-notes.md` and the
  agent-instructions file. Prior feature designs live in `docs/features/`.
- **UX foundations** — `docs/ux-foundations.md`, light touch: the SCR screens'
  stated purpose/states, as context for what the contracts must serve.

## Outputs

Per feature, into **`docs/features/FEAT-NNN-<slug>/`** — keyed on the
feature's **stable FEAT ID from the plan** (immutable, so re-sequencing the
plan never invalidates folder names), with the slug from its name for human
readability — e.g., `docs/features/FEAT-004-sign-in/`:

1. **`technical-design.md`** — the feature's technical design: contracts,
   schema changes, component design, acceptance criteria, decisions. (Named
   `technical-design.md`, not `design.md`, to avoid colliding with the design
   *system* at `docs/design.md`; its ui-design sibling will sit beside it in
   the same folder.) See `references/design-guide.md`.
2. **`tasks.md`** — the ordered, implementable task breakdown. See
   `references/tasks-guide.md`.
3. **`design-flow.html` + `design-flow.model.json`** — an interactive flow
   diagram of the feature's design solution, built with the
   **`html-flow-diagram`** skill (model and HTML saved side by side so it can be
   rebuilt). It shows the components the feature touches inside their
   boundaries (UI, gateway/BFF, services, stores, broker, externals); every
   arrow that crosses a boundary carries a `data` contract taken from the API
   contracts; one flow button per main UC path plus the consequential
   exception paths; notes from the Decisions (`decision`) and Escalations &
   open items (`risk` / `consider`) sections. Mermaid in technical-design.md
   stays optional; this diagram is the end-to-end picture. Produced only when
   the `html-flow-diagram` skill is available — otherwise skipped and said so
   in the delivery summary.

Plus two write-backs:
- **RTM** (`docs/rtm.md`, if present; skip silently otherwise): **append** the
  feature's design ref (e.g., `features/FEAT-004-sign-in/technical-design.md`) into the **Design
  ref** cell of every FR this feature implements — append, never overwrite;
  Design ref column only, per the RTM ownership contract.
- **Architecture amendment escalation** (when triggered — see design-guide):
  a needed new entity or changed boundary is *not* designed locally; it's
  surfaced as a proposed architecture amendment and the feature design waits on
  or explicitly notes it.

## Workflow

### Phase 1 — Locate the feature and ingest

Identify the feature: the user names it (by FEAT ID or name — explicit naming
is the only legitimate off-sequence path), or default to
**the next undesigned feature — computed, not stored**: the earliest feature
in plan build-sequence order whose `docs/features/FEAT-*` folder has no
`technical-design.md` (a folder may exist holding only `feature-spec.md`). Multiple
candidates is not ambiguity — the sequence resolves it (earliest in build
order wins); never present a menu. **Announce the resolution** ("FEAT-004 is
next per the plan's build sequence") so the user can redirect before work
starts.

**Gate — signed-off spec required.** Read the feature's `feature-spec.md`. If
it is missing, or its header is not `Status: Agreed` with a `Signed off` date,
stop: tell the user the feature has no signed-off business spec and to run
`/clarify-feature FEAT-NNN` first. Do not draft a design, do not clarify
business questions here. (Features designed before clarify-feature existed
already have their `technical-design.md` and are not re-gated.)

Execution status
lives in the artifacts (a folder with a verified technical-design.md =
designed; its tasks.md checkboxes = build progress) — the plan itself is never
written with status; it stays the structural source owned by
implementation-planning. Take the feature's trace from `feature-spec.md`'s
header (FR, UC, SCR — clarify-feature already confirmed it with the user and
amended the SRS where needed), read the plan entry for blocks/endpoints and
data, fetch the verbatim FR statements and UC flows, and read the
spec's stories, business rules and acceptance criteria. Read the
architecture's domain model, the ADRs that touch this feature, and the
cross-cutting concepts.

If design work turns up a **business** question the spec does not answer (a
rule, a limit, what the user sees), do not decide it here: stop and send it
back to `/clarify-feature`, which records the answer and resets the spec to
Draft until the user signs off again. **Technical** forks stay here as §7
Decisions.

### Phase 2 — Read the code

Survey the live codebase before designing anything: existing endpoint and
contract conventions (naming, envelope shapes, error format), the current
schema and its migration history, the module the feature lands in and its
boundaries, shared utilities the design should reuse, and prior feature designs
in `docs/features/` (especially any this feature depends on). Record what the
design must conform to. **Reality wins over documents** where they diverge —
and note the divergence.

### Phase 3 — Design

Read `references/design-guide.md`. Produce, concretely:
- **API contracts** — endpoints with methods, paths, request/response shapes,
  validation rules, error codes; following the codebase's established
  conventions. UC exception/alternate flows become error behaviors.
- **Physical schema changes** — tables/columns/indexes/constraints as
  migrations, within the entities the architecture's conceptual model already
  owns. **A new entity or ownership/boundary change escalates to an
  architecture amendment — never invented locally.**
- **Component design** — the modules/services/functions inside the feature's
  building block: responsibilities, interactions, where the stubbed skeleton
  code gets replaced.
- **Acceptance criteria mapping** — every business `AC-*` from
  `feature-spec.md`, quoted by ID (never reworded), mapped to the technical
  checks that prove it (endpoint + status, error code, event, stored state),
  plus technical-only criteria the business spec doesn't state (binding NFRs,
  idempotency) with their own `TAC-*` IDs citing their NFR/ADR. These are what
  acceptance verification will later check.
- **Design decisions** — any real fork gets a short decision note with its
  driver (mini-ADR discipline; feature-local decisions stay here, architectural
  ones escalate).

### Phase 4 — Break down tasks

Read `references/tasks-guide.md`. Decompose the design into `tasks.md`: an
ordered list of implementable tasks, each small enough to complete and verify
in one focused session, each pointing at the design sections and acceptance
criteria it serves, ordered so the feature builds incrementally (schema →
domain → contract → wiring → tests green), with UI tasks referenced but owned
by ui-design's output.

### Phase 5 — Verify

Read `references/verification.md` and self-check: every `AC-*` in
feature-spec.md appears in the §6 mapping; every feature FR is covered by
acceptance criteria and by tasks; every cited ID resolves; the schema stays
inside the conceptual model (or an escalation is filed); contracts don't
collide with existing code; tasks are complete, ordered, and traceable. Fix
failures; flag the unfixable.

### Phase 6 — Diagram the design solution

When the `html-flow-diagram` skill is available, invoke it with the verified
technical-design.md as the source and produce `design-flow.html` +
`design-flow.model.json` in the feature folder (see Outputs, item 3). Draw the
design **exactly as written** — improvements belong in notes, not a silently
redrawn design. Follow that skill's workflow: build until zero `WARN`, then
screenshot and check each flow highlights the right steps. Add a
`> Diagram: [design-flow.html](design-flow.html)` line to technical-design.md's
header block. If the diagram exposes a gap in the design (a missing arrow, an
undefined contract), fix technical-design.md first and rebuild — the two must
tell the same story. If the skill is unavailable, skip this phase and say so in
the summary.

### Phase 7 — Deliver

Write the files, do the RTM append, and summarize: the design in a few
sentences, the task count and order, any escalations filed, the diagram path
and its flows (or that it was skipped), a **live-computed
progress line** ("FEAT-004 designed — 5 of 14 features now designed", counted
from the plan vs. `docs/features/`; never stored anywhere), and the handoff —
**ui-design** now designs the feature's SCR screens against these contracts;
implementation follows tasks.md. Offer to proceed to either.

## Scope boundaries

Does **not**: start without a signed-off `feature-spec.md`; write user stories,
business rules or business acceptance criteria, or answer business questions
(clarify-feature's job); amend the SRS or use cases;
design screens (ui-design's half — it consumes these contracts);
implement anything (tasks.md is the handoff to implementation); change
requirements or the plan (amendments belong to their owning skills); invent
entities or alter consistency boundaries (architecture amendment); write to
any RTM column except appending Design ref.

## What good looks like

- Every business `AC-*` from feature-spec.md is mapped to technical checks,
  word for word; every technical criterion cites its NFR/ADR; every task points at
  design sections; nothing in the design lacks a requirement behind it.
- The schema diff is migrations against the *actual current* schema, not the
  skeleton's — and it stays inside the conceptual model's entities.
- Contracts read like the codebase already reads — conventions inherited, not
  reinvented.
- tasks.md could be handed to a fresh session (or a loop agent) and executed
  top-to-bottom without re-deriving context.
- Escalations were filed, not worked around.
