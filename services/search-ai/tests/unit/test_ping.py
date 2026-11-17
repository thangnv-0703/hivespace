from __future__ import annotations

import pytest
from fastapi.testclient import TestClient

from search_ai.main import create_app


@pytest.mark.parametrize("path", ["/health", "/ready", "/v1/search-ai/ping"])
def test_outer_shell_endpoints_answer(path: str) -> None:
    with TestClient(create_app()) as client:
        assert client.get(path).status_code == 200
