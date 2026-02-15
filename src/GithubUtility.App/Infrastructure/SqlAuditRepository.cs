using System.Data.Common;
using System.Text.Json;
using GithubUtility.App.Options;
using GithubUtility.Core.Abstractions;
using GithubUtility.Core.Models;
using Microsoft.Extensions.Options;

namespace GithubUtility.App.Infrastructure;

public sealed class SqlAuditRepository(IOptions<AuditStoreOptions> options, ILogger<SqlAuditRepository> logger) : IAuditRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly AuditStoreOptions _options = options.Value;
    private readonly SemaphoreSlim _schemaGate = new(1, 1);
    private bool _schemaInitialized;

    public async Task<RepositoryCursor?> GetCursorAsync(string repository, CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT repository, last_successful_sync_utc FROM repository_cursors WHERE repository = @repository";
        AddParameter(command, "@repository", repository);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new RepositoryCursor(
            reader.GetString(0),
            reader.GetFieldValue<DateTimeOffset>(1));
    }

    public async Task SaveCursorAsync(RepositoryCursor cursor, CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = GetCursorUpsertSql();
        AddParameter(command, "@repository", cursor.Repository);
        AddParameter(command, "@last_successful_sync_utc", cursor.LastSuccessfulSyncUtc);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpsertPullRequestsAsync(IReadOnlyList<PullRequestRecord> pullRequests, CancellationToken cancellationToken)
    {
        if (pullRequests.Count == 0)
        {
            return;
        }

        await EnsureSchemaAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        foreach (var pullRequest in pullRequests)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = GetPullRequestUpsertSql();

            AddParameter(command, "@repository", pullRequest.Repository);
            AddParameter(command, "@pr_number", pullRequest.Number);
            AddParameter(command, "@title", pullRequest.Title);
            AddParameter(command, "@author", pullRequest.Author);
            AddParameter(command, "@state", pullRequest.State.ToString());
            AddParameter(command, "@created_at", pullRequest.CreatedAt);
            AddParameter(command, "@updated_at", pullRequest.UpdatedAt);
            AddParameter(command, "@merged_at", pullRequest.MergedAt);
            AddParameter(command, "@reviews_json", JsonSerializer.Serialize(pullRequest.Reviews, SerializerOptions));
            AddParameter(command, "@events_json", JsonSerializer.Serialize(pullRequest.Events, SerializerOptions));

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PullRequestRecord>> ListPullRequestsAsync(CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT repository, pr_number, title, author, pull_request_state, created_at, updated_at, merged_at, reviews_json, events_json
FROM pull_request_snapshots";

        var results = new List<PullRequestRecord>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var reviews = DeserializeReviews(reader.IsDBNull(8) ? null : reader.GetString(8));
            var events = DeserializeEvents(reader.IsDBNull(9) ? null : reader.GetString(9));

            results.Add(new PullRequestRecord(
                reader.GetString(0),
                reader.GetInt32(1),
                reader.GetString(2),
                reader.GetString(3),
                Enum.TryParse<PullRequestState>(reader.GetString(4), true, out var state) ? state : PullRequestState.Closed,
                reader.GetFieldValue<DateTimeOffset>(5),
                reader.GetFieldValue<DateTimeOffset>(6),
                reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7),
                reviews,
                events));
        }

        return results;
    }

    public async Task<IReadOnlyList<PullRequestRecord>> ListPullRequestsByStateAsync(
        PullRequestState state, 
        string? repository, 
        CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        
        if (string.IsNullOrWhiteSpace(repository))
        {
            command.CommandText = @"
SELECT repository, pr_number, title, author, pull_request_state, created_at, updated_at, merged_at, reviews_json, events_json
FROM pull_request_snapshots
WHERE pull_request_state = @state";
            AddParameter(command, "@state", state.ToString());
        }
        else
        {
            command.CommandText = @"
SELECT repository, pr_number, title, author, pull_request_state, created_at, updated_at, merged_at, reviews_json, events_json
FROM pull_request_snapshots
WHERE pull_request_state = @state AND repository = @repository";
            AddParameter(command, "@state", state.ToString());
            AddParameter(command, "@repository", repository);
        }

        return await ExecuteQueryAsync(command, cancellationToken);
    }

    public async Task<IReadOnlyList<PullRequestRecord>> ListPullRequestsByDateRangeAsync(
        DateTimeOffset from, 
        DateTimeOffset to, 
        CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT repository, pr_number, title, author, pull_request_state, created_at, updated_at, merged_at, reviews_json, events_json
FROM pull_request_snapshots
WHERE updated_at >= @from AND updated_at <= @to";
        
        AddParameter(command, "@from", from);
        AddParameter(command, "@to", to);

        return await ExecuteQueryAsync(command, cancellationToken);
    }

    private async Task<IReadOnlyList<PullRequestRecord>> ExecuteQueryAsync(DbCommand command, CancellationToken cancellationToken)
    {
        var results = new List<PullRequestRecord>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var reviews = DeserializeReviews(reader.IsDBNull(8) ? null : reader.GetString(8));
            var events = DeserializeEvents(reader.IsDBNull(9) ? null : reader.GetString(9));

            results.Add(new PullRequestRecord(
                reader.GetString(0),
                reader.GetInt32(1),
                reader.GetString(2),
                reader.GetString(3),
                Enum.TryParse<PullRequestState>(reader.GetString(4), true, out var state) ? state : PullRequestState.Closed,
                reader.GetFieldValue<DateTimeOffset>(5),
                reader.GetFieldValue<DateTimeOffset>(6),
                reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7),
                reviews,
                events));
        }

        return results;
    }

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        if (_schemaInitialized || !_options.InitializeSchemaOnStartup)
        {
            return;
        }

        await _schemaGate.WaitAsync(cancellationToken);
        try
        {
            if (_schemaInitialized)
            {
                return;
            }

            await using var connection = await OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = GetSchemaSql();
            await command.ExecuteNonQueryAsync(cancellationToken);

            _schemaInitialized = true;
            logger.LogInformation("Audit SQL schema ensured using provider {Provider}.", _options.Provider);
        }
        finally
        {
            _schemaGate.Release();
        }
    }

    private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var providerInvariant = GetProviderInvariant();
        var factory = DbProviderFactories.GetFactory(providerInvariant);
        var connection = factory.CreateConnection() ?? throw new InvalidOperationException($"Unable to create DB connection for provider '{providerInvariant}'.");
        connection.ConnectionString = _options.ConnectionString;
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private string GetSchemaSql() => _options.Provider.ToLowerInvariant() switch
    {
        "sqlserver" => @"
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
    
    CREATE INDEX IX_pull_request_snapshots_state ON pull_request_snapshots(pull_request_state);
    CREATE INDEX IX_pull_request_snapshots_updated_at ON pull_request_snapshots(updated_at);
    CREATE INDEX IX_pull_request_snapshots_repository_state ON pull_request_snapshots(repository, pull_request_state);
END;
",
        "postgres" => @"
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

CREATE INDEX IF NOT EXISTS IX_pull_request_snapshots_state ON pull_request_snapshots(pull_request_state);
CREATE INDEX IF NOT EXISTS IX_pull_request_snapshots_updated_at ON pull_request_snapshots(updated_at);
CREATE INDEX IF NOT EXISTS IX_pull_request_snapshots_repository_state ON pull_request_snapshots(repository, pull_request_state);
",
        _ => @"
CREATE TABLE IF NOT EXISTS repository_cursors (
    repository TEXT NOT NULL PRIMARY KEY,
    last_successful_sync_utc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS pull_request_snapshots (
    repository TEXT NOT NULL,
    pr_number INTEGER NOT NULL,
    title TEXT NOT NULL,
    author TEXT NOT NULL,
    pull_request_state TEXT NOT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL,
    merged_at TEXT NULL,
    reviews_json TEXT NOT NULL,
    events_json TEXT NOT NULL,
    PRIMARY KEY (repository, pr_number)
);

CREATE INDEX IF NOT EXISTS IX_pull_request_snapshots_state ON pull_request_snapshots(pull_request_state);
CREATE INDEX IF NOT EXISTS IX_pull_request_snapshots_updated_at ON pull_request_snapshots(updated_at);
CREATE INDEX IF NOT EXISTS IX_pull_request_snapshots_repository_state ON pull_request_snapshots(repository, pull_request_state);
"
    };

    private string GetCursorUpsertSql() => _options.Provider.ToLowerInvariant() switch
    {
        "sqlserver" => @"
MERGE repository_cursors AS target
USING (SELECT @repository AS repository, @last_successful_sync_utc AS last_successful_sync_utc) AS source
ON target.repository = source.repository
WHEN MATCHED THEN
    UPDATE SET last_successful_sync_utc = source.last_successful_sync_utc
WHEN NOT MATCHED THEN
    INSERT (repository, last_successful_sync_utc)
    VALUES (source.repository, source.last_successful_sync_utc);
",
        "postgres" => @"
INSERT INTO repository_cursors (repository, last_successful_sync_utc)
VALUES (@repository, @last_successful_sync_utc)
ON CONFLICT (repository)
DO UPDATE SET last_successful_sync_utc = EXCLUDED.last_successful_sync_utc;
",
        _ => @"
INSERT INTO repository_cursors (repository, last_successful_sync_utc)
VALUES (@repository, @last_successful_sync_utc)
ON CONFLICT(repository)
DO UPDATE SET last_successful_sync_utc = excluded.last_successful_sync_utc;
"
    };

    private string GetPullRequestUpsertSql() => _options.Provider.ToLowerInvariant() switch
    {
        "sqlserver" => @"
MERGE pull_request_snapshots AS target
USING (
    SELECT
        @repository AS repository,
        @pr_number AS pr_number,
        @title AS title,
        @author AS author,
        @state AS pull_request_state,
        @created_at AS created_at,
        @updated_at AS updated_at,
        @merged_at AS merged_at,
        @reviews_json AS reviews_json,
        @events_json AS events_json
) AS source
ON target.repository = source.repository AND target.pr_number = source.pr_number
WHEN MATCHED THEN
    UPDATE SET
        title = source.title,
        author = source.author,
        pull_request_state = source.pull_request_state,
        created_at = source.created_at,
        updated_at = source.updated_at,
        merged_at = source.merged_at,
        reviews_json = source.reviews_json,
        events_json = source.events_json
WHEN NOT MATCHED THEN
    INSERT (repository, pr_number, title, author, pull_request_state, created_at, updated_at, merged_at, reviews_json, events_json)
    VALUES (source.repository, source.pr_number, source.title, source.author, source.pull_request_state, source.created_at, source.updated_at, source.merged_at, source.reviews_json, source.events_json);
",
        "postgres" => @"
INSERT INTO pull_request_snapshots (repository, pr_number, title, author, pull_request_state, created_at, updated_at, merged_at, reviews_json, events_json)
VALUES (@repository, @pr_number, @title, @author, @state, @created_at, @updated_at, @merged_at, @reviews_json, @events_json)
ON CONFLICT (repository, pr_number)
DO UPDATE SET
    title = EXCLUDED.title,
    author = EXCLUDED.author,
    pull_request_state = EXCLUDED.pull_request_state,
    created_at = EXCLUDED.created_at,
    updated_at = EXCLUDED.updated_at,
    merged_at = EXCLUDED.merged_at,
    reviews_json = EXCLUDED.reviews_json,
    events_json = EXCLUDED.events_json;
",
        _ => @"
INSERT INTO pull_request_snapshots (repository, pr_number, title, author, pull_request_state, created_at, updated_at, merged_at, reviews_json, events_json)
VALUES (@repository, @pr_number, @title, @author, @state, @created_at, @updated_at, @merged_at, @reviews_json, @events_json)
ON CONFLICT(repository, pr_number)
DO UPDATE SET
    title = excluded.title,
    author = excluded.author,
    pull_request_state = excluded.pull_request_state,
    created_at = excluded.created_at,
    updated_at = excluded.updated_at,
    merged_at = excluded.merged_at,
    reviews_json = excluded.reviews_json,
    events_json = excluded.events_json;
"
    };

    private string GetProviderInvariant() => _options.Provider.ToLowerInvariant() switch
    {
        "sqlserver" => "Microsoft.Data.SqlClient",
        "postgres" => "Npgsql",
        _ => "Microsoft.Data.Sqlite"
    };

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static IReadOnlyList<PullRequestReviewRecord> DeserializeReviews(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<PullRequestReviewRecord>();
        }

        return JsonSerializer.Deserialize<IReadOnlyList<PullRequestReviewRecord>>(json, SerializerOptions)
            ?? Array.Empty<PullRequestReviewRecord>();
    }

    private static IReadOnlyList<PullRequestEventRecord> DeserializeEvents(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<PullRequestEventRecord>();
        }

        return JsonSerializer.Deserialize<IReadOnlyList<PullRequestEventRecord>>(json, SerializerOptions)
            ?? Array.Empty<PullRequestEventRecord>();
    }
}
