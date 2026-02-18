using GitHub.Copilot.SDK;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Options;
using GithubUtility.App.Options;

namespace GithubUtility.App.Agents;

public sealed class PrAuditChatAgent : IPrAuditChatAgent, IAsyncDisposable
{
    private readonly CopilotClient _copilotClient;
    private readonly AIAgent _agent;
    private readonly CopilotOptions _options;
    private readonly ILogger<PrAuditChatAgent> _logger;
    private bool _isStarted;

    public PrAuditChatAgent(
        IOptions<CopilotOptions> options,
        ILogger<PrAuditChatAgent> logger)
    {
        _options = options.Value;
        _logger = logger;

        _copilotClient = new CopilotClient(BuildClientOptions(_options));
        _agent = _copilotClient.AsAIAgent(instructions: _options.SystemPrompt);
    }

    public async Task<ChatResponse> HandleAsync(ChatRequest request, CancellationToken cancellationToken)
    {
        if (!_isStarted)
        {
            await _copilotClient.StartAsync(cancellationToken);
            _isStarted = true;
        }

        var userIntent = BuildUserIntent(request);

        try
        {
            var session = await _agent.GetNewSessionAsync(cancellationToken);

            // RunAsync(string, session, options, ct) sends the user message and returns the response.
            var response = await _agent.RunAsync(userIntent, session, new AgentRunOptions(), cancellationToken);

            var text = response.Text;

            return new ChatResponse(
                text,
                new { Response = text, UserIntent = userIntent },
                nameof(PrAuditChatAgent),
                DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling chat request for intent: {UserIntent}", userIntent);
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

    private static CopilotClientOptions BuildClientOptions(CopilotOptions options)
    {
        var clientOptions = new CopilotClientOptions
        {
            CliPath = options.CliPath,
        };

        if (!string.IsNullOrWhiteSpace(options.CliUrl))
        {
            clientOptions.CliUrl = options.CliUrl;
        }

        // Forward the GitHub token from the host environment into the CLI subprocess so
        // it can authenticate with GitHub without writing credentials to disk.
        var token = Environment.GetEnvironmentVariable(options.GitHubTokenEnvVar);
        if (!string.IsNullOrWhiteSpace(token))
        {
            clientOptions.Environment = new Dictionary<string, string>
            {
                [options.GitHubTokenEnvVar] = token
            };
        }

        return clientOptions;
    }

    public async ValueTask DisposeAsync()
    {
        if (_isStarted)
        {
            await _copilotClient.DisposeAsync();
        }
    }
}
