# Build context: repository root (needs libs/python). Example:
#   docker build -f services/search-ai/Dockerfile .
FROM ghcr.io/astral-sh/uv:python3.12-bookworm-slim AS build
ENV UV_COMPILE_BYTECODE=1 UV_LINK_MODE=copy
WORKDIR /src
COPY libs/python ./libs/python
COPY services/search-ai ./services/search-ai
WORKDIR /src/services/search-ai
RUN uv sync --frozen --no-dev --no-editable

FROM python:3.12-slim-bookworm
WORKDIR /app
COPY --from=build /src/services/search-ai/.venv /app/.venv
ENV PATH="/app/.venv/bin:$PATH"
USER nobody
CMD ["uvicorn", "search_ai.main:app", "--host", "0.0.0.0", "--port", "8000"]
