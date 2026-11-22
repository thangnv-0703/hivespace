"""Transactional emails: one email_records row per (event, template), sent after commit (design 5.5)."""

from __future__ import annotations

import logging
import uuid
from collections.abc import Callable
from dataclasses import dataclass
from datetime import UTC, datetime
from pathlib import Path
from typing import Any
from urllib.parse import quote
from zoneinfo import ZoneInfo

from jinja2 import Environment, FileSystemLoader, StrictUndefined, select_autoescape
from sqlalchemy import Column, DateTime, Integer, MetaData, String, Table, Text, Uuid, update
from sqlalchemy.dialects.postgresql import insert
from sqlalchemy.ext.asyncio import AsyncConnection, AsyncEngine

from notification.features.emails.events import PasswordChanged
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

LANGUAGES = ("vi", "en")
VERIFICATION = "verification"
PASSWORD_CHANGED = "password_changed"  # noqa: S105 - a template name, not a credential
VIETNAM = ZoneInfo("Asia/Ho_Chi_Minh")  # NFR-L10N-002: times are shown in Vietnam time


@dataclass(frozen=True)
class EmailKind:
    """One email an event asks for: the template, its subjects and how its context comes from the payload."""

    template: str
    subjects: dict[str, str]
    context: Callable[[dict[str, Any], str], dict[str, Any]]


def _verification_context(data: dict[str, Any], public_base_url: str) -> dict[str, Any]:
    link = f"{public_base_url.rstrip('/')}/verify-email?token={quote(data['verificationToken'], safe='')}"
    return {"display_name": data["displayName"], "link": link}


def _password_changed_context(data: dict[str, Any], _public_base_url: str) -> dict[str, Any]:
    # Validated before any write (dispatch); no link and no token in this email.
    event = PasswordChanged.model_validate(data)
    return {
        "display_name": event.display_name,
        "changed_at": event.changed_at.astimezone(VIETNAM).strftime("%H:%M %d/%m/%Y"),
    }


# The template table, keyed by event type.
KINDS: dict[str, EmailKind] = {
    "UserRegistered": EmailKind(
        VERIFICATION,
        {"vi": "Xác minh email HiveSpace của bạn", "en": "Verify your HiveSpace email"},
        _verification_context,
    ),
    "VerificationEmailRequested": EmailKind(
        VERIFICATION,
        {"vi": "Xác minh email HiveSpace của bạn", "en": "Verify your HiveSpace email"},
        _verification_context,
    ),
    "PasswordChanged": EmailKind(
        PASSWORD_CHANGED,
        {"vi": "Mật khẩu HiveSpace của bạn đã được thay đổi", "en": "Your HiveSpace password was changed"},
        _password_changed_context,
    ),
}
EMAIL_EVENTS = frozenset(KINDS)

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


def render_kind(kind: EmailKind, language: str, context: dict[str, Any]) -> tuple[str, str, str]:
    lang = language if language in LANGUAGES else "vi"
    text = _env.get_template(f"{kind.template}.{lang}.txt").render(**context)
    html = _env.get_template(f"{kind.template}.{lang}.html").render(**context)
    return kind.subjects[lang], text, html


def render(language: str, display_name: str, link: str) -> tuple[str, str, str]:
    """The verification email."""
    return render_kind(KINDS["UserRegistered"], language, {"display_name": display_name, "link": link})


async def prepare(
    conn: AsyncConnection, event_type: str, event_id: str, data: dict[str, Any], public_base_url: str
) -> Outgoing | None:
    """Insert the pending record in the caller's transaction. None if this (event, template) has one."""
    kind = KINDS[event_type]
    record_id = uuid.uuid4()
    stmt = (
        insert(email_records)
        .values(
            id=record_id,
            event_id=uuid.UUID(event_id),
            template=kind.template,
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
    context = kind.context(data, public_base_url)
    subject, text, html = render_kind(kind, data["language"], context)
    return Outgoing(record_id, EmailMessage(to=data["email"], subject=subject, text=text, html=html))


async def deliver(engine: AsyncEngine, sender: EmailSender, outgoing: Outgoing) -> None:
    """Send, then mark the record. A failure is recorded without the message; it does not fail the event."""
    values: dict[str, Any]
    try:
        await sender.send(outgoing.message)
    except Exception as exc:  # noqa: BLE001 - any send failure is recorded; retries are FEAT-032
        # The exception type only: SMTP error text can echo the message, which carries the token.
        error = type(exc).__name__
        log.warning("email failed: %s", error, extra={"recordId": str(outgoing.record_id)})
        values = {"status": "failed", "last_error": error}
    else:
        values = {"status": "sent", "sent_at": datetime.now(UTC)}
    async with engine.begin() as conn:
        await conn.execute(
            update(email_records)
            .where(email_records.c.id == outgoing.record_id)
            .values(attempts=email_records.c.attempts + 1, **values)
        )
