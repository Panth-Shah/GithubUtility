namespace GithubUtility.App.Agents;

public sealed record ChatResponse(string Summary, object Data, string Agent, DateTimeOffset GeneratedAtUtc);
