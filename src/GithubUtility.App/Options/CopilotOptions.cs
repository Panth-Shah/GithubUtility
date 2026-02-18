using System.ComponentModel.DataAnnotations;

namespace GithubUtility.App.Options;

public sealed class CopilotOptions
{
    public const string SectionName = "Copilot";

    /// <summary>
    /// Path to the Copilot CLI binary.
    /// On a local dev machine this defaults to "copilot" (resolved from PATH).
    /// In a Docker-based deployment set this to the absolute path inside the container,
    /// e.g. "/usr/local/bin/copilot".
    /// </summary>
    [Required]
    public string CliPath { get; init; } = "copilot";

    /// <summary>
    /// Optional URL of an already-running Copilot CLI server, e.g. "localhost:8080".
    /// When provided the app connects to that server instead of spawning a new CLI process.
    /// Useful when running the CLI as a separate sidecar container in ACS.
    /// </summary>
    public string? CliUrl { get; init; }

    /// <summary>
    /// Name of the environment variable that carries the GitHub token used by the
    /// Copilot CLI to authenticate with GitHub.
    /// The value is read from the host environment at runtime and forwarded to the CLI process.
    /// Set the actual token via an Azure Key Vault reference or App Service secret.
    /// </summary>
    public string GitHubTokenEnvVar { get; init; } = "GITHUB_TOKEN";

    /// <summary>
    /// Model passed to the Copilot CLI session, e.g. "gpt-4o" or "claude-sonnet-4-5".
    /// </summary>
    [Required]
    public string Model { get; init; } = "gpt-4o";

    /// <summary>
    /// System prompt appended to every new Copilot session.
    /// </summary>
    public string SystemPrompt { get; init; } =
        "You are a helpful assistant for GitHub PR auditing. " +
        "Use available tools to answer questions about pull requests, code reviews, and repository activity.";
}
