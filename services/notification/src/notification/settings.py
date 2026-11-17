"""Configuration from the environment (pydantic-settings). Names are listed in .env.example."""

from __future__ import annotations

from pydantic import Field
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", extra="ignore", populate_by_name=True)

    database_url: str = Field(alias="DATABASE_URL")
    messaging_connection: str = Field(default="", alias="ConnectionStrings__messaging")
    events_topic: str = Field(default="hivespace-events", alias="EVENTS_TOPIC")
    events_subscription: str = Field(default="notification", alias="EVENTS_SUBSCRIPTION")

    @property
    def async_database_url(self) -> str:
        """The AppHost gives a plain postgresql:// URI; SQLAlchemy needs the psycopg async driver name."""
        url = self.database_url
        for prefix in ("postgresql://", "postgres://"):
            if url.startswith(prefix):
                return "postgresql+psycopg://" + url[len(prefix) :]
        return url
