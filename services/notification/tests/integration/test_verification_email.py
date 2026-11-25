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


# ---- FEAT-003: PasswordChanged (AC-6) ----


def _password_event(language: str = "vi") -> Envelope:
    return Envelope.model_validate(
        {
            "eventId": str(uuid.uuid4()),
            "eventType": "PasswordChanged",
            "eventVersion": 1,
            "occurredAt": datetime.now(UTC).isoformat(),
            "producer": "identity",
            "correlationId": "corr-1",
            "data": {
                "userId": str(uuid.uuid4()),
                "email": "an@example.com",
                "displayName": "An",
                "language": language,
                "changedAt": "2026-11-22T17:30:00+00:00",
            },
        }
    )


@pytest.mark.parametrize(
    ("language", "subject"),
    [("vi", "Mật khẩu HiveSpace của bạn đã được thay đổi"), ("en", "Your HiveSpace password was changed")],
)
async def test_a_password_change_sends_one_email_with_its_own_template(
    engine: AsyncEngine, language: str, subject: str
) -> None:
    sender = FakeSender()

    await make_handler(engine, sender, BASE_URL)(_password_event(language))

    assert [m.subject for m in sender.sent] == [subject]
    assert "00:30 23/11/2026" in sender.sent[0].text
    async with engine.connect() as conn:
        row = (await conn.execute(text("SELECT template, status FROM email_records"))).one()
    assert (row.template, row.status) == ("password_changed", "sent")


async def test_a_password_event_delivered_twice_sends_once(engine: AsyncEngine) -> None:
    sender = FakeSender()
    handle = make_handler(engine, sender, BASE_URL)
    event = _password_event()

    await handle(event)
    await handle(event)

    assert len(sender.sent) == 1
    assert await _count(engine, "email_records") == 1


async def test_an_invalid_password_event_writes_nothing_and_sends_nothing(engine: AsyncEngine) -> None:
    sender = FakeSender()
    bad = _password_event()
    bad.data["password"] = "leaked"  # noqa: S105 - an extra key the contract forbids

    with pytest.raises(ValueError, match="password"):
        await make_handler(engine, sender, BASE_URL)(bad)

    assert sender.sent == []
    assert await _count(engine, "email_records") == 0
    assert await _count(engine, "inbox") == 0


# ---- FEAT-004: ExternalLoginLinked (AC-12) ----


def _linked_event(
    language: str = "vi", account_created: bool = False, password_removed: bool = False
) -> Envelope:
    return Envelope.model_validate(
        {
            "eventId": str(uuid.uuid4()),
            "eventType": "ExternalLoginLinked",
            "eventVersion": 1,
            "occurredAt": datetime.now(UTC).isoformat(),
            "producer": "identity",
            "correlationId": "corr-1",
            "data": {
                "userId": str(uuid.uuid4()),
                "email": "an@gmail.com",
                "displayName": "An",
                "language": language,
                "provider": "google",
                "accountCreated": account_created,
                "passwordRemoved": password_removed,
                "linkedAt": "2026-11-22T17:30:00+00:00",
            },
        }
    )


@pytest.mark.parametrize(
    ("language", "subject", "removed"),
    [
        ("vi", "Tài khoản Google đã được liên kết với HiveSpace", "mật khẩu trước đây đã bị xóa"),
        ("en", "Google account linked to HiveSpace", "the earlier password was removed"),
    ],
)
@pytest.mark.parametrize("password_removed", [False, True])
async def test_a_link_to_an_existing_account_sends_one_notice_in_the_user_language(
    engine: AsyncEngine, language: str, subject: str, removed: str, password_removed: bool
) -> None:
    sender = FakeSender()

    await make_handler(engine, sender, BASE_URL)(_linked_event(language, password_removed=password_removed))

    assert [m.subject for m in sender.sent] == [subject]
    assert sender.sent[0].to == "an@gmail.com"
    assert "00:30 23/11/2026" in sender.sent[0].text
    assert (removed in sender.sent[0].text) is password_removed
    async with engine.connect() as conn:
        row = (await conn.execute(text("SELECT template, status, language FROM email_records"))).one()
    assert (row.template, row.status, row.language) == ("external_login_linked", "sent", language)
    assert await _count(engine, "contacts") == 1


async def test_a_google_created_account_sends_nothing_but_gets_a_contact(engine: AsyncEngine) -> None:
    sender = FakeSender()
    event = _linked_event("en", account_created=True)

    await make_handler(engine, sender, BASE_URL)(event)

    assert sender.sent == []
    assert await _count(engine, "email_records") == 0
    async with engine.connect() as conn:
        contact = (await conn.execute(text("SELECT email, display_name, language FROM contacts"))).one()
    assert (contact.email, contact.display_name, contact.language) == ("an@gmail.com", "An", "en")
    assert await _count(engine, "inbox") == 1


async def test_a_link_event_delivered_twice_sends_once(engine: AsyncEngine) -> None:
    sender = FakeSender()
    handle = make_handler(engine, sender, BASE_URL)
    event = _linked_event()

    await handle(event)
    await handle(event)

    assert len(sender.sent) == 1
    assert await _count(engine, "email_records") == 1


async def test_an_invalid_link_event_writes_nothing_and_sends_nothing(engine: AsyncEngine) -> None:
    sender = FakeSender()
    bad = _linked_event()
    bad.data["idToken"] = "eyJ-leaked"

    with pytest.raises(ValueError, match="idToken"):
        await make_handler(engine, sender, BASE_URL)(bad)

    assert sender.sent == []
    assert await _count(engine, "email_records") == 0
    assert await _count(engine, "contacts") == 0
    assert await _count(engine, "inbox") == 0


# ---- FEAT-006: SignInCodeRequested (AC-13) ----

CODE = "042917"


def _code_event(user_id: str | None = None, *, language: str = "vi") -> Envelope:
    return Envelope.model_validate(
        {
            "eventId": str(uuid.uuid4()),
            "eventType": "SignInCodeRequested",
            "eventVersion": 1,
            "occurredAt": datetime.now(UTC).isoformat(),
            "producer": "identity",
            "correlationId": "corr-1",
            "data": {
                "userId": user_id or str(uuid.uuid4()),
                "email": "an@example.com",
                "displayName": "An",
                "language": language,
                "code": CODE,
                "expiresAt": (datetime.now(UTC) + timedelta(minutes=5)).isoformat(),
            },
        }
    )


@pytest.mark.parametrize(
    ("language", "subject", "validity"),
    [("vi", "Mã đăng nhập HiveSpace của bạn", "5 phút"), ("en", "Your HiveSpace sign-in code", "5 minutes")],
)
async def test_a_code_event_sends_one_sign_in_code_email_in_its_language(
    engine: AsyncEngine, language: str, subject: str, validity: str
) -> None:
    sender = FakeSender()

    await make_handler(engine, sender, BASE_URL)(_code_event(language=language))

    assert [(m.to, m.subject) for m in sender.sent] == [("an@example.com", subject)]
    assert CODE in sender.sent[0].text
    assert validity in sender.sent[0].text
    async with engine.connect() as conn:
        row = (await conn.execute(text("SELECT template, status, language FROM email_records"))).one()
    assert (row.template, row.status, row.language) == ("sign_in_code", "sent", language)


async def test_a_code_event_delivered_twice_sends_once(engine: AsyncEngine) -> None:
    sender = FakeSender()
    handle = make_handler(engine, sender, BASE_URL)
    event = _code_event()

    await handle(event)
    await handle(event)

    assert len(sender.sent) == 1
    assert await _count(engine, "email_records") == 1


@pytest.mark.parametrize(("key", "value"), [("token", "extra"), ("code", "12345"), ("code", "abcdef")])
async def test_an_invalid_code_event_writes_nothing_and_sends_nothing(
    engine: AsyncEngine, key: str, value: str
) -> None:
    sender = FakeSender()
    bad = _code_event()
    bad.data[key] = value

    with pytest.raises(ValueError, match=key):
        await make_handler(engine, sender, BASE_URL)(bad)

    assert sender.sent == []
    assert await _count(engine, "email_records") == 0
    assert await _count(engine, "contacts") == 0
    assert await _count(engine, "inbox") == 0


async def test_a_code_event_upserts_the_contact_and_stores_no_code(engine: AsyncEngine) -> None:
    user_id = str(uuid.uuid4())

    await make_handler(engine, FakeSender(), BASE_URL)(_code_event(user_id, language="en"))

    assert await _contact(engine, user_id) == ("an@example.com", "An", "en")
    async with engine.connect() as conn:
        for table in ("email_records", "contacts", "inbox"):
            dump = (await conn.execute(text(f"SELECT t::text FROM {table} t"))).scalars().all()  # noqa: S608
            assert all(CODE not in row for row in dump), table


# ---- FEAT-009: ShopRegistered has no handler here (AC-8, D-6) ----


def _shop_registered_event() -> Envelope:
    shop_id = str(uuid.uuid4())
    return Envelope.model_validate(
        {
            "eventId": str(uuid.uuid4()),
            "eventType": "ShopRegistered",
            "eventVersion": 1,
            "occurredAt": datetime.now(UTC).isoformat(),
            "producer": "identity",
            "correlationId": shop_id,
            "data": {
                "shopId": shop_id,
                "ownerUserId": str(uuid.uuid4()),
                "name": "Tiệm Bánh An",
                "sellerType": "individual",
                "registeredAt": datetime.now(UTC).isoformat(),
            },
        }
    )


async def test_a_shop_registered_event_delivered_twice_is_acknowledged_with_no_email_and_one_inbox_row(
    engine: AsyncEngine,
) -> None:
    sender = FakeSender()
    handle = make_handler(engine, sender, BASE_URL)
    event = _shop_registered_event()

    await handle(event)
    await handle(event)

    assert sender.sent == []
    assert await _count(engine, "inbox") == 1
    assert await _count(engine, "email_records") == 0
    assert await _count(engine, "contacts") == 0


# ---- FEAT-010: ShopChanged has no handler here either (design §5.4) ----


def _shop_changed_event() -> Envelope:
    shop_id = str(uuid.uuid4())
    return Envelope.model_validate(
        {
            "eventId": str(uuid.uuid4()),
            "eventType": "ShopChanged",
            "eventVersion": 1,
            "occurredAt": datetime.now(UTC).isoformat(),
            "producer": "identity",
            "correlationId": shop_id,
            "data": {
                "shopId": shop_id,
                "ownerUserId": str(uuid.uuid4()),
                "name": "Tiệm Bánh An",
                "description": None,
                "sellerType": "individual",
                "setupComplete": True,
                "version": 2,
                "changedAt": datetime.now(UTC).isoformat(),
            },
        }
    )


async def test_a_shop_changed_event_delivered_twice_is_acknowledged_with_no_email_and_one_inbox_row(
    engine: AsyncEngine,
) -> None:
    sender = FakeSender()
    handle = make_handler(engine, sender, BASE_URL)
    event = _shop_changed_event()

    await handle(event)
    await handle(event)

    assert sender.sent == []
    assert await _count(engine, "inbox") == 1
    assert await _count(engine, "email_records") == 0
    assert await _count(engine, "contacts") == 0
