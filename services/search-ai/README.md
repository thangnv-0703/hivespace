# search-ai (Python, lean)

Generated from `templates/python-service`. Its responsibilities are in [architecture §5.2](../../docs/architecture.md); its first feature slice is in [the implementation plan](../../docs/implementation-plan.md). Until then it serves the outer shell only: `/health`, `/ready` and the sample `GET /v1/search-ai/ping`.

System-wide setup, the local stack and conventions are in the [root README](../../README.md). Config names: [`.env.example`](.env.example).

```bash
uv run pytest
```

```bash
uv run ruff check src tests && uv run mypy && uv run lint-imports
```

Update from the template:

```bash
uvx copier@9.19.1 update -a services/search-ai/.copier-answers.yml
```
