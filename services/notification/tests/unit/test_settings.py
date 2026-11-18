from notification.settings import Settings


def test_plain_postgres_uri_gets_the_async_driver() -> None:
    settings = Settings(DATABASE_URL="postgresql://u:p@db.internal:5432/notificationdb")

    assert settings.async_database_url == "postgresql+psycopg://u:p@db.internal:5432/notificationdb"


def test_localhost_is_pinned_to_ipv4_and_keeps_an_encoded_password() -> None:
    settings = Settings(DATABASE_URL="postgresql://u:p%21w@localhost:5432/notificationdb")

    assert settings.async_database_url == "postgresql+psycopg://u:p%21w@127.0.0.1:5432/notificationdb"
