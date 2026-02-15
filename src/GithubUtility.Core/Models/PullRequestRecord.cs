namespace GithubUtility.Core.Models;

public sealed record PullRequestRecord(
    string Repository,
    int Number,
    string Title,
    string Author,
    PullRequestState State,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? MergedAt,
    IReadOnlyList<PullRequestReviewRecord> Reviews,
    IReadOnlyList<PullRequestEventRecord> Events);
