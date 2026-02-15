using GithubUtility.Core.Models;

namespace GithubUtility.Core.Abstractions;

public interface IAuditRepository
{
    Task<RepositoryCursor?> GetCursorAsync(string repository, CancellationToken cancellationToken);

    Task SaveCursorAsync(RepositoryCursor cursor, CancellationToken cancellationToken);

    Task UpsertPullRequestsAsync(IReadOnlyList<PullRequestRecord> pullRequests, CancellationToken cancellationToken);

    Task<IReadOnlyList<PullRequestRecord>> ListPullRequestsAsync(CancellationToken cancellationToken);
    
    // New filtered query methods
    Task<IReadOnlyList<PullRequestRecord>> ListPullRequestsByStateAsync(PullRequestState state, string? repository, CancellationToken cancellationToken);
    
    Task<IReadOnlyList<PullRequestRecord>> ListPullRequestsByDateRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
}
