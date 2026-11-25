---
name: clarify-feature
description: >-
  Clarifies a feature's business requirements before any technical design —
  the first step of the per-feature loop, run before detailed-design. Reads the
  feature's plan entry, its FRs from the SRS, its use-case flows and its
  screens' purpose, drafts user stories with business acceptance criteria and
  business rules, then runs a two-way Q&A: the agent asks the user the open
  business questions, and the user can ask the agent questions too. When an
  answer adds or changes a requirement, it amends the SRS and use cases on the
  user's approval. Writes docs/features/FEAT-NNN-<slug>/feature-spec.md and
  ends with an explicit user sign-off (Status: Agreed); detailed-design does
  not start without it. Business only — no endpoints, tables or technology.
  Trigger on "clarify FEAT-N", "clarify the next feature", "write the user
  stories for", "what should this feature do", or /clarify-feature.
---

# Clarify Feature (per feature, business only)

The first **loop skill**: it runs once per feature, before `detailed-design`,
and turns the plan's feature entry into an agreed business spec — user
stories, business rules and business acceptance criteria — that the user has
signed off. `detailed-design` then designs only the *how* from that spec.

Principles:

1. **Business only.** Who, what, why, rules, limits, what the user sees, what
   is out of scope. No endpoints, tables, services, libraries or other
   technical choices — those belong to `detailed-design`. If a technical
   question comes up, note it in the spec's "For detailed design" list and move
   on.
2. **Ask, don't assume.** Every gap the documents leave open becomes a
   question to the user, not a silent default. The user can ask questions too;
   answer them from the documents with IDs, or say plainly that it is not
   decided yet and turn it into a question.
3. **The SRS stays the source of truth.** When an answer adds or changes a
   requirement, amend `docs/srs.md` / `docs/use-cases.md` (with the user's yes)
   rather than letting the spec drift from the SRS.
4. **Only the user signs off.** The spec is `Draft` until the user explicitly
   agrees to it in chat. The agent never sets `Agreed` on its own.

## Inputs

Defaults below; user-provided paths win; cite IDs only when their documents
exist, never fabricate.

- **Implementation plan** — `docs/implementation-plan.md`: the feature entry,
  its FR/UC/SCR trace and value statement.
- **SRS** — `docs/srs.md`: the verbatim FR statements and priorities; NFRs only
  where they state a business rule (for example a limit the user sees).
- **Use cases** — `docs/use-cases.md`: the UC flows (main, alternate,
  exception) the feature realizes.
- **UX foundations** — `docs/ux-foundations.md`: the SCR screens' purpose and
  the personas.
- **Earlier specs** — `docs/features/*/feature-spec.md` (and older features'
  `technical-design.md` §1/§6 for features without a spec) that this feature
  depends on, so rules stay consistent.

Not read: the codebase, ADRs, contracts. This step does not need them.

## Output

**`docs/features/FEAT-NNN-<slug>/feature-spec.md`** — same folder rule as
detailed-design (stable FEAT ID from the plan, slug from its name). Layout in
`references/spec-template.md`.

Write-backs, only when the Q&A changes a requirement and the user approves:
`docs/srs.md`, `docs/use-cases.md` and `docs/rtm.md` (new rows only), following
`../requirements-engineering/references/change-management.md`.

## Workflow

### Phase 1 — Locate the feature

The user names it (FEAT ID or name), or default to the **earliest feature in
the plan's build sequence whose folder has no `feature-spec.md`** and no
`technical-design.md` (features designed before this skill existed are done).
Announce the resolution ("FEAT-010 is next per the plan's build sequence") so
the user can redirect.

If a `feature-spec.md` already exists: `Draft` → resume it (re-read the Q&A
log, continue at Phase 4); `Agreed` → the user wants a change, so continue at
Phase 4 and set the status back to `Draft` (see Phase 7).

### Phase 2 — Ingest

Read the plan entry and pull the full trace: FR statements and priorities, UC
flows, SCR purpose, dependencies on earlier features and their specs. If the
plan entry is missing touchpoints, derive the missing FR/UC/SCR trace from the
documents and include it in the first question round for the user to confirm.

### Phase 3 — Draft

Read `references/question-guide.md`. Draft, in memory:
- user stories (one per distinct actor goal, each citing its FR/UC);
- business acceptance criteria per story (Given/When/Then, business terms);
- business rules the documents already state;
- the list of **open questions**: every gap the question guide's areas expose
  that the documents don't answer.

### Phase 4 — Clarify (two-way)

Run rounds until no open question is left:
1. Ask up to 4 questions per round with AskUserQuestion. Each question stands
   on its own (names the story or rule it is about), gives 2–4 concrete
   options, recommended option first and marked "(Recommended)", with a short
   business reason.
2. After each round, tell the user in one line they can ask their own
   questions now. Answer each one from the documents, citing IDs; if the
   documents don't settle it, say so and add it to the open questions.
3. Record every question and answer (both directions) in the Q&A log, and
   update the drafted stories, rules and criteria.

Stop asking when the remaining unknowns are technical (move them to "For
detailed design") or the user explicitly defers them (move them to "Out of
scope" or "Open, deferred" with the user's words).

### Phase 5 — Amend the SRS when needed

If an answer adds, changes or removes a requirement or a use-case step, show
the proposed edit (ID, before → after) and apply it only on the user's yes,
following `../requirements-engineering/references/change-management.md`:
stable IDs, new IDs for additions, tombstones for removals, revision-history
row and version bump in `docs/srs.md`, the use case updated, a new RTM row for
a new FR. List every amendment in the spec's §6. Never change architecture,
ADRs or technical NFRs here.

### Phase 6 — Verify

Self-check, fix failures before showing the user:
- Every FR the plan assigns to this feature (plus any added in Phase 5) is
  covered by at least one user story.
- Every story has at least one acceptance criterion; every criterion is
  checkable by observing what the user sees or what the business state
  becomes.
- Every cited FR/UC/SCR/BR ID resolves; no tombstoned requirement is served.
- No technical content: no endpoint, table, column, service, queue, library or
  framework name in §1–§4.
- Every question raised in the conversation is in the Q&A log, answered or
  deferred — none silently dropped.

### Phase 7 — Write as Draft, then sign-off

1. Write `feature-spec.md` with `Status: Draft` and show a short summary:
   stories, business rules, number of acceptance criteria, out of scope, SRS
   amendments, items left for detailed design.
2. Ask for sign-off with AskUserQuestion: **"Agree"** / **"Change
   something"**. Only an explicit "Agree" from the user sets `Status: Agreed`
   and `Signed off: <date>` in the header. "Change something" (or any other
   reply) goes back to Phase 4.
3. Any later edit to an `Agreed` spec — by this skill or because detailed-design
   sent a business question back — resets it to `Status: Draft`, removes the
   `Signed off` line, and needs a new sign-off.

### Phase 8 — Deliver

Summarize: the feature, the story count, SRS amendments made, and the handoff
— `/detailed-design FEAT-NNN` designs the technical side from this spec.
Offer to proceed.

## Scope boundaries

Does **not**: design contracts, schema, components or screens; read or change
code; change the plan's build order or the architecture; set `Agreed` without
the user's explicit sign-off; write to any RTM column other than adding rows
for new FRs.

## What good looks like

- A product owner can read the spec without any technical knowledge and say
  "yes, that's what I want".
- Every acceptance criterion is something acceptance verification can check
  later, and detailed-design can map to a technical check without rewording.
- The Q&A log shows why each rule exists.
- The SRS and the spec say the same thing.
