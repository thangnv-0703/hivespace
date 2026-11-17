---
name: html-flow-diagram
description: Draw interactive, self-contained HTML flow diagrams in a polished "runtime architecture flow" style. They cover system architecture, data flows, process and approval flows, user journeys, integration maps, deployment pipelines and before/after landscapes. Output has clickable components, step-by-step flow highlighting, data contracts on arrows, numbered comment pins, a considerations overlay, dark and light themes, and catalogue, persona and table sections. Use this skill whenever the user asks to draw, diagram, map, sketch, visualize or "make a diagram/flow/architecture picture" of how something works or connects. Also use it to turn an HLD, BRD, discovery notes or a description into a diagram, or to produce a shareable HTML diagram, even if they don't say "HTML" or "interactive". Prefer it over Mermaid or ASCII when the result is meant to be presented, reviewed or shared.
---

# HTML flow diagram

This skill produces a single self-contained `.html` file. The diagram is described as a JSON **model**; a build script routes the arrows, lints the layout and injects the model into a renderer template. You write the model, not SVG.

What the reader gets:

- An SVG canvas: grid background, boundary frames, components coloured by kind, routed arrows with labels, and a legend.
- A toolbar with one button per **flow**. A flow dims everything else and animates its numbered steps.
- A side panel that explains whatever is clicked: a component (description, spec, connections, notes), an arrow (payload, format, data class, security) or a pin (design note).
- **Comment pins** (Confirmed / Aligned / Consider / Decision / Risk) and a **Considerations** overlay for suggestions that are not part of the agreed design.
- Sections below the diagram: personas ("who does what"), tables and matrices, design notes, a component catalogue, and free cards.

Bundled files:

- `assets/template.html`: the renderer. Don't edit it per diagram; everything comes from the model.
- `assets/example.model.json`: a complete small model using every feature. Copy it as a starting point.
- `scripts/build_diagram.py`: routes edges, places labels, lints, and writes the HTML.
- `scripts/screenshot.py`: headless Chrome/Edge PNG of the result, and reports JavaScript errors.
- `references/model-schema.md`: every model field. Read it before writing a model.
- `references/layout-guide.md`: coordinates, spacing, routing knobs and layout recipes for common flow shapes. Read it when placing nodes.
- `references/content-patterns.md`: what good notes, data contracts, flows, personas and sections look like. Read it when the diagram is for review or decision making.

## Workflow

### 1. Understand the story before drawing

Read the sources the user points at (design docs, notes, code, an image of an existing design). Then list:

- **Actors**: people and external systems that start or receive things. Place them on the left; externals go on the right.
- **Components**: grouped into boundaries such as cloud, VNet, app tier, "Microservices" or "External".
- **The 2–6 flows** people will actually ask about: "how does a quote get signed?", "how does a release reach prod?". These become flow buttons, so they matter more than drawing every possible arrow.
- **Open questions, risks and assumptions.** These become notes; don't bury them in prose.

If the user gave an agreed design (an HLD diagram, an architecture decision), **draw it as it is**. Put your improvements in notes (`st: "consider"` or `"decision"`) and ghost components. People review a diagram against the agreed design, and silently redrawing it loses their trust. Say what you changed, and why, in the notes.

Keep different concerns on different pages. Runtime flow, deployment pipeline and data model are separate diagrams, linked with `meta.links`. One diagram that tries to show everything gets unreadable.

### 2. Sketch a grid, then assign coordinates

Choose one primary direction: left→right for request and data flows, top→bottom for pipelines and layered stacks. Lay components on a column/row grid with consistent box sizes and 40–70px gaps, so there is room for arrows and labels. The layout guide has ready recipes (layered architecture, pipeline with environments, swimlane process, hub-and-spoke integration, step chain) and sizing rules. Size boxes from their text: about 6.6px per character for labels and 5.4px for sub-labels, plus padding.

### 3. Write the model

Start from `assets/example.model.json`. The essentials are:

- `meta`: title, subtitle, `viewBox` and an overview.
- `frames`, `nodes` and `edges`. Usually let the builder route an edge from `from`/`to`; use `fromSide`, `toSide`, `fromAt`, `toAt`, `via`, `midX` and `midY` only when the automatic route collides.
- `flows`: steps referencing edge ids.
- `notes`, `specs` and `sections`.

Put details in the side panel (`desc`, `specs`, `data`), not on the canvas. Canvas text stays short: labels of 1–3 words, sub-labels under ~25 characters, edge labels of 1–2 words.

Mark facts you inferred rather than read as assumptions, either in a note or the footnote, so the reader knows what to confirm.

### 4. Build and fix every warning

```bash
python <skill>/scripts/build_diagram.py model.json -o diagram.html
```

Each `WARN` matches a visible defect: an arrow through a box, overlapping labels, text overflowing its box, a frame label under a node, content under the legend. Fix them by moving nodes, widening boxes, shortening labels, or adding `via` points or `lx`/`ly`. Then rebuild. Aim for zero warnings. `ERROR` means a broken reference (a typo in an id) and stops the build.

### 5. Look at it

```bash
python <skill>/scripts/screenshot.py diagram.html -o shot.png
python <skill>/scripts/screenshot.py diagram.html -o flow.png --flow <flowKey> --theme light
```

Open the PNG (with the Read tool) and check it the way a reviewer would:

- Is the main path obvious at a glance?
- Do arrows cross unnecessarily?
- Is anything cramped?
- Does each flow highlight the right steps?

Iterate on positions; a couple of passes is normal. If no browser is available, the script says so; rely on the lint and tell the user you could not render it.

### 6. Deliver

Save `diagram.html` and `model.json` next to each other where the user asked, so the diagram can be edited and rebuilt later. Tell the user, briefly:

- what the diagram shows;
- which flows it has;
- which notes need their decision;
- any assumptions you made.

## Style rules and why

- **Kinds carry meaning.** Use the same kind for the same role across a diagram:
  - `frontend`: UI.
  - `backend`: app or processing, including queues and workers.
  - `service`: microservices, functions, jobs.
  - `database`: stores and caches.
  - `security`: edge, identity, gateway, secrets.
  - `cloud`: platform or ops tooling.
  - `external`: people and outside systems.
  - Relabel the legend with `meta.kindLabels` instead of inventing colours.
- **Edge styles carry meaning too.**
  - `emphasis` is the main path. Keep it to roughly the one or two flows that matter; if everything is green, nothing is.
  - `default` is ordinary calls and reads/writes.
  - `security` is auth and secrets.
  - `dashed` is async, deploy or telemetry.
  - `ghost` is a suggestion (`ghost: true`), hidden until the reader turns on Considerations.
- **Every arrow that moves data across a boundary gets a `data` contract**: payload, format, class, security. It makes the diagram reviewable, and it is the first thing security and BA reviewers ask about. Use the class tokens `[[public]]`, `[[internal]]`, `[[conf]]`, `[[pii]]` and `[[restr]]` in any text to render a classification pill.
- **Fan-outs and buses.** When one node calls many, let the edges share a trunk: same `fromAt`, then one `midY` or `via` row. Step badges are placed at the arrowhead end, so shared trunks don't stack badges.
- **Notes over prose.** A decision, risk or assumption is a note with an id (N1, N2…; G1… for cross-cutting ones), a status, a short title, a body and refs. Pin it to the component it concerns (`on`). Notes are what reviewers act on.
- **Accessibility and sharing are built in.** Dark and light themes follow the OS (toggle with T), focus states and keyboard navigation work, reduced motion is respected, and print styles are included. Keep text contrast intact by using kinds rather than custom colours.

## When not to use it

- A quick inline sketch in chat, where ASCII or Mermaid is enough and the user wants it right in the reply.
- Charts of numbers. Use a charting approach instead.
- Pixel-precise visual or UI design mockups.
