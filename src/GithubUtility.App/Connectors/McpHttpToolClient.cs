using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using GithubUtility.App.Options;
using Microsoft.Extensions.Options;

namespace GithubUtility.App.Connectors;

public sealed class McpHttpToolClient(HttpClient httpClient, IOptions<GitHubConnectorOptions> options) : IMcpToolClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly GitHubConnectorOptions _options = options.Value;

    public async Task<JsonNode?> InvokeToolAsync(string toolName, object arguments, CancellationToken cancellationToken)
    {
        var endpoint = _options.Mcp.Endpoint;
        var payload = new
        {
            name = toolName,
            arguments
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Content = new StringContent(JsonSerializer.Serialize(payload, SerializerOptions), Encoding.UTF8, "application/json");

        if (!string.IsNullOrWhiteSpace(_options.Mcp.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.Mcp.ApiKey);
        }

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonNode.Parse(json);
    }
}
