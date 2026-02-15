namespace GithubUtility.Core.Models;

public sealed record UserStatSummary(
    string User,
    int OpenedPrs,
    int MergedPrs,
    int ReviewsSubmitted,
    int ApprovalsSubmitted);
