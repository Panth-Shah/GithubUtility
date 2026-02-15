namespace GithubUtility.Core.Models;

public sealed record PullRequestEventRecord(
    string EventType,
    string Actor,
    DateTimeOffset OccurredAt);
