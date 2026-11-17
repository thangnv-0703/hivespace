-- Outer shell tables every service owns (repo-structure §3). Stock and reservations arrive with FEAT-012 / FEAT-017.
CREATE TABLE outbox (
    id           uuid PRIMARY KEY,
    event_type   varchar(200) NOT NULL,
    envelope     jsonb        NOT NULL,
    occurred_at  timestamptz  NOT NULL,
    sent_at      timestamptz
);
CREATE INDEX ix_outbox_sent_at ON outbox (sent_at);

CREATE TABLE inbox (
    event_id     uuid         NOT NULL,
    consumer     varchar(200) NOT NULL,
    processed_at timestamptz  NOT NULL,
    PRIMARY KEY (event_id, consumer)
);
