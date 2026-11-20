# Layout guide

Good layout does most of the explaining. The aim is that someone sees the main path in two seconds and never has to trace a line through a box.

## Contents

- Grid first
- Sizing boxes from text
- Spacing
- Routing knobs
- Recipes
- Fixing lint warnings

## Grid first

Before writing coordinates, decide columns (x) and rows (y), then snap every box to them. Consistent columns are what make the result look designed.

- **One primary direction.** Requests and data go left→right; pipelines and layered stacks go top→bottom. Return arrows (responses, webhooks) are fine, but keep them few and route them on their own lane or y-offset.
- **Actors on the far left** (x≈20, w≈150) and **externals on the far right** in a dashed "External" frame.
- **Boundaries as frames.** Draw the outer `region` first (x≈200). Inside it, use `frame` groups for tiers ("App Service", "Functions", "Data tier · private endpoints"). Leave 10–20px between a frame edge and its boxes, and 16px at the top for its label, or put the label at the bottom.
- **Rows of equals.** Siblings, such as five microservices or four data stores, share a y and a size, with 12–20px between them.
- **Legend band.** Keep the bottom ~70px of the viewBox free. Pick `viewBox` height = lowest box bottom + ~120.

A quick way to plan: write the columns and rows as a little table in your reasoning, then derive coordinates.

```
cols:  actors 20 | edge 230 | app 440..620 | services 680..830 | data 870 | external 1010
rows:  80 (top tier) | 160 | 250 | 350 | 440
```

## Sizing boxes from text

JetBrains Mono is about 0.6em per character.

| text | size | px / char | rule |
| --- | --- | --- | --- |
| node label | 11px bold | 6.6 | w ≥ chars × 6.6 + 30 (icon + padding) |
| node sub | 9px | 5.4 | w ≥ chars × 5.4 + 12 |
| node sub2 | 7.5px | 4.5 | needs h ≥ 70 |
| edge label | 8px | 4.9 | segment length ≥ chars × 4.9 + 14 |
| frame label | 8.5px | 5.1 | keep arrows out of it |

Typical heights:

- 50 for actors and externals.
- 56 for components.
- 70–80 for wide tier nodes with sub2.
- 40 for small chips such as private endpoints. Below 40 the icon is dropped.

## Spacing

- **Box to box, along the flow:** 40px minimum, 60–70px when the arrow carries a label.
- **Parallel arrows:** 15–25px apart. Shift them with `fromAt`/`toAt` (0.3 and 0.7 instead of both 0.5) or with different `midX`/`midY`.
- **Labels:** sit on the longest straight segment. If a label would land on a box, move it with `lx`/`ly` or `labelAt`, or shorten it. Put long explanations in `data`.

## Routing knobs

In order of preference:

1. **Move a box** so the two boxes overlap on the cross axis. The router then draws a straight line, the cleanest result.
2. **`fromSide` / `toSide`** to leave or enter from a different side. Example: an arrow into the bottom of a cache from a worker beside it.
3. **`fromAt` / `toAt`** to pick the point along a side; separates arrows sharing a side.
4. **`midX` / `midY`** to move the middle leg of a Z route into an empty gutter.
5. **`via: [[x, y], …]`** for anything else. Give the corner points in order; the router fills in orthogonal corners, moving along the exit direction first. If the last leg arrives from the wrong direction, the builder warns. Add one more via point or change `toSide`.

### Shared trunks (fan-out, data bus)

When one node feeds many (a gateway to five services, or services to a data tier), share one trunk:

- **Gateway → services:** every edge uses `fromSide: "bottom"`, the same `fromAt`, and the same `midY`. The edges overlap on the trunk and split at their targets.
- **Services → data stores:** use a common bus row, `via: [[srcX, busY], [dstX, busY]]`. Give each store's entry a different x so the arrowheads don't stack, e.g. `toAt` 0.25 / 0.5 / 0.75.

These overlaps are intentional, and flows still highlight each edge separately.

## Recipes

### Layered architecture (left→right), the default

```
users │ edge/WAF │ app tier (frame) │ gateway │ services (frame) │ data (frame) │ External
```

Users enter the tall edge box on its left at different y values; one arrow goes into the app. App → gateway → services fan out. Services → data go via a bus row below the services. Externals on the right are reached by horizontal arrows from the service that owns the integration. Secrets and identity go in the top row, telemetry on a dashed arrow to an observability node.

### Delivery pipeline (columns as stages)

```
Repos (lane) │ Pipelines (lane) │ Environments (lane: DEV → TEST → UAT → PROD stacked) │ Observe
```

- Use `dashed` frames as lanes.
- Repos → CI horizontally; CI → artifact → CD vertically.
- CD → DEV and → TEST on separate `midX` gutters.
- Environments chain downwards with labels like "same version + approval".
- A monitor node sends a dashed "health gate" arrow back to CD along the bottom (`via` row below everything).

### Swimlane process (rows as actors)

- One dashed frame per actor or department, stacked vertically and spanning the width, with the lane label at the left.
- Steps flow left→right; hand-offs drop between lanes vertically.
- Decision points are small nodes, with two outgoing edges labelled "yes" and "no".
- Each happy path and each exception becomes its own flow.

### Hub and spoke (integration map)

- Hub in the centre (wide box); systems around it at equal distance.
- Inbound arrows on the left and top, outbound on the right and bottom.
- Give each spoke a `data` contract: this is where the integration details live.

### Step chain (journey, approval, sequence-like)

- A horizontal or snake chain of 5–9 steps with uniform boxes.
- Actors sit above or below the step they perform, with short vertical arrows.
- One flow per scenario: happy path, rejection, timeout.

### Before / after (as-is vs to-be)

- Two diagrams on two pages, linked with `meta.links`, using the same kinds and positions where things persist.
- Or one diagram with the new parts as normal nodes and the retiring parts as `ghost`, plus a mapping table section.

## Fixing lint warnings

| warning | usual fix |
| --- | --- |
| edge crosses node X | Move X off the line, add `via` around it, or switch `fromSide`/`toSide` |
| label overlaps node / label | `lx`/`ly`, `labelAt`, shorter text, or move the edge with `fromAt` |
| label longer than its longest segment | shorten the label, drop it (put the detail in `data`), or spread the boxes |
| node label/sub too wide | widen the box (keep its row consistent) or shorten the text |
| nodes overlap | re-snap to the grid |
| frame label overlaps node / edge | `labelPos: "bottom"`, `labelX`, or more top padding inside the frame |
| arrives from the wrong direction | one more `via` point, or set `toSide` to match the last leg |
| node overlaps the legend band | increase `viewBox` height or set `meta.legendY` |
| outside the viewBox | widen the viewBox or move the node |
