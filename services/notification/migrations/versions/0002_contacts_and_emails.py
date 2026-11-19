"""Contact projection and email records (FEAT-001, technical design section 4).

Revision ID: 0002
Revises: 0001
"""

import sqlalchemy as sa
from alembic import op

revision = "0002"
down_revision = "0001"
branch_labels = None
depends_on = None


def upgrade() -> None:
    op.create_table(
        "contacts",
        sa.Column("user_id", sa.Uuid(), primary_key=True),
        sa.Column("email", sa.Text(), nullable=False),
        sa.Column("display_name", sa.Text(), nullable=False),
        sa.Column("language", sa.String(2), nullable=False),
        sa.Column("updated_at", sa.DateTime(timezone=True), nullable=False),
    )
    # No column holds the verification token: it lives only in the event and the sent message (R-6).
    op.create_table(
        "email_records",
        sa.Column("id", sa.Uuid(), primary_key=True),
        sa.Column("event_id", sa.Uuid(), nullable=False),
        sa.Column("template", sa.String(64), nullable=False),
        sa.Column("recipient_user_id", sa.Uuid(), nullable=False),
        sa.Column("recipient_email", sa.Text(), nullable=False),
        sa.Column("language", sa.String(2)),
        sa.Column("status", sa.String(16), nullable=False),
        sa.Column("attempts", sa.Integer(), nullable=False, server_default="0"),
        sa.Column("created_at", sa.DateTime(timezone=True), nullable=False),
        sa.Column("sent_at", sa.DateTime(timezone=True)),
        sa.Column("last_error", sa.Text()),
        sa.CheckConstraint("status in ('pending','sent','failed')", name="ck_email_records_status"),
        sa.UniqueConstraint("event_id", "template", name="uq_email_records_event_template"),
    )


def downgrade() -> None:
    op.drop_table("email_records")
    op.drop_table("contacts")
