from notification.settings import Settings


def test_plain_postgres_uri_gets_the_async_driver() -> None:
    settings = Settings(DATABASE_URL="postgresql://u:p@localhost:5432/notificationdb")

    assert settings.async_database_url == "postgresql+psycopg://u:p@localhost:5432/notificationdb"
