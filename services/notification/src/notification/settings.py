"""Configuration from the environment (pydantic-settings). Names are listed in .env.example."""

from __future__ import annotations

from pydantic import Field
from pydantic_settings import BaseSettings, SettingsConfigDict
from sqlalchemy.engine import make_url


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", extra="ignore", populate_by_name=True)

    database_url: str = Field(alias="DATABASE_URL")
    messaging_connection: str = Field(default="", alias="ConnectionStrings__messaging")
    events_topic: str = Field(default="hivespace-events", alias="EVENTS_TOPIC")
    events_subscription: str = Field(default="notification", alias="EVENTS_SUBSCRIPTION")

    mailpit_connection: str = Field(default="", alias="ConnectionStrings__mailpit")
    # Origin the emailed links point at: the public gateway, where the storefront serves /verify-email.
    public_base_url: str = Field(default="http://localhost:5080", alias="PUBLIC_BASE_URL")
    mail_from: str = Field(default="HiveSpace <no-reply@hivespace.local>", alias="MAIL_FROM")

    @property
    def async_database_url(self) -> str:
        """The AppHost gives a plain postgresql:// URI; SQLAlchemy needs the psycopg async driver name."""
        url = self.database_url
        for prefix in ("postgresql://", "postgres://"):
            if url.startswith(prefix):
                url = "postgresql+psycopg://" + url[len(prefix) :]
                break
        # psycopg's async connect tries only the first address "localhost" resolves to. On Windows with
        # Docker Desktop that is ::1, which accepts and never answers, so startup hangs. Use IPv4 directly.
        parsed = make_url(url)
        if parsed.host == "localhost":
            url = parsed.set(host="127.0.0.1").render_as_string(hide_password=False)
        return url
