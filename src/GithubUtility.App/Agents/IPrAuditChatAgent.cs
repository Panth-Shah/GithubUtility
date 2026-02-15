namespace GithubUtility.App.Agents;

public interface IPrAuditChatAgent
{
    Task<ChatResponse> HandleAsync(ChatRequest request, CancellationToken cancellationToken);
}
