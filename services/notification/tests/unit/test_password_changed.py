"""PasswordChanged/v1: the model agrees with the contract schema, and the email shows Vietnam time (AC-6)."""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any

import pytest
from pydantic import ValidationError

from notification.features.emails.events import PasswordChanged
from notification.features.emails.service import KINDS, render_kind

_SCHEMA_FILE = (
    Path(__file__).resolve().parents[4] / "contracts" / "events" / "PasswordChanged" / "v1.schema.json"
)
SCHEMA = json.loads(_SCHEMA_FILE.read_text(encoding="utf-8"))

VALID: dict[str, Any] = {
    "userId": "01930000-0000-7000-8000-000000000042",
    "email": "an@example.com",
    "displayName": "Lan Tran",
    "language": "en",
    "changedAt": "2026-11-22T17:30:00+00:00",
}


def test_the_model_has_exactly_the_properties_and_required_keys_of_the_schema() -> None:
    aliases = {field.alias for field in PasswordChanged.model_fields.values()}

    assert aliases == set(SCHEMA["properties"])
    assert aliases == set(SCHEMA["required"])
    assert SCHEMA["additionalProperties"] is False
    assert SCHEMA["properties"]["language"]["enum"] == ["vi", "en"]


def test_a_valid_payload_is_accepted() -> None:
    assert PasswordChanged.model_validate(VALID).display_name == "Lan Tran"


@pytest.mark.parametrize(
    "change",
    [
        {"password": "x"},
        {"language": "fr"},
        {"displayName": "n" * 81},
        {"userId": "no"},
        {"changedAt": "2026-11-22"},
    ],
)
def test_a_payload_the_schema_would_reject_is_rejected(change: dict[str, Any]) -> None:
    with pytest.raises(ValidationError):
        PasswordChanged.model_validate({**VALID, **change})


@pytest.mark.parametrize(
    ("language", "subject", "greeting"),
    [
        ("vi", "Mật khẩu HiveSpace của bạn đã được thay đổi", "Xin chào Lan Tran"),
        ("en", "Your HiveSpace password was changed", "Hello Lan Tran"),
    ],
)
def test_the_email_is_in_the_user_language_with_the_time_in_vietnam_and_no_link(
    language: str, subject: str, greeting: str
) -> None:
    kind = KINDS["PasswordChanged"]
    context = kind.context({**VALID, "language": language}, "http://localhost:5080")

    got_subject, text, html = render_kind(kind, language, context)

    assert got_subject == subject
    assert greeting in text
    assert "00:30 23/11/2026" in text  # 17:30 UTC is 00:30 the next day in Asia/Ho_Chi_Minh
    assert "00:30 23/11/2026" in html
    assert "http" not in text
    assert "href" not in html
