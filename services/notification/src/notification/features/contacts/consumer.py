"""Event handling for the contacts feature. Every event is processed once per consumer (NFR-REL-005)."""

from __future__ import annotations

import logging
from collections.abc import Awaitable, Callable
from typing import Protocol

from hivespace_common.eventbus import Envelope

log = logging.getLogger(__name__)

CONSUMER = "notification"


class InboxRecorder(Protocol):
    async def __call__(self, event_id: str, consumer: str) -> bool: ...


def make_handler(record: InboxRecorder) -> Callable[[Envelope], Awaitable[None]]:
    async def handle(envelope: Envelope) -> None:
        if not await record(envelope.event_id, CONSUMER):
            log.info("duplicate event ignored", extra={"eventId": envelope.event_id})
            return
        # Skeleton: Ping has no side effect beyond the inbox row.
        log.info("event processed: %s %s", envelope.event_type, envelope.event_id)

    return handle
