namespace GithubUtility.Core.Models;

public sealed record IngestionRunResult(
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    int RepositoryCount,
    int PullRequestCount,
    int ErrorCount,
    IReadOnlyList<string> Errors);
