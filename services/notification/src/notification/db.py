"""Database engine and migrations (Alembic, applied on start locally; a deploy step in the cloud)."""

from __future__ import annotations

from pathlib import Path

from alembic import command
from alembic.config import Config
from sqlalchemy.ext.asyncio import AsyncEngine, create_async_engine

SERVICE_ROOT = Path(__file__).resolve().parents[2]


def create_engine(url: str) -> AsyncEngine:
    return create_async_engine(url, pool_pre_ping=True)


def migrate(url: str) -> None:
    config = Config(str(SERVICE_ROOT / "alembic.ini"))
    config.set_main_option("script_location", str(SERVICE_ROOT / "migrations"))
    # Alembic's config is a ConfigParser: escape "%" so URL-encoded passwords (%21) survive interpolation.
    config.set_main_option("sqlalchemy.url", url.replace("%", "%%"))
    command.upgrade(config, "head")
