"""SignInCodeRequested/v1 (FEAT-006): the model matches the schema; the email carries the code (AC-13)."""

from __future__ import annotations

import json
from datetime import UTC, datetime, timedelta
from pathlib import Path
from typing import Any

import pytest
from pydantic import ValidationError

from notification.features.emails.events import SignInCodeRequested
from notification.features.emails.service import KINDS, render_kind

_SCHEMA_FILE = (
    Path(__file__).resolve().parents[4] / "contracts" / "events" / "SignInCodeRequested" / "v1.schema.json"
)
SCHEMA = json.loads(_SCHEMA_FILE.read_text(encoding="utf-8"))


def _valid(**change: Any) -> dict[str, Any]:
    return {
        "userId": "01930000-0000-7000-8000-000000000042",
        "email": "an@example.com",
        "displayName": "Lan Tran",
        "language": "en",
        "code": "042917",
        "expiresAt": (datetime.now(UTC) + timedelta(minutes=5)).isoformat(),
        **change,
    }


def test_the_model_has_exactly_the_properties_and_required_keys_of_the_schema() -> None:
    aliases = {field.alias for field in SignInCodeRequested.model_fields.values()}

    assert aliases == set(SCHEMA["properties"])
    assert aliases == set(SCHEMA["required"])
    assert SCHEMA["additionalProperties"] is False
    assert SCHEMA["properties"]["language"]["enum"] == ["vi", "en"]
    assert SCHEMA["properties"]["code"]["pattern"] == "^[0-9]{6}$"


def test_a_valid_payload_is_accepted() -> None:
    assert SignInCodeRequested.model_validate(_valid()).code == "042917"


@pytest.mark.parametrize(
    "change",
    [
        {"token": "x"},
        {"language": "fr"},
        {"code": "12345"},
        {"code": "1234567"},
        {"code": "12a456"},
        {"displayName": "n" * 81},
        {"userId": "no"},
        {"expiresAt": "2026-11-22"},
    ],
)
def test_a_payload_the_schema_would_reject_is_rejected(change: dict[str, Any]) -> None:
    with pytest.raises(ValidationError):
        SignInCodeRequested.model_validate(_valid(**change))


@pytest.mark.parametrize(
    ("language", "subject", "greeting", "validity"),
    [
        ("vi", "Mã đăng nhập HiveSpace của bạn", "Xin chào Lan Tran", "5 phút"),
        ("en", "Your HiveSpace sign-in code", "Hello Lan Tran", "5 minutes"),
    ],
)
def test_the_email_carries_the_code_and_its_five_minutes_in_the_user_language_and_no_link(
    language: str, subject: str, greeting: str, validity: str
) -> None:
    kind = KINDS["SignInCodeRequested"]
    context = kind.context(_valid(language=language), "http://localhost:5080")

    got_subject, text, html = render_kind(kind, language, context)

    assert got_subject == subject
    assert greeting in text
    assert "042917" in text
    assert "042917" in html
    assert validity in text
    assert validity in html
    assert "http" not in text
    assert "href" not in html


def test_the_minutes_round_up_and_never_go_below_one() -> None:
    kind = KINDS["SignInCodeRequested"]
    now = datetime.now(UTC)

    assert kind.context(_valid(expiresAt=(now + timedelta(seconds=150)).isoformat()), "")["minutes"] == 3
    assert kind.context(_valid(expiresAt=(now - timedelta(seconds=30)).isoformat()), "")["minutes"] == 1


def test_the_expiry_is_shown_in_vietnam_time() -> None:
    context = KINDS["SignInCodeRequested"].context(_valid(expiresAt="2026-11-22T17:30:00+00:00"), "")

    assert context["expires_at"] == "00:30 23/11/2026"
