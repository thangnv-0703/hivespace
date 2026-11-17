"""FastAPI app factory and consumer startup."""

from __future__ import annotations

import asyncio
import contextlib
from collections.abc import AsyncIterator

from fastapi import FastAPI, Response
from opentelemetry.instrumentation.fastapi import FastAPIInstrumentor
from sqlalchemy import text

from hivespace_common import eventbus, inbox, otel
from notification.db import create_engine, migrate
from notification.features.contacts.consumer import make_handler
from notification.settings import Settings


def create_app(settings: Settings | None = None) -> FastAPI:
    otel.setup("notification")
    cfg = settings or Settings()  # values come from the environment
    engine = create_engine(cfg.async_database_url)

    async def record(event_id: str, consumer: str) -> bool:
        async with engine.begin() as conn:
            return await inbox.record(conn, event_id, consumer)

    @contextlib.asynccontextmanager
    async def lifespan(_: FastAPI) -> AsyncIterator[None]:
        await asyncio.to_thread(migrate, cfg.async_database_url)
        consumer: asyncio.Task[None] | None = None
        if cfg.messaging_connection:
            consumer = asyncio.create_task(
                eventbus.run_subscription(
                    cfg.messaging_connection, cfg.events_topic, cfg.events_subscription, make_handler(record)
                )
            )
        yield
        if consumer is not None:
            consumer.cancel()
            with contextlib.suppress(asyncio.CancelledError):
                await consumer
        await engine.dispose()

    app = FastAPI(title="HiveSpace Notification", lifespan=lifespan)

    @app.get("/health")
    async def health() -> Response:
        return Response("Healthy", media_type="text/plain")

    @app.get("/ready")
    async def ready() -> Response:
        try:
            async with engine.connect() as conn:
                await conn.execute(text("SELECT 1"))
        except Exception:  # noqa: BLE001 - readiness reports any dependency failure
            return Response("Unhealthy", status_code=503, media_type="text/plain")
        return Response("Healthy", media_type="text/plain")

    FastAPIInstrumentor.instrument_app(app, excluded_urls="health,ready")
    return app


def __getattr__(name: str) -> FastAPI:
    # uvicorn imports "notification.main:app"; build it lazily so importing the module has no side effects.
    if name == "app":
        return create_app()
    raise AttributeError(name)
