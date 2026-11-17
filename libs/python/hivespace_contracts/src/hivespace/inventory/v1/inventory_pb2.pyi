from google.protobuf.internal import containers as _containers
from google.protobuf import descriptor as _descriptor
from google.protobuf import message as _message
from collections.abc import Iterable as _Iterable, Mapping as _Mapping
from typing import ClassVar as _ClassVar, Optional as _Optional, Union as _Union

DESCRIPTOR: _descriptor.FileDescriptor

class GetAvailabilityRequest(_message.Message):
    __slots__ = ("variant_ids",)
    VARIANT_IDS_FIELD_NUMBER: _ClassVar[int]
    variant_ids: _containers.RepeatedScalarFieldContainer[str]
    def __init__(self, variant_ids: _Optional[_Iterable[str]] = ...) -> None: ...

class GetAvailabilityResponse(_message.Message):
    __slots__ = ("items", "probe")
    ITEMS_FIELD_NUMBER: _ClassVar[int]
    PROBE_FIELD_NUMBER: _ClassVar[int]
    items: _containers.RepeatedCompositeFieldContainer[VariantAvailability]
    probe: str
    def __init__(self, items: _Optional[_Iterable[_Union[VariantAvailability, _Mapping]]] = ..., probe: _Optional[str] = ...) -> None: ...

class VariantAvailability(_message.Message):
    __slots__ = ("variant_id", "available")
    VARIANT_ID_FIELD_NUMBER: _ClassVar[int]
    AVAILABLE_FIELD_NUMBER: _ClassVar[int]
    variant_id: str
    available: int
    def __init__(self, variant_id: _Optional[str] = ..., available: _Optional[int] = ...) -> None: ...
