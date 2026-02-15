CREATE TABLE IF NOT EXISTS repository_cursors (
    repository TEXT PRIMARY KEY,
    last_successful_sync_utc TIMESTAMPTZ NOT NULL
);

CREATE TABLE IF NOT EXISTS pull_request_snapshots (
    repository TEXT NOT NULL,
    pr_number INTEGER NOT NULL,
    title TEXT NOT NULL,
    author TEXT NOT NULL,
    pull_request_state TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL,
    merged_at TIMESTAMPTZ NULL,
    reviews_json TEXT NOT NULL,
    events_json TEXT NOT NULL,
    PRIMARY KEY (repository, pr_number)
);
