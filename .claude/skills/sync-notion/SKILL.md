---
name: sync-notion
description: One-way sync of the project's docs/ folder (SRS, use cases, RTM, architecture, dev standards, repo structure, ADRs, product docs) to Notion under the "HiveSpace Docs" page. Pushes only files whose content changed since the last sync. Use when the user asks to sync, publish, push or update the docs in Notion, or runs /sync-notion.
---

# Sync docs/ to Notion

The repo is the source of truth. Notion pages are overwritten on each sync; edits made in Notion are lost.

Helper: `python -I .claude/skills/sync-notion/scripts/diff.py <command>` (run from the repo root). Manifest: `.claude/notion-sync.json` (page IDs and content hashes; commit it).

| Command | Does |
| :-- | :-- |
| `status` | JSON with `pages` (container page IDs) and `new`, `changed`, `unchanged`, `deleted` files, each with `path`, `title`, `parent` |
| `render <path>` | Notion-ready Markdown for one file: sync banner, H1 removed (it becomes the title), relative `.md` links rewritten to Notion page URLs, links outside `docs/` turned into plain text, tag-like `<` escaped outside code |
| `link <path> <page_id>` | Store a page ID with an empty hash, so the file still counts as changed until its content is pushed |
| `record <path> <page_id>` | Store the file's current hash and page ID. Run only after a successful push |
| `forget <path>` | Drop a deleted file from the manifest |
| `set-page <key> <page_id>` | Store a container page ID (`root`, `adr`, `product`, or any other subfolder name) |

## Steps

1. Run `status`. If `new`, `changed` and `deleted` are all empty, say "Notion is up to date" and stop.
2. Read `notion://docs/notion-flavored-markdown-spec` with the Notion fetch tool once per run.
3. **Containers.** If `pages.root` is missing, create a private page titled `HiveSpace Docs` with `creation_mode: "draft"` and `set-page root <id>`. For each subfolder named in a file's `parent` (for example `adr`, `product`) with no page in `pages`, create a child page of root (titles: `adr` → `ADRs`, `product` → `Product`, others capitalized) and `set-page <folder> <id>`.
4. **Pass 1, new files.** Create an empty page for each new file under its parent page (`root` or the folder page), titled with its `title` (a shorter title is fine for long ones). Batch pages that share a parent in one `notion-create-pages` call. Store each page with `link <path> <page_id>`, so links in pass 2 resolve and the file stays "changed" until pass 2 succeeds.
5. **Pass 2, content.** For each new and changed file: run `render <path>` and push its output exactly as printed with `notion-update-page` `replace_content` on the file's page, `allow_async: true` for large files; wait for async tasks with `notion-get-async-task`. Then run `record <path> <page_id>`. Never edit the rendered text by hand; fix `diff.py` instead.
6. **Deleted files.** List them with their page links and ask the user before moving those pages to trash. On a yes, trash them, then `forget <path>`. On a no, `forget` anyway only if the user says so.
7. **Root index.** Nothing to do: Notion lists child pages on the root and folder pages automatically. Never `replace_content` on the root or folder pages, which would drop their `<page>` blocks.
8. Report counts (created, updated, deleted, unchanged), any failures, and the root page link.

If a push fails, do not `record` that file; it stays "changed" and is retried on the next run. To force a full re-push, delete the `sha256` values (keep `page_id`) in the manifest.

## Limits

- One-way. Comments in Notion survive; edits to the body do not.
- Runs only when asked; no hook or schedule.
- Images and links to files outside `docs/` are not uploaded or linked.
