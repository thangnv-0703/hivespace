"""Verification email content and the SMTP endpoint parsing (FR-NOTIF-002, FR-LOC-005)."""

from __future__ import annotations

from datetime import UTC, datetime

import pytest

from hivespace_common.eventbus import Envelope, decode
from notification.features.emails.service import render
from notification.providers.smtp import parse_endpoint

LINK = "http://localhost:5080/verify-email?token=abc123"


def test_vietnamese_template_greets_in_vietnamese_and_carries_the_link() -> None:
    subject, text, html = render("vi", "An", LINK)

    assert subject == "Xác minh email HiveSpace của bạn"
    assert "Xin chào An" in text
    assert LINK in text
    assert 'href="' + LINK + '"' in html


def test_english_template_greets_in_english_and_carries_the_link() -> None:
    subject, text, html = render("en", "An", LINK)

    assert subject == "Verify your HiveSpace email"
    assert "Hello An" in text
    assert LINK in text
    assert 'href="' + LINK + '"' in html


def test_unknown_language_falls_back_to_vietnamese() -> None:
    subject, _, _ = render("fr", "An", LINK)

    assert subject == "Xác minh email HiveSpace của bạn"


def test_html_body_escapes_the_display_name() -> None:
    _, _, html = render("en", "<script>x</script>", LINK)

    assert "<script>" not in html
    assert "&lt;script&gt;" in html


@pytest.mark.parametrize(
    ("connection", "expected"),
    [
        ("smtp://localhost:32800", ("127.0.0.1", 32800)),
        ("Endpoint=smtp://mailpit:1025", ("mailpit", 1025)),
        ("mailpit:2525", ("mailpit", 2525)),
        ("smtp://mailpit", ("mailpit", 1025)),
    ],
)
def test_smtp_endpoint_is_parsed_from_the_apphost_reference(
    connection: str, expected: tuple[str, int]
) -> None:
    assert parse_endpoint(connection) == expected


def test_envelope_round_trips_camel_case_json() -> None:
    envelope = Envelope.model_validate(
        {
            "eventId": "01930000-0000-7000-8000-000000000042",
            "eventType": "UserRegistered",
            "eventVersion": 1,
            "occurredAt": datetime.now(UTC).isoformat(),
            "producer": "identity",
            "correlationId": "corr-1",
            "data": {"userId": "x"},
        }
    )

    assert decode(envelope.model_dump_json(by_alias=True)) == envelope
