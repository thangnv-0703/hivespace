"""Routes each event to the features that handle it, in one transaction with the inbox row (NFR-REL-005)."""

from __future__ import annotations

import logging
from collections.abc import Awaitable, Callable

from sqlalchemy.ext.asyncio import AsyncEngine

from hivespace_common import inbox
from hivespace_common.eventbus import Envelope
from notification.features.contacts import repository as contacts
from notification.features.contacts.events import UserProfileChanged
from notification.features.emails import service as emails
from notification.providers.email import EmailSender

log = logging.getLogger(__name__)

CONSUMER = "notification"


def make_handler(
    engine: AsyncEngine, sender: EmailSender, public_base_url: str
) -> Callable[[Envelope], Awaitable[None]]:
    async def handle(envelope: Envelope) -> None:
        outgoing: emails.Outgoing | None = None
        async with engine.begin() as conn:
            if not await inbox.record(conn, envelope.event_id, CONSUMER):
                log.info("duplicate event ignored", extra={"eventId": envelope.event_id})
                return
            if envelope.event_type == "UserProfileChanged":
                # Validate against the contract before any write; a bad payload rolls back the inbox row too.
                UserProfileChanged.model_validate(envelope.data)
            if envelope.event_type in contacts.CONTACT_EVENTS:
                await contacts.upsert(conn, envelope.data)
            if envelope.event_type in emails.EMAIL_EVENTS:
                outgoing = await emails.prepare(conn, envelope.event_id, envelope.data, public_base_url)
        # After commit: a send failure must not roll back the record, and redelivery then hits the inbox.
        if outgoing is not None:
            await emails.deliver(engine, sender, outgoing)
        log.info("event processed: %s %s", envelope.event_type, envelope.event_id)

    return handle
