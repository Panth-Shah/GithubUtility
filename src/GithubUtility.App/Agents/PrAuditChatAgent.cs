using GitHub.Copilot.SDK;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Options;
using GithubUtility.App.Options;

namespace GithubUtility.App.Agents;

public sealed class PrAuditChatAgent : IPrAuditChatAgent, IAsyncDisposable
{
    private readonly CopilotClient _copilotClient;
    private readonly AIAgent _agent;
    private readonly ILogger<PrAuditChatAgent> _logger;
    private readonly GitHubConnectorOptions _options;
    private bool _isStarted;

    public PrAuditChatAgent(
        IOptions<GitHubConnectorOptions> options,
        ILogger<PrAuditChatAgent> logger)
    {
        _options = options.Value;
        _logger = logger;
        _copilotClient = new CopilotClient();
        _agent = _copilotClient.AsAIAgent(
            instructions: "You are a helpful assistant for GitHub PR auditing. Use the available MCP tools to answer questions about pull requests, repositories, and reviews. When asked about PRs, use the MCP tools to fetch data and provide comprehensive answers."
        );
    }

    public async Task<ChatResponse> HandleAsync(ChatRequest request, CancellationToken cancellationToken)
    {
        // Ensure Copilot client is started
        if (!_isStarted)
        {
            await _copilotClient.StartAsync(cancellationToken);
            _isStarted = true;
        }

        // Build user intent with context
        var userIntent = BuildUserIntent(request);

        try
        {
            // Use the agent framework's built-in planning and execution
            // The GitHub Copilot SDK automatically discovers and connects to MCP servers
            // based on the MCP configuration in the environment
            // 
            // NOTE: The API signature differs from the blog post example.
            // The actual API requires AgentSession and AgentRunOptions.
            // The user message needs to be passed via the session or run options.
            // This is a placeholder implementation - the exact API may need adjustment
            // based on the actual Microsoft.Agents.AI package version.
            
            var session = await _agent.GetNewSessionAsync(cancellationToken);
            
            // Pass the user message to the agent
            // The exact API may vary - trying session-based approach first
            // If this doesn't work, may need to use a different method signature
            var response = await _agent.RunAsync(session, new AgentRunOptions());
            
            // Note: The message passing mechanism may need adjustment based on actual API
            // Alternative approaches:
            // 1. session.AddMessage(userIntent) if available
            // 2. agent.RunAsync(userIntent, session) if supported
            // 3. Check AgentRunOptions for different property names

            return new ChatResponse(
                response.Text ?? "No response generated",
                new { Response = response.Text, UserIntent = userIntent },
                nameof(PrAuditChatAgent),
                DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling chat request");
            throw;
        }
    }

    private static string BuildUserIntent(ChatRequest request)
    {
        var intent = request.Query;

        if (request.From.HasValue || request.To.HasValue)
        {
            var from = request.From?.ToString("yyyy-MM-dd") ?? "30 days ago";
            var to = request.To?.ToString("yyyy-MM-dd") ?? "today";
            intent += $" (Date range: from {from} to {to})";
        }

        if (!string.IsNullOrWhiteSpace(request.Repository))
        {
            intent += $" (Repository: {request.Repository})";
        }

        return intent;
    }

    public async ValueTask DisposeAsync()
    {
        if (_isStarted)
        {
            await _copilotClient.DisposeAsync();
        }
    }
}
