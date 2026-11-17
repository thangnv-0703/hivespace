# Build context: repository root (needs libs/python). Example:
#   docker build -f services/notification/Dockerfile .
FROM ghcr.io/astral-sh/uv:python3.12-bookworm-slim AS build
ENV UV_COMPILE_BYTECODE=1 UV_LINK_MODE=copy
WORKDIR /src
COPY libs/python ./libs/python
COPY services/notification ./services/notification
WORKDIR /src/services/notification
RUN uv sync --frozen --no-dev --no-editable

FROM python:3.12-slim-bookworm
WORKDIR /app
COPY --from=build /src/services/notification/.venv /app/.venv
COPY services/notification/alembic.ini services/notification/migrations /app/
ENV PATH="/app/.venv/bin:$PATH"
USER nobody
CMD ["uvicorn", "notification.main:app", "--host", "0.0.0.0", "--port", "8000"]
