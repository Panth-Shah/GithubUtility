using System.Text.Json;
using System.Text.Json.Nodes;
using GithubUtility.App.Connectors;
using GithubUtility.App.Options;
using GithubUtility.Core.Abstractions;
using GithubUtility.Core.Models;
using Microsoft.Extensions.Options;

namespace GithubUtility.App.Infrastructure;

public sealed class McpGitHubDataSource(IMcpToolClient mcpToolClient, IOptions<GitHubConnectorOptions> options) : IGitHubDataSource
{
    private readonly GitHubConnectorOptions _options = options.Value;

    public async Task<IReadOnlyList<string>> ListRepositoriesAsync(CancellationToken cancellationToken)
    {
        var explicitRepos = _options.Repositories;
        if (explicitRepos.Count > 0)
        {
            return explicitRepos;
        }

        var response = await mcpToolClient.InvokeToolAsync(
            _options.Mcp.ListRepositoriesTool,
            new { organization = _options.Organization },
            cancellationToken);

        var repositories = response?["result"]?["repositories"]?.AsArray();
        if (repositories is null)
        {
            return Array.Empty<string>();
        }

        return repositories
            .Select(node => node?.GetValue<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToArray();
    }

    public async Task<IReadOnlyList<PullRequestRecord>> ListPullRequestsUpdatedSinceAsync(
        string repository,
        DateTimeOffset since,
        CancellationToken cancellationToken)
    {
        var prResponse = await mcpToolClient.InvokeToolAsync(
            _options.Mcp.ListPullRequestsTool,
            new
            {
                repository,
                state = "all",
                updated_since = since
            },
            cancellationToken);

        var pullRequests = prResponse?["result"]?["pull_requests"]?.AsArray();
        if (pullRequests is null)
        {
            return Array.Empty<PullRequestRecord>();
        }

        var results = new List<PullRequestRecord>();

        foreach (var node in pullRequests)
        {
            if (node is null)
            {
                continue;
            }

            var prNumber = node["number"]?.GetValue<int>() ?? 0;
            if (prNumber <= 0)
            {
                continue;
            }

            var updatedAt = ParseDate(node["updated_at"], DateTimeOffset.UtcNow);
            if (updatedAt < since)
            {
                continue;
            }

            var reviews = await FetchReviewsAsync(repository, prNumber, cancellationToken);
            var events = await FetchEventsAsync(repository, prNumber, cancellationToken);

            results.Add(new PullRequestRecord(
                repository,
                prNumber,
                node["title"]?.GetValue<string>() ?? string.Empty,
                node["author"]?.GetValue<string>() ?? "unknown",
                ParseState(node),
                ParseDate(node["created_at"], updatedAt),
                updatedAt,
                ParseNullableDate(node["merged_at"]),
                reviews,
                events));
        }

        return results;
    }

    private async Task<IReadOnlyList<PullRequestReviewRecord>> FetchReviewsAsync(string repository, int prNumber, CancellationToken cancellationToken)
    {
        var response = await mcpToolClient.InvokeToolAsync(
            _options.Mcp.ListPullRequestReviewsTool,
            new
            {
                repository,
                pull_request_number = prNumber
            },
            cancellationToken);

        var reviews = response?["result"]?["reviews"]?.AsArray();
        if (reviews is null)
        {
            return Array.Empty<PullRequestReviewRecord>();
        }

        return reviews
            .Where(node => node is not null)
            .Select(node => new PullRequestReviewRecord(
                node!["reviewer"]?.GetValue<string>() ?? "unknown",
                node["state"]?.GetValue<string>() ?? "UNKNOWN",
                ParseDate(node["submitted_at"], DateTimeOffset.UtcNow)))
            .ToArray();
    }

    private async Task<IReadOnlyList<PullRequestEventRecord>> FetchEventsAsync(string repository, int prNumber, CancellationToken cancellationToken)
    {
        var response = await mcpToolClient.InvokeToolAsync(
            _options.Mcp.ListPullRequestEventsTool,
            new
            {
                repository,
                pull_request_number = prNumber
            },
            cancellationToken);

        var events = response?["result"]?["events"]?.AsArray();
        if (events is null)
        {
            return Array.Empty<PullRequestEventRecord>();
        }

        return events
            .Where(node => node is not null)
            .Select(node => new PullRequestEventRecord(
                node!["event_type"]?.GetValue<string>() ?? "UNKNOWN",
                node["actor"]?.GetValue<string>() ?? "unknown",
                ParseDate(node["occurred_at"], DateTimeOffset.UtcNow)))
            .ToArray();
    }

    private static PullRequestState ParseState(JsonNode node)
    {
        var mergedAt = ParseNullableDate(node["merged_at"]);
        if (mergedAt is not null)
        {
            return PullRequestState.Merged;
        }

        var state = node["state"]?.GetValue<string>();
        return state?.Equals("open", StringComparison.OrdinalIgnoreCase) == true
            ? PullRequestState.Open
            : PullRequestState.Closed;
    }

    private static DateTimeOffset ParseDate(JsonNode? node, DateTimeOffset fallback)
    {
        var parsed = ParseNullableDate(node);
        return parsed ?? fallback;
    }

    private static DateTimeOffset? ParseNullableDate(JsonNode? node)
    {
        if (node is null)
        {
            return null;
        }

        if (DateTimeOffset.TryParse(node.ToString(), out var parsed))
        {
            return parsed;
        }

        if (node is JsonValue value && value.TryGetValue<long>(out var unixSeconds))
        {
            return DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        }

        return null;
    }
}
