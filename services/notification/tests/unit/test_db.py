import pytest
from alembic.config import Config

from notification import db


def test_migrate_passes_a_url_encoded_password_to_alembic_unchanged(monkeypatch: pytest.MonkeyPatch) -> None:
    url = "postgresql+psycopg://postgres:p%21ss@localhost:5432/notificationdb"
    seen: list[str | None] = []

    def fake_upgrade(config: Config, revision: str) -> None:
        seen.append(config.get_main_option("sqlalchemy.url"))

    monkeypatch.setattr(db.command, "upgrade", fake_upgrade)

    db.migrate(url)

    assert seen == [url]
