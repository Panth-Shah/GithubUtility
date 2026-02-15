using GithubUtility.Core.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace GithubUtility.App.Endpoints;

public static class ReportsEndpoints
{
    public static RouteGroupBuilder MapReportsEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/open-prs", GetOpenPRs)
            .WithName("GetOpenPRs")
            .WithTags("Reports")
            .WithOpenApi(operation =>
            {
                operation.Summary = "Get open pull requests report";
                operation.Description = "Returns a list of open pull requests, optionally filtered by repository and age";
                return operation;
            })
            .RequireRateLimiting("api");

        group.MapGet("/user-stats", GetUserStats)
            .WithName("GetUserStats")
            .WithTags("Reports")
            .WithOpenApi(operation =>
            {
                operation.Summary = "Get user statistics";
                operation.Description = "Returns PR and review statistics per user for a given date range";
                return operation;
            })
            .RequireRateLimiting("api");

        group.MapGet("/release-summary", GetReleaseSummary)
            .WithName("GetReleaseSummary")
            .WithTags("Reports")
            .WithOpenApi(operation =>
            {
                operation.Summary = "Get release audit summary";
                operation.Description = "Returns aggregated statistics about PRs for release auditing purposes";
                return operation;
            })
            .RequireRateLimiting("api");

        group.MapGet("/repositories", GetRepositoryReport)
            .WithName("GetRepositoryReport")
            .WithTags("Reports")
            .WithOpenApi(operation =>
            {
                operation.Summary = "Get repository report";
                operation.Description = "Returns PR statistics grouped by repository for a given date range";
                return operation;
            })
            .RequireRateLimiting("api");

        return group;
    }

    internal static async Task<IResult> GetOpenPRs(
        [FromQuery] string? repository,
        [FromQuery] int? olderThanDays,
        IPrAuditOrchestrator orchestrator,
        CancellationToken cancellationToken)
    {
        var report = await orchestrator.GetOpenPrReportAsync(repository, olderThanDays ?? 0, cancellationToken);
        return Results.Ok(report);
    }

    internal static async Task<IResult> GetUserStats(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        IPrAuditOrchestrator orchestrator,
        CancellationToken cancellationToken)
    {
        var toValue = to ?? DateTimeOffset.UtcNow;
        var fromValue = from ?? toValue.AddDays(-30);

        var report = await orchestrator.GetUserStatsAsync(fromValue, toValue, cancellationToken);
        return Results.Ok(report);
    }

    internal static async Task<IResult> GetReleaseSummary(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        IPrAuditOrchestrator orchestrator,
        CancellationToken cancellationToken)
    {
        var toValue = to ?? DateTimeOffset.UtcNow;
        var fromValue = from ?? toValue.AddDays(-30);

        var summary = await orchestrator.GetReleaseAuditSummaryAsync(fromValue, toValue, cancellationToken);
        return Results.Ok(summary);
    }

    internal static async Task<IResult> GetRepositoryReport(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        IPrAuditOrchestrator orchestrator,
        CancellationToken cancellationToken)
    {
        var toValue = to ?? DateTimeOffset.UtcNow;
        var fromValue = from ?? toValue.AddDays(-30);

        var report = await orchestrator.GetRepositoryReportAsync(fromValue, toValue, cancellationToken);
        return Results.Ok(report);
    }
}
