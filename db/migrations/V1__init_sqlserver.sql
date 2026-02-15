IF OBJECT_ID('repository_cursors', 'U') IS NULL
BEGIN
    CREATE TABLE repository_cursors (
        repository NVARCHAR(255) NOT NULL PRIMARY KEY,
        last_successful_sync_utc DATETIMEOFFSET(7) NOT NULL
    );
END;

IF OBJECT_ID('pull_request_snapshots', 'U') IS NULL
BEGIN
    CREATE TABLE pull_request_snapshots (
        repository NVARCHAR(255) NOT NULL,
        pr_number INT NOT NULL,
        title NVARCHAR(512) NOT NULL,
        author NVARCHAR(255) NOT NULL,
        pull_request_state NVARCHAR(32) NOT NULL,
        created_at DATETIMEOFFSET(7) NOT NULL,
        updated_at DATETIMEOFFSET(7) NOT NULL,
        merged_at DATETIMEOFFSET(7) NULL,
        reviews_json NVARCHAR(MAX) NOT NULL,
        events_json NVARCHAR(MAX) NOT NULL,
        CONSTRAINT PK_pull_request_snapshots PRIMARY KEY (repository, pr_number)
    );
END;
