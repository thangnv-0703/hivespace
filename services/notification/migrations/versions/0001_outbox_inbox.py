"""Outer-shell tables every service owns: outbox and inbox (repo-structure §3).

Revision ID: 0001
Revises:
"""

import sqlalchemy as sa
from alembic import op
from sqlalchemy.dialects import postgresql

revision = "0001"
down_revision = None
branch_labels = None
depends_on = None


def upgrade() -> None:
    op.create_table(
        "outbox",
        sa.Column("id", sa.Uuid(), primary_key=True),
        sa.Column("event_type", sa.String(200), nullable=False),
        sa.Column("envelope", postgresql.JSONB(), nullable=False),
        sa.Column("occurred_at", sa.DateTime(timezone=True), nullable=False),
        sa.Column("sent_at", sa.DateTime(timezone=True)),
    )
    op.create_index("ix_outbox_sent_at", "outbox", ["sent_at"])
    op.create_table(
        "inbox",
        sa.Column("event_id", sa.Uuid(), primary_key=True),
        sa.Column("consumer", sa.String(200), primary_key=True),
        sa.Column("processed_at", sa.DateTime(timezone=True), nullable=False),
    )


def downgrade() -> None:
    op.drop_table("inbox")
    op.drop_index("ix_outbox_sent_at", table_name="outbox")
    op.drop_table("outbox")
