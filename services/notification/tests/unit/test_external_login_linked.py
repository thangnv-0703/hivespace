"""ExternalLoginLinked/v1 (FEAT-004 AC-12): the model matches the schema; the notice renders right."""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any

import pytest
from pydantic import ValidationError

from notification.features.emails.events import ExternalLoginLinked
from notification.features.emails.service import KINDS, render_kind

_SCHEMA_FILE = (
    Path(__file__).resolve().parents[4] / "contracts" / "events" / "ExternalLoginLinked" / "v1.schema.json"
)
SCHEMA = json.loads(_SCHEMA_FILE.read_text(encoding="utf-8"))

VALID: dict[str, Any] = {
    "userId": "01930000-0000-7000-8000-000000000042",
    "email": "an@gmail.com",
    "displayName": "Lan Tran",
    "language": "en",
    "provider": "google",
    "accountCreated": False,
    "passwordRemoved": False,
    "linkedAt": "2026-11-22T17:30:00+00:00",
}


def test_the_model_has_exactly_the_properties_and_required_keys_of_the_schema() -> None:
    aliases = {field.alias for field in ExternalLoginLinked.model_fields.values()}

    assert aliases == set(SCHEMA["properties"])
    assert aliases == set(SCHEMA["required"])
    assert SCHEMA["additionalProperties"] is False
    assert SCHEMA["properties"]["language"]["enum"] == ["vi", "en"]
    assert SCHEMA["properties"]["provider"]["enum"] == ["google"]


def test_a_valid_payload_is_accepted() -> None:
    event = ExternalLoginLinked.model_validate(VALID)

    assert (event.display_name, event.account_created, event.password_removed) == ("Lan Tran", False, False)


@pytest.mark.parametrize(
    "change",
    [
        {"token": "x"},
        {"subject": "1234"},
        {"provider": "facebook"},
        {"language": "fr"},
        {"displayName": "n" * 81},
        {"accountCreated": "maybe"},
        {"linkedAt": "2026-11-22"},
    ],
)
def test_a_payload_the_schema_would_reject_is_rejected(change: dict[str, Any]) -> None:
    with pytest.raises(ValidationError):
        ExternalLoginLinked.model_validate({**VALID, **change})


@pytest.mark.parametrize("missing", sorted(SCHEMA["required"]))
def test_every_required_key_is_required(missing: str) -> None:
    with pytest.raises(ValidationError):
        ExternalLoginLinked.model_validate({k: v for k, v in VALID.items() if k != missing})


@pytest.mark.parametrize(
    ("language", "subject", "greeting", "removed"),
    [
        (
            "vi",
            "Tài khoản Google đã được liên kết với HiveSpace",
            "Xin chào Lan Tran",
            "mật khẩu trước đây đã bị xóa",
        ),
        ("en", "Google account linked to HiveSpace", "Hello Lan Tran", "the earlier password was removed"),
    ],
)
def test_the_notice_is_in_the_user_language_with_vietnam_time_and_the_password_paragraph_only_when_removed(
    language: str, subject: str, greeting: str, removed: str
) -> None:
    kind = KINDS["ExternalLoginLinked"]

    for password_removed in (False, True):
        data = {**VALID, "language": language, "passwordRemoved": password_removed}
        got_subject, text, html = render_kind(kind, language, kind.context(data, "http://localhost:5080"))

        assert got_subject == subject
        assert greeting in text
        assert greeting in html
        assert "00:30 23/11/2026" in text  # 17:30 UTC is 00:30 the next day in Asia/Ho_Chi_Minh
        assert "00:30 23/11/2026" in html
        assert (removed in text) is password_removed
        assert (removed in html) is password_removed
        # The only link is the set-a-password one, in the removed-password paragraph (FEAT-005 AC-18).
        assert ("http://localhost:5080/profile/password" in text) is password_removed
        assert ('href="http://localhost:5080/profile/password"' in html) is password_removed
        assert text.count("http") == (1 if password_removed else 0)
        assert html.count("http") == (1 if password_removed else 0)


def test_only_a_link_to_an_existing_account_asks_for_the_email() -> None:
    kind = KINDS["ExternalLoginLinked"]

    assert kind.applies({**VALID, "accountCreated": False}) is True
    assert kind.applies({**VALID, "accountCreated": True}) is False
    assert KINDS["PasswordChanged"].applies({}) is True


@pytest.mark.parametrize("language", ["vi", "en"])
def test_ac18_the_removed_password_notice_links_to_the_set_password_page(language: str) -> None:
    kind = KINDS["ExternalLoginLinked"]
    data = {**VALID, "language": language, "passwordRemoved": True}

    _, text, html = render_kind(kind, language, kind.context(data, "https://hivespace.example/"))

    assert "https://hivespace.example/profile/password" in text
    assert 'href="https://hivespace.example/profile/password"' in html
