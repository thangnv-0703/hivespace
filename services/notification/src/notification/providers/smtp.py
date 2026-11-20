"""SMTP sender; locally it talks to Mailpit. The only module that imports aiosmtplib."""

from __future__ import annotations

from email.message import EmailMessage as MimeMessage
from urllib.parse import urlparse

import aiosmtplib

from notification.providers.email import EmailMessage


def parse_endpoint(connection: str) -> tuple[str, int]:
    """Accepts the AppHost's reference ('smtp://host:port', optionally 'Endpoint=...') or 'host:port'."""
    value = connection.strip()
    if value.lower().startswith("endpoint="):
        value = value[len("endpoint=") :]
    value = value.split(";", 1)[0]
    parsed = urlparse(value if "://" in value else f"smtp://{value}")
    host = parsed.hostname or "localhost"
    # IPv4 directly: "localhost" can resolve to ::1 first on Windows with Docker Desktop and stall.
    return ("127.0.0.1" if host == "localhost" else host), parsed.port or 1025


class SmtpEmailSender:
    def __init__(self, connection: str, sender: str) -> None:
        self._host, self._port = parse_endpoint(connection)
        self._sender = sender

    async def send(self, message: EmailMessage) -> None:
        mime = MimeMessage()
        mime["From"] = self._sender
        mime["To"] = message.to
        mime["Subject"] = message.subject
        mime.set_content(message.text)
        mime.add_alternative(message.html, subtype="html")
        await aiosmtplib.send(mime, hostname=self._host, port=self._port, timeout=10)
