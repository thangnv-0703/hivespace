"""Payload of UserProfileChanged/v1 (contracts/events), without private fields."""

from __future__ import annotations

from typing import Literal
from uuid import UUID

from pydantic import BaseModel, ConfigDict, Field
from pydantic.alias_generators import to_camel


class UserProfileChanged(BaseModel):
    """Mirrors the JSON schema; extra keys are rejected. A unit test keeps the two in step."""

    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=False, extra="forbid")

    user_id: UUID
    email: str = Field(max_length=254)
    display_name: str = Field(max_length=80)
    language: Literal["vi", "en"]
    changed_fields: list[Literal["displayName", "language"]] = Field(min_length=1)
