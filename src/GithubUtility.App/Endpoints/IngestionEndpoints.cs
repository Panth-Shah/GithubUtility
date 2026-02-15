using System.Diagnostics;
using System.Diagnostics.Metrics;
using GithubUtility.Core.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace GithubUtility.App.Endpoints;

public static class IngestionEndpoints
{
    public static RouteGroupBuilder MapIngestionEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/run", RunIngestion)
            .WithName("RunIngestion")
            .WithTags("Ingestion")
            .WithOpenApi(operation =>
            {
                operation.Summary = "Trigger manual ingestion";
                operation.Description = "Manually triggers the PR ingestion process for all configured repositories";
                return operation;
            })
            .RequireRateLimiting("ingestion");

        return group;
    }

    internal static async Task<IResult> RunIngestion(
        IPrAuditOrchestrator orchestrator,
        Meter meter,
        ActivitySource activitySource,
        CancellationToken cancellationToken)
    {
        using var activity = activitySource.StartActivity("Ingestion Run");
        var startTime = Stopwatch.GetTimestamp();

        try
        {
            var result = await orchestrator.RunIngestionAsync(cancellationToken);

            var elapsedMs = Stopwatch.GetElapsedTime(startTime).TotalMilliseconds;

            var counter = meter.CreateCounter<int>("ingestion.runs");
            counter.Add(1);

            var durationHist = meter.CreateHistogram<double>("ingestion.duration");
            durationHist.Record(elapsedMs);

            var prCounter = meter.CreateCounter<int>("ingestion.prs_processed");
            prCounter.Add(result.PullRequestCount);

            activity?.SetTag("repos_processed", result.RepositoryCount);
            activity?.SetTag("prs_processed", result.PullRequestCount);
            activity?.SetTag("errors", result.ErrorCount);

            return Results.Ok(result);
        }
        catch (Exception ex)
        {
            activity?.SetTag("error", true);
            activity?.SetTag("exception.message", ex.Message);
            throw;
        }
    }
}
