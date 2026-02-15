using GithubUtility.Core.Models;

namespace GithubUtility.Core.Abstractions;

public interface IGitHubDataSource
{
    Task<IReadOnlyList<string>> ListRepositoriesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<PullRequestRecord>> ListPullRequestsUpdatedSinceAsync(
        string repository,
        DateTimeOffset since,
        CancellationToken cancellationToken);
}
