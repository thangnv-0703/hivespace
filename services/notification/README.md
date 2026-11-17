# Notification (Python, lean)

Contact projection, vi/en email templates, send and retry (architecture §5.2, ADR-0016). In the walking skeleton it consumes the `Ping` event once through its inbox. The verification email arrives with FEAT-001, order emails with FEAT-032.

System-wide setup, the local stack and conventions are in the [root README](../../README.md).

| | |
| :-- | :-- |
| Layout | `src/notification/main.py` app factory · `settings.py` · `features/<feature>/` · `providers/` (only place vendor SDKs appear) |
| Config | [`.env.example`](.env.example) (set by the AppHost) |
| Migrations | Alembic, `migrations/versions/`, applied on start |
| Boundaries | `import-linter` contracts in `pyproject.toml` |

Commands (from this folder):

```bash
uv sync
```

```bash
uv run pytest
```

```bash
uv run ruff check src tests && uv run mypy && uv run lint-imports
```
