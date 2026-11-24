"""Contact projection: the latest email, name and language per user, kept from Identity's events."""

from __future__ import annotations

import uuid
from datetime import UTC, datetime
from typing import Any

from sqlalchemy import Column, DateTime, MetaData, String, Table, Text, Uuid
from sqlalchemy.dialects.postgresql import insert
from sqlalchemy.ext.asyncio import AsyncConnection

metadata = MetaData()

contacts = Table(
    "contacts",
    metadata,
    Column("user_id", Uuid, primary_key=True),
    Column("email", Text, nullable=False),
    Column("display_name", Text, nullable=False),
    Column("language", String(2), nullable=False),
    Column("updated_at", DateTime(timezone=True), nullable=False),
)

# Events that carry the contact's details. UserProfileChanged brings the saved name and language (FR-LOC-005).
# ExternalLoginLinked is the only event of an account created with Google (FEAT-004 D-3).
CONTACT_EVENTS = frozenset(
    {
        "UserRegistered",
        "VerificationEmailRequested",
        "UserProfileChanged",
        "ExternalLoginLinked",
        "SignInCodeRequested",
    }
)


async def upsert(conn: AsyncConnection, data: dict[str, Any]) -> None:
    values = {
        "user_id": uuid.UUID(data["userId"]),
        "email": data["email"],
        "display_name": data["displayName"],
        "language": data["language"],
        "updated_at": datetime.now(UTC),
    }
    stmt = insert(contacts).values(**values)
    stmt = stmt.on_conflict_do_update(
        index_elements=[contacts.c.user_id], set_={k: v for k, v in values.items() if k != "user_id"}
    )
    await conn.execute(stmt)
