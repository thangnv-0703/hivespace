"""Event envelope (contracts/events/envelope.schema.json) and a Service Bus subscription receiver (ADR-0002)."""

from __future__ import annotations

import asyncio
import logging
from collections.abc import Awaitable, Callable
from datetime import datetime
from typing import Any

from azure.servicebus import ServiceBusReceivedMessage
from azure.servicebus.aio import ServiceBusClient
from opentelemetry import propagate, trace
from pydantic import BaseModel, ConfigDict, Field

log = logging.getLogger(__name__)
tracer = trace.get_tracer("hivespace.eventbus")


class Envelope(BaseModel):
    """Mirrors the JSON schema; field names are camelCase on the wire."""

    model_config = ConfigDict(populate_by_name=True, frozen=True)

    event_id: str = Field(alias="eventId")
    event_type: str = Field(alias="eventType")
    event_version: int = Field(alias="eventVersion")
    occurred_at: datetime = Field(alias="occurredAt")
    producer: str
    correlation_id: str = Field(alias="correlationId")
    traceparent: str | None = None
    data: dict[str, Any]


Handler = Callable[[Envelope], Awaitable[None]]


def decode(body: bytes | str) -> Envelope:
    return Envelope.model_validate_json(body)


async def run_subscription(connection_string: str, topic: str, subscription: str, handle: Handler) -> None:
    """Receive until cancelled. At-least-once delivery: handlers must be idempotent (NFR-REL-005)."""
    while True:
        try:
            async with ServiceBusClient.from_connection_string(connection_string) as client:
                receiver = client.get_subscription_receiver(topic_name=topic, subscription_name=subscription)
                async with receiver:
                    while True:
                        messages = await receiver.receive_messages(max_message_count=10, max_wait_time=5)
                        for message in messages:
                            await _process(receiver, message, handle)
        except asyncio.CancelledError:
            raise
        except Exception:  # noqa: BLE001 - the loop must survive broker outages and retry
            log.warning("receive failed; retrying", exc_info=True)
            await asyncio.sleep(2)


async def _process(receiver: Any, message: ServiceBusReceivedMessage, handle: Handler) -> None:
    try:
        envelope = decode(b"".join(message.body))
    except ValueError:
        log.exception("malformed envelope; dead-lettering")
        await receiver.dead_letter_message(message)
        return

    # Continue the producer's trace so one flow is one trace (NFR-OBS-001).
    ctx = propagate.extract({"traceparent": envelope.traceparent or ""})
    with tracer.start_as_current_span(f"consume {envelope.event_type}", context=ctx, kind=trace.SpanKind.CONSUMER):
        try:
            await handle(envelope)
        except Exception:
            log.exception("handler failed; abandoning for redelivery", extra={"eventId": envelope.event_id})
            await receiver.abandon_message(message)
            return
    await receiver.complete_message(message)
