using GithubUtility.App.Options;
using GithubUtility.Core.Abstractions;
using GithubUtility.Core.Models;
using Microsoft.Extensions.Options;

namespace GithubUtility.App.Infrastructure;

public sealed class SampleGitHubDataSource(IOptions<GitHubConnectorOptions> options) : IGitHubDataSource
{
    private readonly GitHubConnectorOptions _options = options.Value;

    public Task<IReadOnlyList<string>> ListRepositoriesAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<string> repositories = _options.Repositories.Count == 0
            ? ["org/platform-service", "org/payments-api"]
            : _options.Repositories;

        return Task.FromResult(repositories);
    }

    public Task<IReadOnlyList<PullRequestRecord>> ListPullRequestsUpdatedSinceAsync(
        string repository,
        DateTimeOffset since,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var pullRequests = BuildSampleData(repository)
            .Where(pr => pr.UpdatedAt >= since)
            .ToArray();

        return Task.FromResult<IReadOnlyList<PullRequestRecord>>(pullRequests);
    }

    private static IReadOnlyList<PullRequestRecord> BuildSampleData(string repository)
    {
        var baseTime = DateTimeOffset.UtcNow;

        return
        [
            new PullRequestRecord(
                repository,
                101,
                "Harden release branch checks",
                "alice",
                PullRequestState.Open,
                baseTime.AddDays(-12),
                baseTime.AddHours(-5),
                null,
                [
                    new PullRequestReviewRecord("bob", "COMMENTED", baseTime.AddDays(-10)),
                    new PullRequestReviewRecord("claire", "APPROVED", baseTime.AddDays(-9))
                ],
                [new PullRequestEventRecord("labeled", "alice", baseTime.AddDays(-12))]),
            new PullRequestRecord(
                repository,
                102,
                "Fix changelog generation for hotfixes",
                "bob",
                PullRequestState.Merged,
                baseTime.AddDays(-8),
                baseTime.AddDays(-2),
                baseTime.AddDays(-2),
                [
                    new PullRequestReviewRecord("alice", "APPROVED", baseTime.AddDays(-3)),
                    new PullRequestReviewRecord("claire", "APPROVED", baseTime.AddDays(-3))
                ],
                [new PullRequestEventRecord("merged", "release-bot", baseTime.AddDays(-2))]),
            new PullRequestRecord(
                repository,
                103,
                "Retire deprecated branch policy",
                "claire",
                PullRequestState.Closed,
                baseTime.AddDays(-20),
                baseTime.AddDays(-6),
                null,
                [new PullRequestReviewRecord("alice", "CHANGES_REQUESTED", baseTime.AddDays(-7))],
                [new PullRequestEventRecord("closed", "claire", baseTime.AddDays(-6))])
        ];
    }
}
