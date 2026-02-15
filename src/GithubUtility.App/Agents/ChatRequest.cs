namespace GithubUtility.App.Agents;

public sealed record ChatRequest(string Query, DateTimeOffset? From = null, DateTimeOffset? To = null, string? Repository = null);
