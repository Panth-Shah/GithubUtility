using GithubUtility.Core.Abstractions;
using GithubUtility.Core.Models;

namespace GithubUtility.Core.Services;

public sealed class PrAuditOrchestrator(IGitHubDataSource gitHubDataSource, IAuditRepository auditRepository) : IPrAuditOrchestrator
{
    public async Task<IngestionRunResult> RunIngestionAsync(CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var errors = new List<string>();
        var totalPrs = 0;
        var repositories = await gitHubDataSource.ListRepositoriesAsync(cancellationToken);

        foreach (var repository in repositories)
        {
            try
            {
                var cursor = await auditRepository.GetCursorAsync(repository, cancellationToken);
                var since = cursor?.LastSuccessfulSyncUtc ?? DateTimeOffset.UtcNow.AddDays(-30);

                var updatedPrs = await gitHubDataSource.ListPullRequestsUpdatedSinceAsync(repository, since, cancellationToken);
                totalPrs += updatedPrs.Count;

                if (updatedPrs.Count > 0)
                {
                    await auditRepository.UpsertPullRequestsAsync(updatedPrs, cancellationToken);
                }

                await auditRepository.SaveCursorAsync(
                    new RepositoryCursor(repository, DateTimeOffset.UtcNow),
                    cancellationToken);
            }
            catch (Exception ex)
            {
                errors.Add($"{repository}: {ex.Message}");
            }
        }

        var completedAt = DateTimeOffset.UtcNow;
        return new IngestionRunResult(
            startedAt,
            completedAt,
            repositories.Count,
            totalPrs,
            errors.Count,
            errors);
    }

    public async Task<IReadOnlyList<OpenPrSummary>> GetOpenPrReportAsync(string? repository, int olderThanDays, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var prs = await auditRepository.ListPullRequestsByStateAsync(PullRequestState.Open, repository, cancellationToken);

        return prs
            .Select(pr => new OpenPrSummary(
                pr.Repository,
                pr.Number,
                pr.Title,
                pr.Author,
                (int)Math.Floor((now - pr.CreatedAt).TotalDays),
                pr.UpdatedAt))
            .Where(summary => summary.AgeDays >= Math.Max(olderThanDays, 0))
            .OrderByDescending(summary => summary.AgeDays)
            .ThenBy(summary => summary.Repository)
            .ThenBy(summary => summary.Number)
            .ToArray();
    }

    public async Task<IReadOnlyList<UserStatSummary>> GetUserStatsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        var prs = await auditRepository.ListPullRequestsByDateRangeAsync(from, to, cancellationToken);

        var prAuthors = prs
            .GroupBy(pr => pr.Author, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => new
                {
                    Opened = group.Count(),
                    Merged = group.Count(pr => pr.State == PullRequestState.Merged)
                },
                StringComparer.OrdinalIgnoreCase);

        var reviewStats = prs
            .SelectMany(pr => pr.Reviews)
            .Where(review => review.SubmittedAt >= from && review.SubmittedAt <= to)
            .GroupBy(review => review.Reviewer, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => new
                {
                    Reviews = group.Count(),
                    Approvals = group.Count(r => r.State.Equals("APPROVED", StringComparison.OrdinalIgnoreCase))
                },
                StringComparer.OrdinalIgnoreCase);

        var users = prAuthors.Keys
            .Union(reviewStats.Keys, StringComparer.OrdinalIgnoreCase)
            .OrderBy(user => user, StringComparer.OrdinalIgnoreCase);

        return users
            .Select(user =>
            {
                prAuthors.TryGetValue(user, out var authorStats);
                reviewStats.TryGetValue(user, out var reviewerStats);

                return new UserStatSummary(
                    user,
                    authorStats?.Opened ?? 0,
                    authorStats?.Merged ?? 0,
                    reviewerStats?.Reviews ?? 0,
                    reviewerStats?.Approvals ?? 0);
            })
            .ToArray();
    }

    public async Task<ReleaseAuditSummary> GetReleaseAuditSummaryAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        var prs = await auditRepository.ListPullRequestsByDateRangeAsync(from, to, cancellationToken);

        var mergedWithoutApproval = prs.Count(pr =>
            pr.State == PullRequestState.Merged &&
            pr.Reviews.All(review => !review.State.Equals("APPROVED", StringComparison.OrdinalIgnoreCase)));

        return new ReleaseAuditSummary(
            from,
            to,
            prs.Count(pr => pr.State == PullRequestState.Open),
            prs.Count(pr => pr.State == PullRequestState.Closed),
            prs.Count(pr => pr.State == PullRequestState.Merged),
            mergedWithoutApproval);
    }

    public async Task<IReadOnlyList<RepositoryReport>> GetRepositoryReportAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        var prs = await auditRepository.ListPullRequestsByDateRangeAsync(from, to, cancellationToken);

        return prs
            .GroupBy(pr => pr.Repository, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new RepositoryReport(
                group.Key,
                group.Count(pr => pr.State == PullRequestState.Open),
                group.Count(pr => pr.State == PullRequestState.Merged),
                group.Count(pr => pr.State == PullRequestState.Closed),
                group.Select(pr => pr.Author).Distinct(StringComparer.OrdinalIgnoreCase).Count()))
            .ToArray();
    }
}
