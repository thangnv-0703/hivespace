# Review Artifact (the visual the user opens to review the design)

Specs in markdown cannot be reviewed by eye. Every run of this skill ends by
publishing a **design canvas artifact**: one artboard per key screen state, so
the user can look at the UI before anything is built. The artifact is a
**visual aid**. `ui-design.md` and the manifest stay authoritative; where the
artifact and the spec differ, the spec wins and the artifact is fixed.

## When

After the spec document is written (Phase 4), in every mode (anchor,
per-feature, re-verification), unless the user declines.

If the session has no Artifact tool: say so plainly in the delivery summary,
name the missing capability, skip the artifact, and claim nothing. Never skip
silently, never describe an artifact that was not made.

## What to draw

- Per screen in scope: the **default** state, plus each state that changes the
  layout (error, "notice" or result views, empty, loading when distinctive).
- One **mobile** view (390 px) of the most demanding screen.
- Any shell extras the spec defines (header menu, banners) as fragments.
- Skip states that only restate design.md's state conventions. List them under
  "not drawn" in the document header so the gap is honest.
- Strategy A (registered) screens: draw only the code-native supplements and
  states the tool design lacks. Code-native and generated screens: all key
  states.
- Use the spec's copy. Placeholder data only where the spec needs an example
  (an email address, a name). No invented statistics or lorem ipsum.

## How (Artifact tool, "Design" type)

1. `Artifact` with `action: "quickstart"`, `intent: "design"`,
   `design_systems: false` (the project's design system is `docs/design.md` +
   `docs/tokens.json`, used directly; do not look for another).
2. Publish with the returned `type_url`, a `title` (the feature name),
   `auto_open: "after_first_write"`, no files.
3. Follow the instructions the create result carries for the file format. They
   are served by the artifact type and may change; do not rely on a remembered
   format. Order: write `project/canvas.json` first (every board already listed
   in `boards` and `order`, entry board named `Main.dc.html`), then the entry
   board, publish both, then write and publish the remaining boards **one file
   per call**. Frame positions: 80 px between frames in a row, 120 between rows.
4. Look: design.md role tokens as literal values (the artifact cannot import the
   repo's CSS); the project font through a Google Fonts `<link>`; lucide icons
   as inline SVG; real `<button>`, `<a>`, `<label>` elements; page screens use a
   fluid root and `expand: "fill"`, fragments use a fixed width. Breakpoints
   follow design.md (the desktop split layout shows from `lg`).
5. Do **not** render, screenshot or otherwise verify the artifact unless the
   user asks. Say it has not been visually checked.

## Persist in the repo (so a new session finds it without being told)

- Copy `canvas.json` and every `*.dc.html` to
  `docs/features/FEAT-NNN-<slug>/mockups/` (anchor mode: `docs/anchor-mockups/`).
  Local files in a temp or scratchpad folder are not a record.
- Add the "Visual mockups" line to the document header (see
  document-templates.md): the folder, the live canvas URL, the states not drawn,
  and "the spec wins on any difference".
- Add `mockups` to each screen's manifest entry (manifest-guide.md).
- Link the `mockups/` folder from the feature's `tasks.md` UI task, when one
  exists.

## Revision and re-verification

Update the **same canvas** (same `url`) and the same files in `mockups/`. Never
create a second canvas for one feature. Add or remove boards through
`canvas.json`; keep every board that is not changing.

## Sharing

Artifacts are private to the owner until shared from the page's Share menu.
Tell the user so in the delivery summary. Do not offer other hosting.
