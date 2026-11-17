"""Alembic environment: runs migrations with the async psycopg driver."""

from __future__ import annotations

import asyncio

from alembic import context
from sqlalchemy.engine import Connection
from sqlalchemy.ext.asyncio import create_async_engine

config = context.config


def run(connection: Connection) -> None:
    context.configure(connection=connection)
    with context.begin_transaction():
        context.run_migrations()


async def main() -> None:
    engine = create_async_engine(config.get_main_option("sqlalchemy.url") or "")
    async with engine.connect() as conn:
        await conn.run_sync(run)
        await conn.commit()
    await engine.dispose()


# psycopg's async mode cannot use Windows' default ProactorEventLoop; the selector loop works everywhere.
asyncio.run(main(), loop_factory=asyncio.SelectorEventLoop)
