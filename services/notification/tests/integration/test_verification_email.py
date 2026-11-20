"""Event -> contact + email record + sent message, against a real Postgres (AC-7, AC-8, NFR-REL-005)."""

from __future__ import annotations

import uuid
from collections.abc import AsyncIterator, Iterator
from datetime import UTC, datetime, timedelta

import pytest
from sqlalchemy import text
from sqlalchemy.ext.asyncio import AsyncEngine, create_async_engine
from testcontainers.community.postgres import PostgresContainer

from hivespace_common.eventbus import Envelope
from notification.db import migrate
from notification.dispatch import make_handler
from notification.providers.email import EmailMessage

pytestmark = pytest.mark.integration

TOKEN = "tok-SECRET-9f8e7d"  # noqa: S105 - a marker searched for, not a credential
BASE_URL = "http://localhost:5080"


class FakeSender:
    def __init__(self, fail: bool = False) -> None:
        self.sent: list[EmailMessage] = []
        self.fail = fail

    async def send(self, message: EmailMessage) -> None:
        if self.fail:
            raise ConnectionRefusedError(f"smtp down while sending {message.text}")
        self.sent.append(message)


@pytest.fixture(scope="module")
def database_url() -> Iterator[str]:
    with PostgresContainer("postgres:17-alpine", driver="psycopg") as pg:
        url = pg.get_connection_url().replace("localhost", "127.0.0.1")
        migrate(url)
        yield url


@pytest.fixture
async def engine(database_url: str) -> AsyncIterator[AsyncEngine]:
    engine = create_async_engine(database_url)
    async with engine.begin() as conn:
        await conn.execute(text("TRUNCATE email_records, contacts, inbox"))
    yield engine
    await engine.dispose()


def _event(event_type: str = "UserRegistered", language: str = "vi") -> Envelope:
    return Envelope.model_validate(
        {
            "eventId": str(uuid.uuid4()),
            "eventType": event_type,
            "eventVersion": 1,
            "occurredAt": datetime.now(UTC).isoformat(),
            "producer": "identity",
            "correlationId": "corr-1",
            "data": {
                "userId": str(uuid.uuid4()),
                "email": "an@example.com",
                "displayName": "An",
                "language": language,
                "verificationToken": TOKEN,
                "verificationTokenExpiresAt": (datetime.now(UTC) + timedelta(hours=24)).isoformat(),
            },
        }
    )


async def _count(engine: AsyncEngine, table: str) -> int:
    async with engine.connect() as conn:
        return (await conn.execute(text(f"SELECT count(*) FROM {table}"))).scalar_one()  # noqa: S608 - test constant


async def test_registration_event_sends_one_email_in_the_registration_language(engine: AsyncEngine) -> None:
    sender = FakeSender()

    await make_handler(engine, sender, BASE_URL)(_event(language="en"))

    assert len(sender.sent) == 1
    message = sender.sent[0]
    assert message.to == "an@example.com"
    assert message.subject == "Verify your HiveSpace email"
    assert f"{BASE_URL}/verify-email?token={TOKEN}" in message.text
    async with engine.connect() as conn:
        status = (await conn.execute(text("SELECT status, attempts FROM email_records"))).one()
    assert (status.status, status.attempts) == ("sent", 1)
    assert await _count(engine, "contacts") == 1


async def test_the_same_event_delivered_twice_makes_one_email_and_one_record(engine: AsyncEngine) -> None:
    sender = FakeSender()
    handle = make_handler(engine, sender, BASE_URL)
    event = _event()

    await handle(event)
    await handle(event)

    assert len(sender.sent) == 1
    assert await _count(engine, "email_records") == 1


async def test_a_resend_event_is_its_own_email(engine: AsyncEngine) -> None:
    sender = FakeSender()
    handle = make_handler(engine, sender, BASE_URL)

    await handle(_event("UserRegistered"))
    await handle(_event("VerificationEmailRequested"))

    assert len(sender.sent) == 2
    assert await _count(engine, "email_records") == 2


async def test_a_failed_send_is_recorded_without_the_token_and_does_not_fail_the_event(
    engine: AsyncEngine,
) -> None:
    await make_handler(engine, FakeSender(fail=True), BASE_URL)(_event())

    async with engine.connect() as conn:
        row = (await conn.execute(text("SELECT status, last_error FROM email_records"))).one()
    assert row.status == "failed"
    assert TOKEN not in (row.last_error or "")


async def test_the_token_is_stored_nowhere(engine: AsyncEngine) -> None:
    await make_handler(engine, FakeSender(), BASE_URL)(_event())

    async with engine.connect() as conn:
        for table in ("email_records", "contacts", "inbox", "outbox"):
            dump = (await conn.execute(text(f"SELECT t::text FROM {table} t"))).scalars().all()  # noqa: S608
            assert all(TOKEN not in row for row in dump), table


# ---- FEAT-002: UserProfileChanged (AC-8) ----


def _profile_event(user_id: str, *, language: str = "en", name: str = "Lan Tran") -> Envelope:
    return Envelope.model_validate(
        {
            "eventId": str(uuid.uuid4()),
            "eventType": "UserProfileChanged",
            "eventVersion": 1,
            "occurredAt": datetime.now(UTC).isoformat(),
            "producer": "identity",
            "correlationId": user_id,
            "data": {
                "userId": user_id,
                "email": "an@example.com",
                "displayName": name,
                "language": language,
                "changedFields": ["displayName", "language"],
            },
        }
    )


async def _contact(engine: AsyncEngine, user_id: str) -> tuple[str, str, str]:
    async with engine.connect() as conn:
        row = (
            await conn.execute(
                text("SELECT email, display_name, language FROM contacts WHERE user_id = :id"),
                {"id": uuid.UUID(user_id)},
            )
        ).one()
    return (row.email, row.display_name, row.language)


async def test_a_profile_change_updates_the_contact_and_sends_no_email(engine: AsyncEngine) -> None:
    sender = FakeSender()
    handle = make_handler(engine, sender, BASE_URL)
    registered = _event("UserRegistered", "vi")
    user_id = registered.data["userId"]
    await handle(registered)

    await handle(_profile_event(user_id))

    assert await _contact(engine, user_id) == ("an@example.com", "Lan Tran", "en")
    assert len(sender.sent) == 1  # only the registration email
    assert await _count(engine, "email_records") == 1


async def test_the_same_profile_event_delivered_twice_updates_the_contact_once(engine: AsyncEngine) -> None:
    handle = make_handler(engine, FakeSender(), BASE_URL)
    user_id = str(uuid.uuid4())
    event = _profile_event(user_id)

    await handle(event)
    async with engine.connect() as conn:
        first = (await conn.execute(text("SELECT updated_at FROM contacts"))).scalar_one()
    await handle(event)
    async with engine.connect() as conn:
        second = (await conn.execute(text("SELECT updated_at FROM contacts"))).scalar_one()

    assert await _count(engine, "contacts") == 1
    assert await _count(engine, "inbox") == 1
    assert first == second  # the duplicate did not touch the row


async def test_a_later_email_is_sent_in_the_language_saved_in_the_profile(engine: AsyncEngine) -> None:
    sender = FakeSender()
    handle = make_handler(engine, sender, BASE_URL)
    registered = _event("UserRegistered", "vi")
    user_id = registered.data["userId"]
    await handle(registered)
    await handle(_profile_event(user_id, language="en"))

    # Identity builds the resend event from the saved profile, so it now carries "en".
    resend = _event("VerificationEmailRequested", "en")
    resend.data["userId"] = user_id
    await handle(resend)

    subjects = [m.subject for m in sender.sent]
    assert subjects == ["Xác minh email HiveSpace của bạn", "Verify your HiveSpace email"]
    assert (await _contact(engine, user_id))[2] == "en"


async def test_an_invalid_profile_event_changes_nothing_and_leaves_no_inbox_row(engine: AsyncEngine) -> None:
    handle = make_handler(engine, FakeSender(), BASE_URL)
    bad = _profile_event(str(uuid.uuid4()))
    bad.data["phone"] = "+84912345678"

    with pytest.raises(ValueError, match="phone"):
        await handle(bad)

    assert await _count(engine, "contacts") == 0
    assert await _count(engine, "inbox") == 0
