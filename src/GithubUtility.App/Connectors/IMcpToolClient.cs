using System.Text.Json.Nodes;

namespace GithubUtility.App.Connectors;

public interface IMcpToolClient
{
    Task<JsonNode?> InvokeToolAsync(string toolName, object arguments, CancellationToken cancellationToken);
}
