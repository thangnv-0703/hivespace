"""Verification emails: one email_records row per (event, template), sent after commit (design 5.5)."""

from __future__ import annotations

import logging
import uuid
from dataclasses import dataclass
from datetime import UTC, datetime
from pathlib import Path
from typing import Any
from urllib.parse import quote

from jinja2 import Environment, FileSystemLoader, StrictUndefined, select_autoescape
from sqlalchemy import Column, DateTime, Integer, MetaData, String, Table, Text, Uuid, update
from sqlalchemy.dialects.postgresql import insert
from sqlalchemy.ext.asyncio import AsyncConnection, AsyncEngine

from notification.providers.email import EmailMessage, EmailSender

log = logging.getLogger(__name__)

metadata = MetaData()

email_records = Table(
    "email_records",
    metadata,
    Column("id", Uuid, primary_key=True),
    Column("event_id", Uuid, nullable=False),
    Column("template", String(64), nullable=False),
    Column("recipient_user_id", Uuid, nullable=False),
    Column("recipient_email", Text, nullable=False),
    Column("language", String(2)),
    Column("status", String(16), nullable=False),
    Column("attempts", Integer, nullable=False, default=0),
    Column("created_at", DateTime(timezone=True), nullable=False),
    Column("sent_at", DateTime(timezone=True)),
    Column("last_error", Text),
)

# Events that ask for a verification email.
EMAIL_EVENTS = frozenset({"UserRegistered", "VerificationEmailRequested"})
TEMPLATE = "verification"
LANGUAGES = ("vi", "en")
SUBJECTS = {"vi": "Xác minh email HiveSpace của bạn", "en": "Verify your HiveSpace email"}

_env = Environment(
    loader=FileSystemLoader(Path(__file__).parent / "templates"),
    autoescape=select_autoescape(["html"]),
    undefined=StrictUndefined,
)


@dataclass(frozen=True)
class Outgoing:
    """A prepared email. The rendered body (with the token) stays in memory; it is never stored or logged."""

    record_id: uuid.UUID
    message: EmailMessage


def render(language: str, display_name: str, link: str) -> tuple[str, str, str]:
    lang = language if language in LANGUAGES else "vi"
    ctx = {"display_name": display_name, "link": link}
    text = _env.get_template(f"{TEMPLATE}.{lang}.txt").render(**ctx)
    html = _env.get_template(f"{TEMPLATE}.{lang}.html").render(**ctx)
    return SUBJECTS[lang], text, html


async def prepare(
    conn: AsyncConnection, event_id: str, data: dict[str, Any], public_base_url: str
) -> Outgoing | None:
    """Insert the pending record in the caller's transaction. None if this (event, template) has one."""
    record_id = uuid.uuid4()
    stmt = (
        insert(email_records)
        .values(
            id=record_id,
            event_id=uuid.UUID(event_id),
            template=TEMPLATE,
            recipient_user_id=uuid.UUID(data["userId"]),
            recipient_email=data["email"],
            language=data["language"],
            status="pending",
            attempts=0,
            created_at=datetime.now(UTC),
        )
        .on_conflict_do_nothing(constraint="uq_email_records_event_template")
    )
    # RETURNING, not rowcount: a conflict-skipped insert reports -1 on this driver.
    if (await conn.execute(stmt.returning(email_records.c.id))).scalar_one_or_none() is None:
        return None
    link = f"{public_base_url.rstrip('/')}/verify-email?token={quote(data['verificationToken'], safe='')}"
    subject, text, html = render(data["language"], data["displayName"], link)
    return Outgoing(record_id, EmailMessage(to=data["email"], subject=subject, text=text, html=html))


async def deliver(engine: AsyncEngine, sender: EmailSender, outgoing: Outgoing) -> None:
    """Send, then mark the record. A failure is recorded without the message; it does not fail the event."""
    values: dict[str, Any]
    try:
        await sender.send(outgoing.message)
    except Exception as exc:  # noqa: BLE001 - any send failure is recorded; retries are FEAT-032
        # The exception type only: SMTP error text can echo the message, which carries the token.
        error = type(exc).__name__
        log.warning("verification email failed: %s", error, extra={"recordId": str(outgoing.record_id)})
        values = {"status": "failed", "last_error": error}
    else:
        values = {"status": "sent", "sent_at": datetime.now(UTC)}
    async with engine.begin() as conn:
        await conn.execute(
            update(email_records)
            .where(email_records.c.id == outgoing.record_id)
            .values(attempts=email_records.c.attempts + 1, **values)
        )
