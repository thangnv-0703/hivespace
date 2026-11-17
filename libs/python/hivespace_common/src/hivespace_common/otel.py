"""Same hosting contract as .NET ServiceDefaults (dev-standards §11): OTLP traces and JSON logs."""

from __future__ import annotations

import json
import logging
import os
import sys
from datetime import UTC, datetime

from opentelemetry import trace
from opentelemetry.exporter.otlp.proto.grpc.trace_exporter import OTLPSpanExporter
from opentelemetry.sdk.resources import SERVICE_NAME, Resource
from opentelemetry.sdk.trace import TracerProvider
from opentelemetry.sdk.trace.export import BatchSpanProcessor


class JsonFormatter(logging.Formatter):
    """One JSON object per line with the trace id as the correlation id (NFR-OBS-002)."""

    def __init__(self, service: str) -> None:
        super().__init__()
        self._service = service

    def format(self, record: logging.LogRecord) -> str:
        span = trace.get_current_span().get_span_context()
        entry: dict[str, object] = {
            "timestamp": datetime.fromtimestamp(record.created, UTC).isoformat().replace("+00:00", "Z"),
            "level": record.levelname,
            "service": self._service,
            "logger": record.name,
            "message": record.getMessage(),
        }
        if span.is_valid:
            entry["traceId"] = format(span.trace_id, "032x")
            entry["correlationId"] = entry["traceId"]
        if record.exc_info:
            entry["exception"] = self.formatException(record.exc_info)
        return json.dumps(entry, ensure_ascii=False)


def setup(service: str) -> None:
    handler = logging.StreamHandler(sys.stdout)
    handler.setFormatter(JsonFormatter(service))
    logging.basicConfig(level=logging.INFO, handlers=[handler], force=True)

    if not os.environ.get("OTEL_EXPORTER_OTLP_ENDPOINT"):
        return
    name = os.environ.get("OTEL_SERVICE_NAME", service)
    provider = TracerProvider(resource=Resource.create({SERVICE_NAME: name}))
    provider.add_span_processor(BatchSpanProcessor(OTLPSpanExporter()))
    trace.set_tracer_provider(provider)
