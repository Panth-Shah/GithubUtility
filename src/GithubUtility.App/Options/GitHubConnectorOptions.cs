using System.ComponentModel.DataAnnotations;

namespace GithubUtility.App.Options;

public sealed class GitHubConnectorOptions
{
    public const string SectionName = "GitHubConnector";

    [Required(ErrorMessage = "Mode is required")]
    [RegularExpression("^(Mcp|Sample)$", ErrorMessage = "Mode must be Mcp or Sample")]
    public string Mode { get; init; } = "Mcp";

    public string? Organization { get; init; }

    public List<string> Repositories { get; init; } = new();

    [Required]
    public McpConnectorOptions Mcp { get; init; } = new();
}
