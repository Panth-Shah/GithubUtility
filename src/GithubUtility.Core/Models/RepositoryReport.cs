namespace GithubUtility.Core.Models;

public sealed record RepositoryReport(
    string Repository,
    int OpenPrs,
    int MergedPrs,
    int ClosedPrs,
    int ActiveContributors);
