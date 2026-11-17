from google.protobuf import descriptor as _descriptor
from google.protobuf import message as _message
from typing import ClassVar as _ClassVar, Optional as _Optional

DESCRIPTOR: _descriptor.FileDescriptor

class Money(_message.Message):
    __slots__ = ("amount", "currency")
    AMOUNT_FIELD_NUMBER: _ClassVar[int]
    CURRENCY_FIELD_NUMBER: _ClassVar[int]
    amount: int
    currency: str
    def __init__(self, amount: _Optional[int] = ..., currency: _Optional[str] = ...) -> None: ...
