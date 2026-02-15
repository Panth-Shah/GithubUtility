using GithubUtility.Core.Models;

namespace GithubUtility.Core.Abstractions;

public interface IPrAuditOrchestrator
{
    Task<IngestionRunResult> RunIngestionAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<OpenPrSummary>> GetOpenPrReportAsync(string? repository, int olderThanDays, CancellationToken cancellationToken);

    Task<IReadOnlyList<UserStatSummary>> GetUserStatsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);

    Task<ReleaseAuditSummary> GetReleaseAuditSummaryAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);

    Task<IReadOnlyList<RepositoryReport>> GetRepositoryReportAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
}
