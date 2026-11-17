"""Fail when vi and en translation sets have different keys (NFR-L10N-004, dev-standards §5).

Checks:
  - contracts/errors/messages.vi.yaml vs messages.en.yaml
  - every web/apps/*/src/locales/vi/*.json vs the matching en/*.json (nested keys flattened)

Usage: python -I tools/scripts/i18n_parity.py <repo-root>
"""

from __future__ import annotations

import json
import sys
from pathlib import Path
from typing import Any

import yaml


def flatten(value: Any, prefix: str = "") -> set[str]:
    if isinstance(value, dict):
        keys: set[str] = set()
        for k, v in value.items():
            keys |= flatten(v, f"{prefix}{k}.")
        return keys
    return {prefix.rstrip(".")}


def compare(label: str, vi: dict[str, Any], en: dict[str, Any]) -> list[str]:
    vi_keys, en_keys = flatten(vi), flatten(en)
    problems = [f"{label}: missing in en: {k}" for k in sorted(vi_keys - en_keys)]
    problems += [f"{label}: missing in vi: {k}" for k in sorted(en_keys - vi_keys)]
    return problems


def main(root: Path) -> int:
    problems: list[str] = []
    errors = root / "contracts" / "errors"
    problems += compare(
        "contracts/errors",
        yaml.safe_load((errors / "messages.vi.yaml").read_text(encoding="utf-8")),
        yaml.safe_load((errors / "messages.en.yaml").read_text(encoding="utf-8")),
    )
    for vi_file in sorted(root.glob("web/apps/*/src/locales/vi/*.json")):
        en_file = vi_file.parent.parent / "en" / vi_file.name
        label = str(vi_file.relative_to(root))
        if not en_file.exists():
            problems.append(f"{label}: no matching en file")
            continue
        problems += compare(
            label,
            json.loads(vi_file.read_text(encoding="utf-8")),
            json.loads(en_file.read_text(encoding="utf-8")),
        )
    for p in problems:
        print(p)
    print("i18n parity: " + ("FAILED" if problems else "ok"))
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main(Path(sys.argv[1] if len(sys.argv) > 1 else ".")))
