using System.ComponentModel.DataAnnotations;

namespace GithubUtility.App.Options;

public sealed class McpConnectorOptions
{
    [Required(ErrorMessage = "Endpoint is required")]
    [Url(ErrorMessage = "Endpoint must be a valid URL")]
    public string Endpoint { get; init; } = "http://localhost:8080/tools/invoke";

    public string? ApiKey { get; init; }

    [Required]
    public string ListRepositoriesTool { get; init; } = "list_repositories";

    [Required]
    public string ListPullRequestsTool { get; init; } = "list_pull_requests";

    [Required]
    public string ListPullRequestReviewsTool { get; init; } = "list_reviews";

    [Required]
    public string ListPullRequestEventsTool { get; init; } = "list_pull_request_events";
}
