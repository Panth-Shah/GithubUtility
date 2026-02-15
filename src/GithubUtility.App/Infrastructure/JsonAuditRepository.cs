using System.Text.Json;
using GithubUtility.Core.Abstractions;
using GithubUtility.Core.Models;

namespace GithubUtility.App.Infrastructure;

public sealed class JsonAuditRepository(IHostEnvironment hostEnvironment) : IAuditRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _statePath = Path.Combine(hostEnvironment.ContentRootPath, "data", "audit-state.json");

    public async Task<RepositoryCursor?> GetCursorAsync(string repository, CancellationToken cancellationToken)
    {
        var state = await ReadStateAsync(cancellationToken);
        if (!state.Cursors.TryGetValue(repository, out var timestamp))
        {
            return null;
        }

        return new RepositoryCursor(repository, timestamp);
    }

    public async Task SaveCursorAsync(RepositoryCursor cursor, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var state = await ReadStateCoreAsync(cancellationToken);
            state.Cursors[cursor.Repository] = cursor.LastSuccessfulSyncUtc;
            await WriteStateCoreAsync(state, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task UpsertPullRequestsAsync(IReadOnlyList<PullRequestRecord> pullRequests, CancellationToken cancellationToken)
    {
        if (pullRequests.Count == 0)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var state = await ReadStateCoreAsync(cancellationToken);
            var index = state.PullRequests.ToDictionary(pr => BuildKey(pr.Repository, pr.Number), StringComparer.OrdinalIgnoreCase);

            foreach (var pullRequest in pullRequests)
            {
                index[BuildKey(pullRequest.Repository, pullRequest.Number)] = pullRequest;
            }

            state.PullRequests = index.Values
                .OrderBy(pr => pr.Repository, StringComparer.OrdinalIgnoreCase)
                .ThenBy(pr => pr.Number)
                .ToList();

            await WriteStateCoreAsync(state, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<PullRequestRecord>> ListPullRequestsAsync(CancellationToken cancellationToken)
    {
        var state = await ReadStateAsync(cancellationToken);
        return state.PullRequests;
    }

    public async Task<IReadOnlyList<PullRequestRecord>> ListPullRequestsByStateAsync(
        PullRequestState state, 
        string? repository, 
        CancellationToken cancellationToken)
    {
        var allPrs = await ListPullRequestsAsync(cancellationToken);
        return allPrs
            .Where(pr => pr.State == state)
            .Where(pr => string.IsNullOrWhiteSpace(repository) || pr.Repository.Equals(repository, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    public async Task<IReadOnlyList<PullRequestRecord>> ListPullRequestsByDateRangeAsync(
        DateTimeOffset from, 
        DateTimeOffset to, 
        CancellationToken cancellationToken)
    {
        var allPrs = await ListPullRequestsAsync(cancellationToken);
        return allPrs
            .Where(pr => pr.UpdatedAt >= from && pr.UpdatedAt <= to)
            .ToArray();
    }

    private async Task<AuditState> ReadStateAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await ReadStateCoreAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<AuditState> ReadStateCoreAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_statePath)!);

        if (!File.Exists(_statePath))
        {
            return new AuditState();
        }

        await using var stream = File.OpenRead(_statePath);
        var state = await JsonSerializer.DeserializeAsync<AuditState>(stream, SerializerOptions, cancellationToken);
        return state ?? new AuditState();
    }

    private async Task WriteStateCoreAsync(AuditState state, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_statePath)!);

        await using var stream = File.Create(_statePath);
        await JsonSerializer.SerializeAsync(stream, state, SerializerOptions, cancellationToken);
    }

    private static string BuildKey(string repository, int prNumber) => $"{repository}#{prNumber}";

    private sealed class AuditState
    {
        public Dictionary<string, DateTimeOffset> Cursors { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public List<PullRequestRecord> PullRequests { get; set; } = new();
    }
}
