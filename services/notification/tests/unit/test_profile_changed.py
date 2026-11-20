"""UserProfileChanged/v1: the model agrees with the contract schema and rejects what it rejects (AC-8)."""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any

import pytest
from pydantic import ValidationError

from notification.features.contacts.events import UserProfileChanged

_SCHEMA_FILE = (
    Path(__file__).resolve().parents[4] / "contracts" / "events" / "UserProfileChanged" / "v1.schema.json"
)
SCHEMA = json.loads(_SCHEMA_FILE.read_text(encoding="utf-8"))

VALID: dict[str, Any] = {
    "userId": "01930000-0000-7000-8000-000000000042",
    "email": "an@example.com",
    "displayName": "Lan Tran",
    "language": "en",
    "changedFields": ["displayName"],
}


def test_the_model_has_exactly_the_properties_and_required_keys_of_the_schema() -> None:
    aliases = {field.alias for field in UserProfileChanged.model_fields.values()}

    assert aliases == set(SCHEMA["properties"])
    assert aliases == set(SCHEMA["required"])
    assert SCHEMA["additionalProperties"] is False


def test_the_model_allows_the_languages_and_changed_fields_the_schema_lists() -> None:
    assert SCHEMA["properties"]["language"]["enum"] == ["vi", "en"]
    assert SCHEMA["properties"]["changedFields"]["items"]["enum"] == ["displayName", "language"]
    assert SCHEMA["properties"]["displayName"]["maxLength"] == 80


def test_a_valid_payload_is_accepted() -> None:
    event = UserProfileChanged.model_validate(VALID)

    assert (event.display_name, event.language) == ("Lan Tran", "en")


@pytest.mark.parametrize(
    "change",
    [
        {"phone": "+84912345678"},
        {"gender": "female"},
        {"dateOfBirth": "1995-04-30"},
        {"language": "fr"},
        {"changedFields": []},
        {"changedFields": ["phone"]},
        {"displayName": "n" * 81},
        {"userId": "not-a-uuid"},
    ],
)
def test_a_payload_the_schema_would_reject_is_rejected(change: dict[str, Any]) -> None:
    with pytest.raises(ValidationError):
        UserProfileChanged.model_validate({**VALID, **change})


def test_a_missing_key_is_rejected() -> None:
    payload = {k: v for k, v in VALID.items() if k != "email"}

    with pytest.raises(ValidationError):
        UserProfileChanged.model_validate(payload)
