"""FastAPI app factory."""

from __future__ import annotations

from fastapi import FastAPI, Response
from opentelemetry.instrumentation.fastapi import FastAPIInstrumentor

from hivespace_common import otel
from search_ai.features.ping.router import router as ping_router


def create_app() -> FastAPI:
    otel.setup("search-ai")
    app = FastAPI(title="HiveSpace search-ai")

    @app.get("/health")
    async def health() -> Response:
        return Response("Healthy", media_type="text/plain")

    @app.get("/ready")
    async def ready() -> Response:
        # No dependencies yet; the first slice adds its database check here.
        return Response("Healthy", media_type="text/plain")

    app.include_router(ping_router)
    FastAPIInstrumentor.instrument_app(app, excluded_urls="health,ready")
    return app


def __getattr__(name: str) -> FastAPI:
    # uvicorn imports "search_ai.main:app"; build it lazily so importing the module has no side effects.
    if name == "app":
        return create_app()
    raise AttributeError(name)
