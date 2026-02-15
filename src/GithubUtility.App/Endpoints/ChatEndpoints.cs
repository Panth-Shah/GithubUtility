using GithubUtility.App.Agents;
using GithubUtility.App.Services;

namespace GithubUtility.App.Endpoints;

public static class ChatEndpoints
{
    public static RouteGroupBuilder MapChatEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/query", HandleChatQuery)
            .WithName("ChatQuery")
            .WithTags("Chat")
            .WithOpenApi(operation =>
            {
                operation.Summary = "Query the AI chat agent";
                operation.Description = "Ask natural language questions about pull requests using the AI-powered chat agent";
                return operation;
            })
            .RequireRateLimiting("chat");

        return group;
    }

    internal static async Task<IResult> HandleChatQuery(
        ChatRequest request,
        IPrAuditChatAgent agent,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return Results.BadRequest(new { Error = "Query is required." });
        }

        var response = await agent.HandleAsync(request, cancellationToken);
        return Results.Ok(response);
    }
}
