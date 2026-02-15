namespace GithubUtility.Core.Models;

public sealed record PullRequestReviewRecord(
    string Reviewer,
    string State,
    DateTimeOffset SubmittedAt);
