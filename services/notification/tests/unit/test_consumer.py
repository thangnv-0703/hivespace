"""Duplicate delivery produces one side effect (NFR-REL-005, dev-standards §9.2 'Event duplication')."""

from __future__ import annotations

from datetime import UTC, datetime

from hivespace_common.eventbus import Envelope, decode
from notification.features.contacts.consumer import CONSUMER, make_handler


def _envelope() -> Envelope:
    return Envelope.model_validate(
        {
            "eventId": "01930000-0000-7000-8000-000000000042",
            "eventType": "Ping",
            "eventVersion": 1,
            "occurredAt": datetime.now(UTC).isoformat(),
            "producer": "catalog",
            "correlationId": "corr-1",
            "data": {"message": "hi"},
        }
    )


async def test_same_event_twice_is_processed_once() -> None:
    seen: set[tuple[str, str]] = set()
    processed: list[str] = []

    async def record(event_id: str, consumer: str) -> bool:
        key = (event_id, consumer)
        if key in seen:
            return False
        seen.add(key)
        processed.append(event_id)
        return True

    handle = make_handler(record)
    envelope = _envelope()

    await handle(envelope)
    await handle(envelope)

    assert processed == [envelope.event_id]
    assert (envelope.event_id, CONSUMER) in seen


def test_envelope_round_trips_camel_case_json() -> None:
    envelope = _envelope()

    decoded = decode(envelope.model_dump_json(by_alias=True))

    assert decoded == envelope
