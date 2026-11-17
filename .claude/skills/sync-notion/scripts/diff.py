"""Track which docs/ files need pushing to Notion.

Usage (run with python -I from anywhere):
  diff.py status                      JSON: new, changed, deleted, unchanged (with titles and parents)
  diff.py render <path>               Notion-ready Markdown: banner, no H1, links to Notion pages, '<' escaped
  diff.py link <path> <page_id>       store a page id with no hash (page created, content not pushed yet)
  diff.py record <path> <page_id>     store the current hash and page id for a docs-relative path
  diff.py forget <path>               drop a deleted file from the manifest
  diff.py set-page <key> <page_id>    store a container page id (key: root, or a folder name like adr)
"""

import hashlib
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[4]
DOCS = ROOT / "docs"
MANIFEST = ROOT / ".claude" / "notion-sync.json"
IGNORE = {".requirements-progress.md"}


def load() -> dict:
    if MANIFEST.exists():
        return json.loads(MANIFEST.read_text(encoding="utf-8"))
    return {"pages": {}, "files": {}}


def save(m: dict) -> None:
    MANIFEST.write_text(json.dumps(m, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def docs_files() -> dict[str, Path]:
    out = {}
    for p in sorted(DOCS.rglob("*.md")):
        rel = p.relative_to(DOCS).as_posix()
        if p.name in IGNORE:
            continue
        out[rel] = p
    return out


def sha(p: Path) -> str:
    return hashlib.sha256(p.read_bytes()).hexdigest()


def title(p: Path) -> str:
    for line in p.read_text(encoding="utf-8").splitlines():
        m = re.match(r"#\s+(.+)", line)
        if m:
            return m.group(1).strip()
    return p.stem


def parent_key(rel: str) -> str:
    return rel.split("/")[0] if "/" in rel else "root"


def status() -> None:
    m = load()
    files = docs_files()
    result = {"pages": m["pages"], "new": [], "changed": [], "unchanged": [], "deleted": []}
    for rel, p in files.items():
        entry = {"path": rel, "title": title(p), "parent": parent_key(rel)}
        known = m["files"].get(rel)
        if known is None:
            result["new"].append(entry)
        else:
            entry["page_id"] = known["page_id"]
            bucket = "unchanged" if known["sha256"] == sha(p) else "changed"
            result[bucket].append(entry)
    for rel, known in m["files"].items():
        if rel not in files:
            result["deleted"].append({"path": rel, "page_id": known["page_id"]})
    print(json.dumps(result, indent=2, ensure_ascii=False))


def record(rel: str, page_id: str, pending: bool = False) -> None:
    p = docs_files().get(rel)
    if p is None:
        sys.exit(f"not a synced docs file: {rel}")
    m = load()
    m["files"][rel] = {"page_id": page_id, "sha256": "" if pending else sha(p)}
    save(m)


def link(rel: str, page_id: str) -> None:
    record(rel, page_id, pending=True)


def forget(rel: str) -> None:
    m = load()
    m["files"].pop(rel, None)
    save(m)


LINK = re.compile(r"(?<!!)\[([^\]]*)\]\(([^)\s]+)\)")
TAG = re.compile(r"<(?=[A-Za-z/!])")


def notion_url(page_id: str) -> str:
    return "https://www.notion.so/" + page_id.replace("-", "")


def rewrite_line(line: str, rel: str, m: dict) -> str:
    """Rewrite relative links, then escape tag-like '<' outside inline code."""

    def link(match: re.Match) -> str:
        text, target = match.group(1), match.group(2)
        if re.match(r"[a-z]+:", target) or target.startswith("#"):
            return match.group(0)
        path = target.split("#")[0]
        resolved = (DOCS / Path(rel).parent / path).resolve()
        try:
            key = resolved.relative_to(DOCS.resolve()).as_posix()
        except ValueError:
            return text
        known = m["files"].get(key)
        return f"[{text}]({notion_url(known['page_id'])})" if known else text

    parts = re.split(r"(`+[^`]*`+)", LINK.sub(link, line))
    for i in range(0, len(parts), 2):
        parts[i] = TAG.sub(r"\\<", parts[i])
    return "".join(parts)


def render(rel: str) -> None:
    p = docs_files().get(rel)
    if p is None:
        sys.exit(f"not a synced docs file: {rel}")
    m = load()
    lines = p.read_text(encoding="utf-8").splitlines()
    out = [f"<callout icon=\"🔄\" color=\"gray_bg\">\nSynced from `docs/{rel}`. Edit in the repo; changes made here are overwritten.\n</callout>", ""]
    fence = None
    dropped_title = False
    for line in lines:
        stripped = line.lstrip()
        if fence:
            out.append(line)
            if stripped.startswith(fence) and stripped.strip() == fence:
                fence = None
            continue
        fm = re.match(r"(`{3,}|~{3,})", stripped)
        if fm:
            fence = fm.group(1)
            out.append(line)
            continue
        if not dropped_title and re.match(r"#\s", line):
            dropped_title = True
            continue
        out.append(rewrite_line(line, rel, m))
    print("\n".join(out))


def set_page(key: str, page_id: str) -> None:
    m = load()
    m["pages"][key] = page_id
    save(m)


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    cmd, *args = sys.argv[1:] or ["status"]
    commands = {
        "status": (status, 0),
        "render": (render, 1),
        "record": (record, 2),
        "link": (link, 2),
        "forget": (forget, 1),
        "set-page": (set_page, 2),
    }
    if cmd not in commands or len(args) != commands[cmd][1]:
        sys.exit(__doc__)
    commands[cmd][0](*args)
