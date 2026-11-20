"""Test environment fixes for Windows with Docker Desktop (docs/scaffold-notes.md)."""

from __future__ import annotations

import asyncio
import os
import sys

import pytest

# Testcontainers' Ryuk reaper container does not come up on Docker Desktop for Windows (no port mapping). The
# fixtures use context managers, so containers are removed without it.
os.environ.setdefault("TESTCONTAINERS_RYUK_DISABLED", "true")


@pytest.fixture(scope="session")
def event_loop_policy() -> asyncio.AbstractEventLoopPolicy:
    # psycopg's async mode cannot use Windows' default ProactorEventLoop.
    if sys.platform == "win32":
        return asyncio.WindowsSelectorEventLoopPolicy()
    return asyncio.DefaultEventLoopPolicy()
