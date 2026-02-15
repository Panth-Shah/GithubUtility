namespace GithubUtility.Core.Models;

public sealed record RepositoryCursor(
    string Repository,
    DateTimeOffset LastSuccessfulSyncUtc);
