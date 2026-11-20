# Content patterns

The canvas shows the shape. The side panel and the sections carry the substance that architects, analysts and reviewers act on. This file shows what "good" looks like for each piece.

## Contents

- Respect the source of truth
- Node descriptions and specs
- Data contracts on arrows
- Flows
- Notes
- Sections by audience
- Writing style

## Respect the source of truth

When the user supplies an agreed design (an HLD image, a whiteboard photo, an approved document):

- Use its component names and grouping exactly, even where you would design it differently.
- Express each disagreement as a note:
  - `consider`: a refinement.
  - `decision`: an open choice for an owner.
  - `confirmed`: once the user decides.
- Add things that are missing from the agreed design (observability, an email service, Phase 2 systems) as `ghost` nodes and edges, with a note explaining why.
- When sources conflict, for example a deck and a later document, follow the newest agreed one and log the conflict in a note or the footnote.
- When the user confirms a decision, update its note to `confirmed` and change the labels everywhere it shows (canvas, specs, cards).

## Node descriptions and specs

- **`desc`**: one or two sentences on what the component is *for*, not how it is built.
- **`specs`**: the operational facts reviewers ask for:
  - `host`: the runtime or service, with SKU or tier if relevant.
  - `trigger`: what invokes it (HTTP from X, queue Y, timer, webhook).
  - `ops`: 3–6 bullets of what it does, using domain verbs ("Price lines by pricing type", not "Handles pricing").
  - `data`: what it owns or reads.
  - `nfr`: latency, scaling, isolation, rules to respect.

Add specs for every component that has logic. Leaves such as actors and simple externals can skip them.

## Data contracts on arrows

Give `data` to every arrow that crosses a trust boundary, carries personal or confidential data, or is an integration:

```json
"data": { "p": "Quote document (customer, lines, priced snapshot)", "f": "JSON · REST over HTTPS", "c": "pii", "s": "Partition by tenant; managed identity; audited", "w": "On save" }
```

- `p` (payload): what moves, in business terms.
- `f` (format/protocol): JSON/REST, gRPC, queue message, SQL, SFTP CSV, OIDC, webhook.
- `c` (class): `public` · `internal` · `conf` · `pii` · `restr` (secrets, card data).
- `s` (security): authN/authZ, transport, encryption, validation (e.g. an HMAC on webhooks).
- `w` (when, optional): trigger or frequency.

A data catalogue table (see below) summarises the same information per data entity.

## Flows

- Name flows after the question they answer ("Dealer rep: quote → e-sign", "Release to PROD"), or after the actor.
- Write 3–8 steps. Each step is one bold verb phrase followed by one sentence, and lights up the edges involved, in order.
- Give each actor or persona node `flow: "<key>"`, so clicking the person plays their journey.
- Make exceptions their own flows (rejection, retry, rollback) rather than extra steps.
- Use the `all` key to set the overview intro; `meta.overview` gives key facts (users, runtime, data, notes count).

## Notes

A note is a decision unit. Use this structure:

- **id**: N1… for notes pinned to a node, G1… for cross-cutting ones. Ids stay stable across revisions, so people can refer to them.
- **st**: chosen honestly. Use `decision` only when someone must choose; use `risk` for things that can go wrong.
- **t**: the point in under 8 words ("Quote snapshot vs reprice on open").
- **b**: context, recommendation, consequence. Two to four sentences.
- **r**: where the evidence is (document and page, sheet and row, review finding id).

If an independent review produced findings, map each one to a note, or say it is resolved by the design.

## Sections by audience

Choose sections for the people who will read the page. Don't include all of them by default.

| audience | sections that help |
| --- | --- |
| Business / BA | `personas` (who does what: where, when, can, cannot, how, data), a permission `table` with `matrix: true`, scope `cards` (in, out, later) |
| Architects | `catalogue`, `notes`, a data catalogue `table`, a mapping `table` (legacy → new) |
| Security | data catalogue with classes and protection, notes on identity, isolation and secrets |
| Delivery / ops | link to a separate deployment page; environment and pipeline tables live there |

### Persona card rows (pattern)

```json
{ "title": "Dealer Sales Rep", "tone": "frontend", "flow": "dealer",
  "tags": ["Dealer SSO", "Role: Sales Rep", "Scope: own group"],
  "rows": [
    ["Where", "Client app at example.com; desktop-first"],
    ["When",  "Daily, during customer meetings"],
    ["Can",   ["Create and clone quotes", "Generate documents", "Send for e-sign"]],
    ["Cannot","Admin screens; other groups' data"],
    ["How",   "Browser → WAF → app → OIDC → API → services"],
    ["Data",  ["Quote · JSON [[pii]] read/write own", "Price book · JSON [[conf]] read"]]
  ] }
```

### Data catalogue table (pattern)

Columns: Data · Where · Format · Class · Who reads/writes · Protection. Put the class in as a token (`[[pii]]`) so it renders as a pill.

### Permission matrix (pattern)

Columns: Capability · role 1 · role 2 · …. Cells are `✓`, `–`, or a scope word (`own`, `group`, `read`). Mark proposals beyond the source requirements with † and say so in the section lead.

## Writing style

- Write short, concrete phrases with domain words, not generic ones ("Pricing" says less than "Price lines by pricing type and dealer markup").
- Show units and numbers when they are known ("p95 < 300 ms", "TTL 24 h", "≤ 15 min SAS").
- Do not invent facts. Anything inferred goes into a `consider` note, or the footnote says "assumption".
- Use the people's own terms from their documents (component and role names), so the diagram reads as theirs.
