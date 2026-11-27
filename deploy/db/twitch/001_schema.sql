-- 001: Tabellen des Twitch-Workers (vorher als CREATE TABLE im Code, siehe E-011 und E-036).
-- IF NOT EXISTS: Datenbanken, in denen der alte Worker die Tabellen schon angelegt hat, laufen ohne Fehler durch.

CREATE TABLE IF NOT EXISTS snapshots (
    id           bigserial PRIMARY KEY,
    taken_at     timestamptz NOT NULL,
    stream_count int         NOT NULL,
    viewer_sum   bigint      NOT NULL,
    pages        int         NOT NULL,
    duration_ms  int         NOT NULL,
    complete     boolean     NOT NULL,
    filter       text        NOT NULL
);

CREATE TABLE IF NOT EXISTS stream_samples (
    snapshot_id  bigint      NOT NULL REFERENCES snapshots(id) ON DELETE CASCADE,
    taken_at     timestamptz NOT NULL,
    stream_id    text        NOT NULL,
    user_id      text        NOT NULL,
    user_login   text        NOT NULL,
    game_id      text        NOT NULL,
    game_name    text        NOT NULL,
    language     text        NOT NULL,
    viewer_count int         NOT NULL,
    started_at   timestamptz NOT NULL,
    PRIMARY KEY (snapshot_id, stream_id)
);

CREATE INDEX IF NOT EXISTS ix_samples_stream ON stream_samples (stream_id);
CREATE INDEX IF NOT EXISTS ix_samples_user   ON stream_samples (user_id, taken_at);
CREATE INDEX IF NOT EXISTS ix_snapshots_time ON snapshots (taken_at);
