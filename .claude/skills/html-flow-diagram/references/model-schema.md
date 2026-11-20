# Model schema

A model is one JSON object. Only `meta`, `nodes` and `edges` are required. Coordinates are in SVG user units; the canvas is `meta.viewBox` wide and tall, with the origin at the top-left.

```json
{
  "meta": { ... },
  "frames":   [ ... ],
  "nodes":    [ ... ],
  "edges":    [ ... ],
  "flows":    { "key": { ... } },
  "notes":    [ ... ],
  "specs":    { "nodeId": { ... } },
  "sections": [ ... ]
}
```

## Contents

- meta
- frames
- nodes
- edges
- flows
- notes
- specs
- sections
- Rich text

## meta

| field | type | notes |
| --- | --- | --- |
| `title` | string | Page and diagram title. |
| `subtitle` | HTML | One or two sentences under the title: what this shows and how to use it. |
| `description` | string | Screen-reader description of the whole diagram (`<desc>`). |
| `viewBox` | [w, h] | Default `[1280, 820]`. Leave ~70px at the bottom for the legend. |
| `minWidth` | number | Minimum rendered width before horizontal scroll (default min(w, 940)). |
| `wide` | bool | Big-picture mode: the page uses up to 2200px instead of a 1360px column, with a narrower side panel. Use with `minWidth` ≈ 1400–1600 for viewBoxes wider than ~1500, so text stays readable. |
| `legendX`, `legendY` | number | Legend position (default x=30, y=h−62). |
| `overview` | [[label, value], …] | Key facts shown in the side panel when no flow is selected. |
| `links` | [{href, label}] | Toolbar links to sibling diagrams (e.g. runtime ↔ deployment). |
| `kindLabels` | {kind: label} | Rename legend entries, e.g. `{"service": "Azure Function"}`. |
| `edgeLabels` | {style: label} | Rename edge-style legend entries, e.g. `{"dashed": "Telemetry"}`. |
| `classes` | {token: [label, cssVar]} | Add or override classification pills, e.g. `{"secret": ["Secret", "--security-stroke"]}`. |
| `specRows` | [[key, label], …] | Row order and labels for specs (default host, trigger, ops, data, nfr). |
| `footnote` | HTML | Sources, assumptions, version. |

## frames

Boundaries drawn behind everything: cloud or region, network, tier, lane, "External".

| field | type | notes |
| --- | --- | --- |
| `x, y, w, h` | number | Required. |
| `label` | string | Optional caption. |
| `style` | `"region"` \| `"frame"` \| `"dashed"` | `region` = outer amber boundary with a pill label; `frame` = subtle filled group; `dashed` = outline only (external zone, lane). |
| `tone` | kind | Tints a frame's stroke with a kind colour (e.g. `"service"` for a Functions group). |
| `labelPos` | `"top"` \| `"bottom"` | Put the label at the bottom when arrows enter from the top. |
| `labelX` | number | Move the label horizontally to dodge arrows. |

## nodes

| field | type | notes |
| --- | --- | --- |
| `id` | string | Required, unique, referenced by edges, flows, notes and specs. |
| `kind` | `frontend` \| `backend` \| `service` \| `database` \| `security` \| `cloud` \| `external` | Colour and icon. |
| `x, y, w, h` | number | Required. Typical: 150×50 (actor), 150–190×56 (component), 300–420×56–80 (wide tier). |
| `label` | string | Bold first line; ≤ ~22 chars at w=170. |
| `sub` | string | Muted second line; ≤ (w−12)/5.4 chars. |
| `sub2` | string | Small third line (needs h ≥ 70), e.g. a URL. |
| `desc` | string/HTML | One or two sentences shown in the side panel. |
| `ghost` | bool | Consideration: dashed, hidden until "Considerations" is on. |
| `sigil` | kind | Use another kind's icon (e.g. a queue drawn as `backend` with the `service` icon). |
| `flow` | flow key | Clicking the node opens that flow; good for actor and persona nodes. |

## edges

| field | type | notes |
| --- | --- | --- |
| `id` | string | Required, unique. Short ids (`e1`, `s3`, `d2`) keep flows readable. |
| `from`, `to` | node id | Required. Arrowhead points at `to`. |
| `s` | `emphasis` \| `default` \| `security` \| `dashed` \| `ghost` | Style (see SKILL.md for meaning). Default `default`. |
| `ghost` | bool | Hide until Considerations is on (use with `s: "ghost"`). |
| `label` | string | 1–2 words on the canvas. Leave it out for obvious arrows. |
| `data` | {p, f, c, s, w} | Data contract shown on click: payload, format/protocol, class token, security, when. |
| `fromSide`, `toSide` | `top` \| `bottom` \| `left` \| `right` | Override the automatic side choice. |
| `fromAt`, `toAt` | 0–1 | Position along the side (0 = top/left). Separates parallel arrows on one side. |
| `via` | [[x, y], …] | Corner points. The router inserts orthogonal corners between them, moving along the exit direction first. |
| `midX` / `midY` | number | Where the middle segment of a Z-shaped route runs (H→H uses midX, V→V uses midY). |
| `lx`, `ly` | number | Explicit label centre. |
| `labelAt` | 0–1 | Label position as a fraction of the path length (default: middle of the longest segment). |
| `d` | SVG path | Escape hatch: a hand-written path (`M`/`L`/`Q` only). It is not routed, but it is linted. |
| `noArrow` | bool | No arrowhead (association lines). |

Routing rules worth knowing:

- With no sides given, the router picks right→left when the target is beside the source, and bottom→top when it is below.
- When the two boxes overlap on the cross axis, the line is straight.
- Otherwise the route is an L or a Z with 8px rounded corners.
- Same-side routes (right→right) loop out 24px.

## flows

`flows` is an object keyed by flow id. Each key becomes a toolbar button.

```json
"checkout": {
  "title": "Customer checkout",
  "button": "Checkout",
  "color": "frontend",
  "intro": "From cart to confirmed order.",
  "tags": ["Identity: customer login", "Data: [[pii]]"],
  "nodes": ["db"],
  "steps": [
    { "e": ["e1", "e3"], "t": "<strong>Open the shop</strong> through the CDN/WAF." }
  ]
}
```

- `button`: shorter toolbar text (optional).
- `color`: the kind used for the button dot.
- `tags`: chips shown under the title.
- `nodes`: extra nodes to highlight (optional).
- In each step, the badge number sits on `e[0]`.

A special key `all` overrides the overview's title and intro. Steps should read as a short story of 3–8 steps; each step's `e` lists the edges it lights up.

## notes

```json
{ "id": "N1", "on": "pay", "st": "decision", "t": "3-D Secure handling", "b": "Body (HTML).", "r": "Refs / sources" }
```

- `st` is one of:
  - `confirmed`: decided.
  - `aligned`: the agreed design covers it; the note adds guidance.
  - `consider`: a suggested refinement.
  - `decision`: needs an owner's decision.
  - `risk`.
- Notes with `on` become numbered pins on that node, stacked leftwards when there are several.
- Notes without `on` are cross-cutting: they appear in the notes section only. Number them G1, G2….

## specs

`specs` is an object keyed by node id. It is shown in the side panel and as catalogue cards.

```json
"bff": { "host": "App Service, .NET 10", "trigger": "SPA calls", "ops": ["…", "…"], "data": "Session only", "nfr": "p95 < 300 ms" }
```

- Default rows are `host`, `trigger`, `ops`, `data` and `nfr`.
- Any extra key is appended with its own name as the row label.
- Arrays render as bullet lists.
- Change the order or labels with `meta.specRows`.

## sections

`sections` is an ordered list rendered under the diagram. If you leave it out, the page shows notes (when there are any) and then the catalogue (when there are specs).

| `type` | fields | use |
| --- | --- | --- |
| `personas` | `title`, `lead`, `items: [{title, tone, flow, tags[], rows: [[label, text \| [..]]]}]` | "Who does what". Each card can open its flow. |
| `table` | `title`, `lead`, `columns[]`, `rows[[…]]`, `matrix` (bool) | Data catalogue, mapping tables, permission matrices. With `matrix: true`, `✓` renders green, `–` muted, anything else amber. |
| `notes` | `title`, `lead` | The notes list, with filters by status. |
| `catalogue` | `title`, `lead` | One card per node that has a spec. |
| `cards` | `title`, `lead`, `items: [{title, tone, bullets[], html}]` | Scope, out-of-scope, context. |
| `html` | `title`, `html` | Anything else. Keep it small. |

## Rich text

Every text field that is described as HTML can use the following:

- `<b>`, `<strong>`, `<code>` and `<a href>`.
- `[[token]]` classification pills: `public`, `internal`, `conf`, `pii`, `restr`, plus any token defined in `meta.classes`.

Don't put user-supplied or untrusted strings into HTML fields without escaping them.
