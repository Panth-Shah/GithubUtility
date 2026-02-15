namespace GithubUtility.Core.Models;

public sealed record ReleaseAuditSummary(
    DateTimeOffset From,
    DateTimeOffset To,
    int OpenPrs,
    int ClosedPrs,
    int MergedPrs,
    int MergedWithoutApproval);
