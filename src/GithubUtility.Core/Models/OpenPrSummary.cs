namespace GithubUtility.Core.Models;

public sealed record OpenPrSummary(
    string Repository,
    int Number,
    string Title,
    string Author,
    int AgeDays,
    DateTimeOffset UpdatedAt);
