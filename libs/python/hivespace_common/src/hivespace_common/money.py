"""Money as an amount in minor units plus a currency code (ADR-0010). Never a float, never a bare number."""

from __future__ import annotations

from dataclasses import dataclass


@dataclass(frozen=True, slots=True)
class Money:
    amount: int
    currency: str

    def __add__(self, other: Money) -> Money:
        if self.currency != other.currency:
            raise ValueError(f"cannot add {other.currency} to {self.currency}")
        return Money(self.amount + other.amount, self.currency)
