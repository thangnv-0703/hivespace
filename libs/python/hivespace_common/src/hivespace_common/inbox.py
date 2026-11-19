"""Inbox table: its primary key makes event handling idempotent (NFR-REL-005)."""

from __future__ import annotations

from datetime import UTC, datetime
from typing import Protocol

from sqlalchemy import Column, DateTime, MetaData, String, Table, Uuid
from sqlalchemy.dialects.postgresql import insert
from sqlalchemy.ext.asyncio import AsyncConnection

metadata = MetaData()

inbox = Table(
    "inbox",
    metadata,
    Column("event_id", Uuid, primary_key=True),
    Column("consumer", String(200), primary_key=True),
    Column("processed_at", DateTime(timezone=True), nullable=False),
)


class Inbox(Protocol):
    async def record(self, event_id: str, consumer: str) -> bool:
        """Return True if the event is new for this consumer, False if it was already processed."""
        ...


async def record(conn: AsyncConnection, event_id: str, consumer: str) -> bool:
    """Insert the inbox row in the caller's transaction; a conflict means a duplicate delivery."""
    stmt = (
        insert(inbox)
        .values(event_id=event_id, consumer=consumer, processed_at=datetime.now(UTC))
        .on_conflict_do_nothing()
        .returning(inbox.c.event_id)
    )
    # RETURNING, not rowcount: psycopg reports -1 for an INSERT ... ON CONFLICT through SQLAlchemy.
    return (await conn.execute(stmt)).scalar_one_or_none() is not None
